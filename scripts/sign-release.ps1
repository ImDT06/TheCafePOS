param(
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [Parameter(Mandatory=$true)][string]$SignToolPath,
    [Parameter(Mandatory=$true)][uri]$TimestampUrl
)
$ErrorActionPreference = 'Stop'
# No certificate import, trust-store changes, or security-policy changes.
$root = Split-Path $PSScriptRoot -Parent
if ($TimestampUrl.Scheme -ne 'https') { throw 'Use an HTTPS RFC3161 timestamp endpoint from your certificate provider.' }
if (-not (Test-Path -LiteralPath $SignToolPath -PathType Leaf)) { throw 'Provide signtool.exe from the Windows SDK.' }
$thumbprint = $CertificateThumbprint.Replace(' ', '')
if ($thumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Invalid certificate thumbprint.' }
$cert = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $thumbprint)
if (-not $cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date) -or $cert.NotBefore -gt (Get-Date)) { throw 'A valid certificate with an accessible private key is required.' }
if ($cert.Subject -eq $cert.Issuer) { throw 'Self-signed certificates do not establish Smart App Control public trust.' }
if (-not ($cert.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')) { throw 'Certificate must permit code signing.' }
$rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($cert)
if ($null -eq $rsa) { throw 'Smart App Control requires an RSA signing certificate.' }
$rsa.Dispose()
$output = Join-Path $root ('artifacts/signed-release-' + [guid]::NewGuid().ToString('N'))
dotnet publish (Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/TheCafePOS_WPF.csproj') -c Release --self-contained false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed; nothing signed.' }
foreach ($name in @('TheCafePOS_WPF.exe', 'TheCafePOS_WPF.dll')) {
    $path = Join-Path $output $name
    & $SignToolPath sign /s My /sha1 $thumbprint /fd SHA256 /tr $TimestampUrl.AbsoluteUri /td SHA256 $path
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $name" }
    & $SignToolPath verify /pa /all /v $path
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $name" }
}
# Inventory vendor binaries without replacing their publishers' signatures.
$inventory = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Extension -in @('.dll','.exe') } | ForEach-Object {
    [pscustomobject]@{File=$_.FullName.Substring($output.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;Signature=(Get-AuthenticodeSignature -LiteralPath $_.FullName).Status.ToString()}
}
$inventory | Export-Csv (Join-Path $output 'binary-signatures.csv') -NoTypeInformation -Encoding UTF8
Write-Output "Signed app files: $output. Review dependency signatures and validate on the target device; Authenticode verification is not an App Control approval."
