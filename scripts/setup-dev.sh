#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet user-secrets set 'Jwt:SigningKey' "$(openssl rand -base64 64 | tr -d '\n')" --project src/Sawa3ed.Api
dotnet user-secrets set 'Jwt:OtpPepper' "$(openssl rand -base64 64 | tr -d '\n')" --project src/Sawa3ed.Api
dotnet restore Sawa3ed.slnx
dotnet tool restore
echo 'Ready. Run: dotnet run --project src/Sawa3ed.Api --launch-profile http'
echo 'Local signing/OTP secrets were rotated; existing sessions and OTPs are invalidated.'
