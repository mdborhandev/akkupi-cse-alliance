#!/usr/bin/env bash
# EF Core CLI wrapper: scripts/ef.sh migrations add InitialCreate
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -f "$root/.secrets/dev-env.sh" ]]; then
  # shellcheck disable=SC1091
  source "$root/.secrets/dev-env.sh"
fi
exec dotnet ef "$@" --project "$root/src/ACA.Infrastructure/ACA.Infrastructure.csproj" --startup-project "$root/src/ACA.Web/ACA.Web.csproj"
