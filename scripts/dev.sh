#!/usr/bin/env bash
set -euo pipefail

PORT="${1:-5010}"

dotnet run \
  --project src/TotalCall.Client/TotalCall.Client.csproj \
  --no-launch-profile \
  --urls "http://localhost:${PORT}"
