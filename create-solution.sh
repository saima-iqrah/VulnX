#!/usr/bin/env bash
# Run once from the repo root. Requires the .NET SDK (10.x).
set -euo pipefail
dotnet new sln -n VulnX
dotnet sln VulnX.sln add src/VulnX.Domain src/VulnX.Application src/VulnX.Infrastructure src/VulnX.Scanner src/VulnX.API
dotnet restore
dotnet build
