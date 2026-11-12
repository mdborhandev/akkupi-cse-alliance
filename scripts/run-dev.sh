#!/usr/bin/env bash
# Run the ACA web app with local dev configuration (loads gitignored secrets).
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -f "$root/.secrets/dev-env.sh" ]]; then
  # shellcheck disable=SC1091
  source "$root/.secrets/dev-env.sh"
fi
exec dotnet run --project "$root/src/ACA.Web/ACA.Web.csproj" "$@"
