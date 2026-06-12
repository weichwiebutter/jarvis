#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"
dotnet run --project ./HermesRuntime/cli/Hermes.Cli.csproj -- startup-status
