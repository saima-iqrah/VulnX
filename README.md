VulnX — AI-Assisted Web Vulnerability Assessment Platform
VulnX is a web application for assessing common website security and configuration weaknesses. Users submit a website URL they own or are authorized to test, review evidence-based findings, understand risks through AI-assisted explanations, track scan history, and generate verifiable PDF reports.
> **Authorized use only:** Scan only websites you own or have explicit permission to assess. VulnX uses low-impact HTTP checks and is not a penetration-testing tool.
Features
Authentication and access control — registration, login, role-based access, JWT access tokens, and refresh-token rotation.
Safe target validation — URL normalization, authorization confirmation, SSRF protections, destination re-validation, request budgets, timeouts, and rate limits.
Modular security checks — security headers, TLS/HTTPS, cookies, CORS, information disclosure, and HTML content checks.
Background scanning — queued scans, cancellation, and live progress updates through SignalR.
Evidence-based findings — findings include severity, confidence, evidence, impact, remediation guidance, and references.
Security scoring — deterministic score from 0–100 and grade from A to F.
Results and history — dashboards, filtering, sorting, scan history, score trends, and comparison between scans.
AI assistant — explains findings and summarizes scans using stored scan data; a built-in fallback is available when the AI provider is not configured.
Reports and exports — PDF reports with report numbers and verification codes, plus CSV/JSON exports.
Notifications and administration — in-app notifications, user and scan management, platform settings, audit logs, and system health.
Testing and CI — backend tests and a GitHub Actions workflow for automated checks.
What VulnX Checks
VulnX focuses on security configuration and hygiene, with checks grouped into six modules:
Security headers
TLS/HTTPS configuration
Cookie security attributes
CORS configuration
Information disclosure
HTML content, including mixed content and insecure forms
The project specification describes 26 checks across these six modules. VulnX does not crawl a website or submit attack payloads, and it does not aim to detect issues such as SQL injection or cross-site scripting (XSS). A clean scan does not prove that a website is fully secure.
Technology Stack
Layer	Technology	Purpose
Frontend	React 19, TypeScript, Vite 7, React Router 7	Single-page UI, dashboards, and navigation
Backend	C#, ASP.NET Core Web API on .NET 10	API, application logic, and background processing
Database	PostgreSQL	Persistent storage for users, scans, findings, reports, and logs
ORM	Entity Framework Core 10, Npgsql	Database access and migrations
Authentication	ASP.NET Core Identity, JWT	Account management, roles, and API authentication
Real-time updates	ASP.NET Core SignalR	Live scan progress
Scanner	Custom engine using `HttpClient`	Low-impact HTTP checks and target-safety controls
PDF reports	QuestPDF	Report generation
Email	MailKit / SMTP	Verification, reset, and alert emails when configured
AI	Google Gemini API	Finding explanations, summaries, and chat
Testing / CI	xUnit, Vitest, Testing Library, GitHub Actions	Automated tests and continuous integration
Deployment	Docker, Docker Compose	Repeatable environment setup
Architecture
The backend is organized into five projects:
`VulnX.Domain` — core entities and enumerations.
`VulnX.Application` — interfaces, DTOs, and business rules such as score calculation.
`VulnX.Infrastructure` — persistence, identity, email, AI integration, PDF generation, and background workers.
`VulnX.Scanner` — scanner engine, modules, SSRF guard, and vulnerability catalog.
`VulnX.API` — controllers, SignalR hub, middleware, and application startup.
The React frontend communicates with the API over HTTP/HTTPS using JSON. SignalR provides live scan updates. PostgreSQL stores persistent application data.
Requirements
For local development, install:
.NET SDK 10.x
PostgreSQL 15 or later
Node.js 20 or later and npm
Visual Studio with the ASP.NET and web development workload (recommended on Windows), or a terminal-based development environment
Git
Getting Started
1. Clone the repository
```bash
git clone <YOUR_GITHUB_REPOSITORY_URL>
cd <YOUR_REPOSITORY_FOLDER>
```
Replace the placeholders with your actual repository URL and folder name.
2. Create the PostgreSQL database
Using pgAdmin Query Tool or `psql`, run:
```sql
CREATE USER vulnx_app WITH PASSWORD 'CHANGE_THIS_DB_PASSWORD';
CREATE DATABASE vulnx_db OWNER vulnx_app;
```
Use a strong local development password. Do not commit real credentials.
3. Configure development secrets
From the repository root, set the API's user secrets:
```bash
cd src/VulnX.API

dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=vulnx_db;Username=vulnx_app;Password=CHANGE_THIS_DB_PASSWORD"
dotnet user-secrets set "Jwt:Key" "REPLACE_WITH_A_RANDOM_SECRET_AT_LEAST_48_CHARACTERS_LONG"
dotnet user-secrets set "Seed:Enabled" "true"
dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets set "Seed:AdminPassword" "REPLACE_WITH_A_STRONG_ADMIN_PASSWORD"

cd ../..
```
Generate a random JWT key in PowerShell:
```powershell
$b = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
[Convert]::ToBase64String($b)
```
Use the generated value for `Jwt:Key`. Keep passwords, API keys, and connection strings out of source control.
4. Restore, build, and run
From the repository root:
```bash
dotnet restore VulnX.slnx
dotnet build VulnX.slnx
```
Install frontend dependencies:
```bash
cd frontend
npm install
cd ..
```
The existing EF Core migrations are in `src/VulnX.Infrastructure/Persistence/Migrations`. Do not create the initial migration again. In Development, the API applies existing migrations on startup. Alternatively, apply them manually:
```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/VulnX.Infrastructure --startup-project src/VulnX.API
```
Run the API using the HTTP launch profile:
```bash
dotnet run --project src/VulnX.API --launch-profile http
```
According to the current development configuration, the API is expected at `http://localhost:5080` and the Vite frontend at `http://localhost:5173`. Check the terminal output if the configured ports differ.
Frontend: `http://localhost:5173`
Health endpoint: `http://localhost:5080/api/health`
You can also open `VulnX.slnx` in Visual Studio, set `VulnX.API` as the startup project, and press F5. The API's Development configuration starts the Vite development server.
5. Optional integrations
Google Gemini: Configure the relevant AI settings and API key using user secrets or environment variables. If no AI provider is configured, the built-in fallback can be used.
SMTP email: Configure the `Smtp:*` settings to enable verification, password-reset, and alert emails. Without SMTP, development email links are exposed through the development-only flow described in the project documentation.
Review `.env.example` and `DEPLOY.md` for the complete environment and deployment settings.
Running Tests
Backend tests:
```bash
dotnet test tests/VulnX.Tests/VulnX.Tests.csproj
```
Frontend tests (when frontend test files are present):
```bash
cd frontend
npm test
```
GitHub Actions workflow: `.github/workflows/ci.yml`
Security Notes
Scan only targets for which you have explicit authorization.
The scanner blocks unsafe local and private destinations and re-validates resolved addresses and redirects.
Scan profiles have request and time budgets; rate limits restrict excessive use.
Passwords are hashed by ASP.NET Core Identity.
Access tokens are short-lived; refresh tokens are rotated and stored as hashes.
Use HTTPS in production and store secrets in a secure secret manager or environment variables.
AI explanations use selected scan data. Do not send results to an external AI provider if those results must remain private.
The development email sender and development-only endpoints must not be treated as production email infrastructure.
Troubleshooting
Problem	Things to check
Frontend opens but is blank	Run `npm install` inside `frontend/`; check the Vite terminal output.
Scan progress does not update	Confirm the API is running and the frontend proxy for `/hubs` is configured correctly.
`ConnectionStrings:Default` is missing	Set the API user secret or the corresponding production environment variable.
JWT key is missing or too short	Configure a strong `Jwt:Key` secret.
Database migration errors	Confirm the database is compatible with the current migrations; for an outdated local database, back up needed data and use a fresh development database if appropriate.
`429 Too Many Requests`	A rate limit was reached; wait before retrying.
Email is not sent	Check Admin → Settings → Email delivery and configure SMTP.
Project Status
The project documentation marks the main feature phases as implemented, including scanning, results, AI assistance, reporting, administration, and exports. Build and test the project in your own environment before relying on it; environment-specific compilation and deployment issues may still need to be resolved.
Future Improvements
Verify domain ownership before allowing scans.
Add carefully scoped checks for outdated JavaScript libraries and exposed diagnostic pages.
Add scheduled scans and email reports.
Use an external job queue for multi-server deployments.
Evaluate detection precision and recall against known test websites.
References
OWASP Top 10
OWASP Web Security Testing Guide
OWASP SSRF Prevention Cheat Sheet
ASP.NET Core documentation
Entity Framework Core documentation
PostgreSQL documentation
React documentation
Google Gemini API documentation
---
VulnX — Understand web security findings, prioritize fixes, and generate evidence-based reports.
