# VulnX — AI-Assisted Web Vulnerability Assessment Platform

VulnX is a web-based security assessment platform that helps users identify common web security misconfigurations in websites they own or are authorized to test. It performs low-impact automated security checks, generates vulnerability reports, calculates a security score, and uses AI to explain findings in an understandable way.

## Features

- **Automated Security Scanning:** Performs automated checks for common web security misconfigurations.
- **Security Score:** Calculates a score from 0 to 100 and assigns a grade from A to F.
- **AI-Powered Explanations:** Uses the Gemini API to explain security findings and provide remediation guidance.
- **Scan History:** Stores previous scan results for future reference.
- **Scan Comparison:** Compares results from different scans.
- **Real-Time Progress:** Uses SignalR to provide scan progress updates.
- **PDF Reports:** Generates downloadable PDF security reports.
- **Data Export:** Supports CSV and JSON exports.
- **Authentication and Authorization:** Uses ASP.NET Core Identity and JWT-based authentication.
- **Admin Dashboard:** Includes administrative features and audit logging.
- **Email Notifications:** Supports email configuration for notifications.
- **Automated Testing and CI:** Includes test projects and a GitHub Actions workflow.
- **Docker Support:** Includes Docker-related configuration and deployment documentation.

## Security Checks

VulnX focuses on six categories of common web security issues:

| Category | What It Checks |
|---|---|
| Security Headers | Checks for missing or misconfigured security headers, such as Content-Security-Policy and Strict-Transport-Security. |
| TLS/HTTPS | Checks HTTPS usage, HTTP-to-HTTPS redirection, and related transport-security configuration. |
| Cookie Security | Checks cookie security attributes, including Secure, HttpOnly, and SameSite. |
| CORS Configuration | Identifies potentially permissive Cross-Origin Resource Sharing configurations. |
| Information Disclosure | Looks for exposed server information and unnecessary technical details. |
| HTML Content | Checks for issues such as mixed content and insecure form configurations. |

**Important:** VulnX is a lightweight security misconfiguration assessment tool, not a full penetration-testing solution. It does not perform comprehensive SQL injection or cross-site scripting exploitation.

## Technology Stack

### Frontend
- React 19
- TypeScript
- Vite 7

### Backend
- ASP.NET Core Web API
- C# and .NET 10
- Entity Framework Core
- ASP.NET Core Identity
- JWT Authentication
- SignalR
- HttpClient

### Database
- PostgreSQL
- Entity Framework Core Migrations
- Database triggers, functions, and stored procedures

### Additional Tools and Technologies
- Gemini API
- QuestPDF
- MailKit
- xUnit
- Vitest
- GitHub Actions
- Docker

## System Architecture

VulnX follows a layered architecture to separate business logic, application services, infrastructure, scanning operations, and API endpoints.

```text
VulnX/
├── src/
│   ├── VulnX.Domain/
│   ├── VulnX.Application/
│   ├── VulnX.Infrastructure/
│   ├── VulnX.Scanner/
│   └── VulnX.API/
├── tests/
│   └── VulnX.Tests/
├── frontend/
├── .github/
│   └── workflows/
│       └── ci.yml
├── VulnX.slnx
└── DEPLOY.md
```

*Note: The structure above is a high-level overview. Actual files and folders may vary by repository version.*

## How It Works

1. The user registers or logs in to the platform.
2. The user submits the URL of a website they own or are authorized to assess.
3. VulnX validates the target and applies security restrictions.
4. The background scanning process performs the supported security checks.
5. Scan findings and supporting evidence are stored in the database.
6. The platform calculates a security score and grade.
7. AI-generated explanations help users understand findings and possible remediation steps.
8. Users can review scan history, compare results, export data, and generate PDF reports.

## Security Considerations

VulnX includes safeguards intended to reduce the risk of misuse and unsafe scanning, including:

- Blocking localhost and private-network targets.
- URL and target validation.
- DNS and redirect revalidation.
- Request timeouts and request-budget limits.
- Rate limiting.
- Audit logging.
- Secure refresh-token handling and rotation.
- Hashed refresh-token storage.

Only scan websites that you own or have explicit permission to test.

## Prerequisites

Before running VulnX locally, install:

- .NET SDK 10
- PostgreSQL 15 or later
- Node.js 20 or later
- npm
- Visual Studio or another compatible code editor

## Installation and Setup

### 1. Clone the Repository

```bash
git clone <YOUR_GITHUB_REPOSITORY_URL>
cd <YOUR_REPOSITORY_FOLDER>
```

Replace the placeholders with your actual GitHub repository URL and folder name.

### 2. Configure PostgreSQL

Create a PostgreSQL user and database for the application.

Example:

```sql
CREATE USER vulnx_app WITH PASSWORD 'YOUR_STRONG_PASSWORD';
CREATE DATABASE vulnx_db OWNER vulnx_app;
```

Use your own strong password instead of the example value.

### 3. Configure Application Secrets

Configure the following settings using .NET User Secrets:

- `ConnectionStrings:Default`
- `Jwt:Key`
- `Seed:Enabled`
- `Seed:AdminEmail`
- `Seed:AdminPassword`

Example commands, run from the API project directory:

```bash
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=vulnx_db;Username=vulnx_app;Password=YOUR_STRONG_PASSWORD"

dotnet user-secrets set "Jwt:Key" "YOUR_LONG_RANDOM_SECRET_KEY"

dotnet user-secrets set "Seed:Enabled" "true"

dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"

dotnet user-secrets set "Seed:AdminPassword" "YOUR_STRONG_ADMIN_PASSWORD"
```

Use a sufficiently long, randomly generated JWT secret and a strong admin password. Keep real credentials out of source control.

### 4. Run the Backend

Open `VulnX.slnx` in Visual Studio and set `VulnX.API` as the startup project.

Alternatively, run:

```bash
dotnet build VulnX.slnx
dotnet run --project src/VulnX.API --launch-profile http
```

In development, the application is configured to apply existing Entity Framework Core migrations automatically.

### 5. Run the Frontend

Open a separate terminal:

```bash
cd frontend
npm install
npm run dev
```

### 6. Open the Application

After starting the backend and frontend, open:

- **Frontend:** http://localhost:5173
- **Backend API:** http://localhost:5080
- **Health Check:** http://localhost:5080/api/health

The URLs assume the default local development configuration.

## Running Tests

### Backend Tests

From the repository root:

```bash
dotnet test tests/VulnX.Tests/VulnX.Tests.csproj
```

### Frontend Tests

From the frontend directory:

```bash
npm test
```

Run this command if the frontend test script is defined in `package.json`.

## Configuration

Optional integrations can be configured through environment variables, application settings, or User Secrets, as appropriate.

- **Gemini API:** Configure the API key to enable AI-powered explanations.
- **SMTP:** Configure the `Smtp:*` settings to enable email delivery.
- **Database:** Configure `ConnectionStrings:Default`.
- **JWT:** Configure `Jwt:Key`.
- **Admin Seeding:** Configure the `Seed:*` settings.

Check `.env.example` and the application configuration files for the supported setting names. Never commit real API keys, passwords, tokens, or production connection strings.

## Troubleshooting

| Problem | Possible Solution |
|---|---|
| Frontend dependencies are missing | Run `npm install` inside the `frontend` directory. |
| Database connection fails | Verify PostgreSQL is running and check the connection string and credentials. |
| JWT configuration error | Configure a valid `Jwt:Key` through User Secrets or the appropriate environment configuration. |
| Database schema is outdated | Check the existing migrations and apply them using the project's configured migration workflow. |
| Scan requests are rate-limited | Wait before retrying and check the application's rate-limit configuration. |
| Email is not delivered | Verify SMTP settings and credentials. |
| Real-time progress is unavailable | Check that the API is running and that the SignalR connection is working. |

## Project Status

VulnX includes the following implemented areas, according to the project development documentation:

- User authentication and dashboards
- Background security scanning
- SSRF-related protections
- Real-time scan progress
- Findings and security scoring
- Scan history and comparison
- AI-assisted explanations
- PDF report generation
- Administrative and audit features
- CSV and JSON export
- Test and CI configuration
- Docker and deployment documentation

Scheduled scanning is not currently implemented. Check the repository for the latest implementation status before relying on any feature.

## Future Improvements

Potential future enhancements include:

- Scheduled and recurring scans
- Expanded security checks
- More comprehensive testing
- Improved reporting and visual analytics
- Additional deployment and monitoring options

## Responsible Use

VulnX is intended for authorized security assessment and educational purposes. Users are responsible for ensuring that they have permission to assess any submitted website. The platform's results should be treated as indicators of potential security issues, not as a guarantee that a website is secure or insecure.

## License

No license is specified here. Add a `LICENSE` file and update this section if you intend to distribute the project under a particular open-source license.

## Author

**Saima Kawsar**

GitHub: [saima-iqrah](https://github.com/saima-iqrah)

---

If you find this project useful, consider giving the repository a star on GitHub.
