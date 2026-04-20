#!/usr/bin/env bash
# Devcontainer post-create bootstrap. Restores the solution and pre-pulls the SQL Server image
# used by Testcontainers so the first integration-test run does not pay the download cost.
set -euo pipefail

echo "[devcontainer] dotnet --info"
dotnet --info

echo "[devcontainer] Restoring CaeriusNet.slnx"
dotnet restore CaeriusNet.slnx

echo "[devcontainer] Pre-pulling mcr.microsoft.com/mssql/server:2022-latest (best-effort)"
docker pull mcr.microsoft.com/mssql/server:2022-latest || \
  echo "[devcontainer] Skipping image pre-pull (docker not available yet)."

# Enable Testcontainers container-reuse to keep the SQL Server warm across `dotnet test`
# invocations inside the same workspace. Ryuk is left enabled (recommended in 2026) so
# leaked containers are still reaped when the docker daemon restarts.
PROPS="$HOME/.testcontainers.properties"
if [ ! -f "$PROPS" ] || ! grep -q '^testcontainers.reuse.enable=true' "$PROPS"; then
  echo "[devcontainer] Enabling Testcontainers reuse → $PROPS"
  printf 'testcontainers.reuse.enable=true\n' > "$PROPS"
fi

echo "[devcontainer] Ready."
