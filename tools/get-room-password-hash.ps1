param(
    [Parameter(Mandatory = $true)]
    [string]$Password
)

$ErrorActionPreference = "Stop"

$bytes = [System.Text.Encoding]::UTF8.GetBytes($Password)
[System.Security.Cryptography.SHA256]$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $hashBytes = $sha256.ComputeHash($bytes)
}
finally {
    $sha256.Dispose()
}
[Convert]::ToBase64String($hashBytes)
