#!/usr/bin/env bash
set -euo pipefail

dotnet test TotalCall.sln -m:1 /nr:false --no-build
