# VulnX

AI-assisted web vulnerability assessment platform.
Stack: React + Vite (Phase 3+), ASP.NET Core Web API (.NET 10), PostgreSQL, EF Core, ASP.NET Identity, JWT.

## Phase status
| Phase | Scope | Status |
|---|---|---|
| 1-2 | Structure, PostgreSQL, EF Core, Identity, JWT + refresh tokens | done |
| 3 | React/Vite design system, landing, auth, dashboards | done |
| 4-6 | Target validation + SSRF protection, scanner modules, background worker, SignalR progress | done |
| 7-8 | Results, scoring, filtering, history, tags, AI assistant | done |
| 9 | PDF reports + verification | done |
| 10 | Admin panel, audit logs, system health | done |
| 11 | Scan comparison, notifications, CSV/JSON export | done (scheduled scans are optional in the spec and are not implemented) |
| 12 | Tests, hardening, documentation, deployment | unit tests, CI, Docker and deployment guide added (see below) |

> No error was found by static review of the C# code, but nothing could be compiled where these changes were made
> (no .NET SDK). Build once in Visual Studio / `dotnet build` and report any compiler message.

## Run in Visual Studio (Insiders / 2026) - recommended on Windows

1. Install: **Visual Studio** with the *ASP.NET and web development* workload, **.NET SDK 10**, **PostgreSQL 15+** (with pgAdmin).
2. Create the database (pgAdmin Query Tool or `psql`):
   ```sql
   CREATE USER vulnx_app WITH PASSWORD 'YOUR_DB_PASSWORD';
   CREATE DATABASE vulnx_db OWNER vulnx_app;
   ```
3. Open **`VulnX.slnx`** (File > Open > Project/Solution). Wait for NuGet restore to finish.
   (Do NOT run `create-solution.sh` / `dotnet new sln`; the solution already exists.)
4. Set **VulnX.API** as the startup project (right-click > Set as Startup Project).
5. Secrets: right-click **VulnX.API** > **Manage User Secrets**, paste (edit values):
   ```json
   {
     "ConnectionStrings:Default": "Host=localhost;Port=5432;Database=vulnx_db;Username=vulnx_app;Password=YOUR_DB_PASSWORD",
     "Jwt:Key": "PASTE_A_RANDOM_48+_CHAR_STRING",
     "Seed:Enabled": "true",
     "Seed:AdminEmail": "admin@example.com",
     "Seed:AdminPassword": "Use-A-Strong#Pass1"
   }
   ```
   Generate a key in PowerShell:
   `$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)`
6. Migrations already exist in `src/VulnX.Infrastructure/Persistence/Migrations` (InitialCreate, Phase7_8_TagsAndScanNotes, DatabaseProgrammability).
   **Do not run `Add-Migration InitialCreate` again.** In Development the API applies them automatically on start.
   To apply them by hand: **Package Manager Console**, *Default project* = **VulnX.Infrastructure**:
   ```powershell
   Update-Database -Project VulnX.Infrastructure -StartupProject VulnX.API
   ```
   Add a migration only after you change an entity: `Add-Migration SomeName -Project VulnX.Infrastructure -StartupProject VulnX.API -OutputDir Persistence\Migrations`
7. Press **F5** with the **http** profile. The API starts on `http://localhost:5080` and the Vite dev server is started with it
   (run `npm install` once inside `frontend/`). The browser opens `http://localhost:5173`. Health check: `http://localhost:5080/api/health`.
8. Test the API: open `src/VulnX.API/VulnX.API.http` in VS and click *Send Request* on each block
   (register > login > me > admin/ping). Note: the VS HTTP client does not keep the refresh cookie, so test `/refresh` with curl or Postman.
9. For the `https` profile run once: `dotnet dev-certs https --trust`.

Tests (later phases): **Test > Run All Tests** (Test Explorer).

## Setup (command line alternative)
Requirements: .NET SDK 10.x, PostgreSQL 15+, Node 20+ (from Phase 3).

```bash
# 1. PostgreSQL
#    CREATE USER vulnx_app WITH PASSWORD '...'; CREATE DATABASE vulnx_db OWNER vulnx_app;

# 2. Build
dotnet build VulnX.slnx

# 3. Secrets (development). See .env.example for every key.
cd src/VulnX.API
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=vulnx_db;Username=vulnx_app;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:Enabled" "true"
dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets set "Seed:AdminPassword" "Use-A-Strong#Pass1"
cd ../..

# 4. Migrations: they already exist. Development applies them on start; or apply them now:
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/VulnX.Infrastructure --startup-project src/VulnX.API

# 5. Run API (Development also starts the Vite dev server)
dotnet run --project src/VulnX.API --launch-profile http
```

## Try the auth API (Phase 2)
Replace `PORT` with the port printed by `dotnet run`. Use `-c jar.txt -b jar.txt` so curl keeps the refresh cookie.

```bash
# register (password: 10+ chars, upper, lower, digit, symbol)
curl -X POST http://localhost:PORT/api/auth/register -H "Content-Type: application/json" \
  -d '{"fullName":"Test User","email":"test@example.com","password":"Str0ng#Passw0rd"}'

# login -> returns accessToken; refresh token is set as HttpOnly cookie
curl -c jar.txt -X POST http://localhost:PORT/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Str0ng#Passw0rd"}'

# who am I
curl http://localhost:PORT/api/auth/me -H "Authorization: Bearer ACCESS_TOKEN"

# rotate tokens
curl -b jar.txt -c jar.txt -X POST http://localhost:PORT/api/auth/refresh

# admin route: 403 for normal user, 200 for the seeded admin
curl http://localhost:PORT/api/admin/ping -H "Authorization: Bearer ACCESS_TOKEN"

# logout
curl -b jar.txt -c jar.txt -X POST http://localhost:PORT/api/auth/logout
```
Email verification / password-reset links are written to the log by the dev email sender
(set `Logging:LogLevel:VulnX.Infrastructure` to `Debug` to see the link). Replace
`LoggingEmailSender` with a real provider before production.

## Security notes (Phase 2)
- Passwords hashed by ASP.NET Identity; lockout after 5 failed attempts (15 min).
- Access token: 15 min JWT. Refresh token: random 64 bytes, only its SHA-256 hash is stored, rotated on every refresh,
  reuse of a revoked token revokes all sessions of that user. Delivered as HttpOnly + SameSite=Strict cookie on `/api/auth`.
- Password change/reset revokes all refresh tokens. Login/logout/failed-login/password events go to `AuditLogs`.
- Auth endpoints are rate limited (10 req/min/IP). Generic messages avoid account enumeration (forgot-password, login).
- Known limit: a suspended user's already-issued access token stays valid until it expires (<= 15 min); refresh is blocked.
- Production: serve API over HTTPS (cookie is forced `Secure` outside Development) and keep API + frontend on the same site.

## Changing the database name
The database name exists only in the connection string (current default: `vulnx_db`). To rename it:
1. `CREATE DATABASE <new_name> OWNER vulnx_app;`
2. Update the user-secret: `dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=<new_name>;Username=vulnx_app;Password=..."` (or `ConnectionStrings__Default` in production / `.env`).
3. Start the API: the migrations create every table in the new empty database and the admin is seeded again (`Seed:Enabled=true`).
Old data stays in the old database; drop it with `DROP DATABASE <old_name>;` when you no longer need it.

## Tests
```bash
dotnet test tests/VulnX.Tests/VulnX.Tests.csproj     # SSRF guard, target validation, score model
cd frontend && npm test                               # only when frontend test files exist
```
Continuous integration: `.github/workflows/ci.yml`. Deployment: `DEPLOY.md` (Docker Compose included).

## Troubleshooting
- Browser opens `localhost:5173` but shows nothing -> run `npm install` once inside `frontend/`; the API starts Vite itself in Development.
- Live scan progress never moves -> make sure Vite proxies `/hubs` (`frontend/vite.config.ts`) and the API runs on port 5080.
- `relation "..." already exists` or `column ... does not exist` during start -> the database was created by an older version. Create a fresh database (see above) instead of patching it.
- `ConnectionStrings:Default is not configured` -> set the user-secret.
- `Jwt:Key is missing or shorter than 32 characters` -> set `Jwt:Key`.
- Register fails with role error -> run migrations, then restart so roles are seeded.
- `429 Too Many Requests` -> auth rate limit, wait a minute.
- Package version not found -> `dotnet add package <name>` for the latest stable and keep versions aligned.

## Session timeout and account security
- Users are signed out after 30 minutes without activity and admins after 15 minutes, with a 60-second warning.
  Override with `VITE_IDLE_USER_MINUTES` and `VITE_IDLE_ADMIN_MINUTES` (frontend environment variables).
  Activity in any browser tab counts, and signing out in one tab signs out all tabs.
- The refresh cookie lives `Jwt:RefreshTokenDays` days (default 7). Lower it for stricter sessions.
- **Security** page: recent sign-in activity and "Sign out of all devices" (revokes every refresh token).
- `Ctrl+K` opens a command palette for quick navigation.

## Email troubleshooting
1. Admin > Settings > Email delivery shows whether SMTP is configured and has a **Send test email** button that prints the exact error.
2. Gmail: `Smtp:Host=smtp.gmail.com`, `Smtp:Port=587`, `Smtp:User`, `Smtp:Password` (App Password without spaces), `Smtp:From` equal to `Smtp:User`.
3. Optional: `Smtp:Security` (Auto, StartTls, SslOnConnect, None) and `Smtp:AllowInvalidCertificate=true` (development servers only).
4. Without SMTP, the development build shows the verification / reset link on the page (`/api/dev/last-email`, Development only).

## CI
`.github/workflows/ci.yml` runs the backend tests and the frontend tests and build on every push and pull request.

## Database programmability (triggers, functions, stored procedures)
VulnX uses PostgreSQL features that are installed by the migration `*_DatabaseProgrammability` (hand-written SQL, applied by `dotnet ef database update` or automatically in development).

| Type | Name | Purpose |
|---|---|---|
| Trigger | `trg_scan_status_audit` | Writes every scan status change (`db.scan_status_changed`) to `AuditLogs` automatically |
| Trigger | `trg_auditlogs_no_update` | Makes `AuditLogs` append-only (rejects UPDATE) |
| Function | `vulnx_score_from_counts`, `vulnx_scan_score`, `vulnx_grade` | The database recalculates the security score and grade, used by the admin **score integrity check** and usable in any SELECT |
| Procedure | `vulnx_recalculate_scores()` | Corrects stored scores in one batch (`CALL vulnx_recalculate_scores();`) |
| Procedure | `vulnx_cleanup_old_data(365, 60)` | Same housekeeping as the background `MaintenanceWorker` (which does it with EF Core); can be run by hand with `CALL` |

Admin > **Database** lists the installed objects and runs the check, recalculation and cleanup.
`docs/database-demo.sql` contains demo queries for pgAdmin, plus the employees / salary example from the course material translated to PostgreSQL (in a separate `demo` schema).
Remove the objects with `dotnet ef database update <PreviousMigrationName>`.
