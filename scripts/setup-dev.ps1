$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $apiProject = 'src/Sawa3ed.Api'
    foreach ($secretName in @('Jwt:SigningKey', 'Jwt:OtpPepper')) {
        $secretBytes = New-Object byte[] 64
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        $rng.GetBytes($secretBytes)
        $rng.Dispose()
        $secretValue = [Convert]::ToBase64String($secretBytes)
        dotnet user-secrets set $secretName $secretValue --project $apiProject
        if ($LASTEXITCODE -ne 0) { throw 'Could not save development secrets.' }
    }
    dotnet restore Sawa3ed.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    Write-Host 'Ready. Run: dotnet run --project src/Sawa3ed.Api --launch-profile http'
    Write-Host 'This script rotates local signing/OTP secrets. Existing sessions and OTPs are invalidated.'
}
finally { Pop-Location }
