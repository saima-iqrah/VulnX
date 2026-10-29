# VulnX deployment guide

## 1. What you need
- A Linux server (or any host) with Docker and Docker Compose.
- A domain name and a reverse proxy that terminates HTTPS (Caddy, nginx, Traefik or a cloud load balancer).
  The API forces `Secure` cookies and HSTS outside Development, so production must be served over HTTPS.
- Outbound internet access from the API container (the scanner and the optional AI provider connect out).

## 2. First start (Docker Compose)
```bash
cp .env.example .env          # then edit the values
docker compose up -d --build
docker compose logs -f api
```
Required values in `.env`:

| Variable | Meaning |
|---|---|
| `POSTGRES_PASSWORD` | Password of the `vulnx_app` database user (database name: `vulnx_db`) |
| `JWT_KEY` | 48+ random characters. `openssl rand -base64 48` |
| `PUBLIC_URL` | The public address of the site, e.g. `https://vulnx.example.com` (used for CORS and e-mail links) |
| `SEED_ENABLED`, `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD` | Create the first admin once; set `SEED_ENABLED=false` afterwards |

The API applies the EF Core migrations on start (`Database__MigrateOnStart=true`) and then seeds roles and the
scanner vulnerability catalog. Health check: `GET /api/health` (returns `Healthy` when the database is reachable).

Optional settings (same `Section__Key` naming as environment variables): `Smtp__Host`, `Smtp__Port`, `Smtp__User`,
`Smtp__Password`, `Smtp__From` for real e-mail; the `Ai__*` keys for an external AI provider. Without them the
built-in assistant and the logging e-mail sender are used.

## 3. HTTPS reverse proxy example (Caddy)
```
vulnx.example.com {
    reverse_proxy localhost:8080
}
```
SignalR uses WebSockets; Caddy forwards them automatically. With nginx add `proxy_http_version 1.1;` and the
`Upgrade` / `Connection` headers for `/hubs/`.

## 4. Database
- Another database name: change `POSTGRES_DB` / `Database=` in `docker-compose.yml` (and the health check) before the first start.
- Backup: `docker compose exec db pg_dump -U vulnx_app vulnx_db > vulnx_backup.sql`
- Restore into an empty database: `docker compose exec -T db psql -U vulnx_app vulnx_db < vulnx_backup.sql`
- Never edit an already applied migration. Change an entity, add a new migration, redeploy.

## 5. Updating
```bash
git pull
docker compose up -d --build
```

## 6. Hardening checklist
- [ ] `JWT_KEY` and `POSTGRES_PASSWORD` are random and only in `.env` / the secret store, never in git.
- [ ] `SEED_ENABLED=false` after the admin exists; the admin password is changed.
- [ ] HTTPS only; `PUBLIC_URL` is the https address.
- [ ] Port 5432 of the database is not published to the internet (compose does not publish it).
- [ ] SMTP configured, so verification and password-reset e-mails reach users.
- [ ] Scan rate limits reviewed in Admin > Settings.
- [ ] Backups scheduled; logs (JSON on stdout) collected.
- [ ] Only scan targets you own or have written permission to test.
