param(
    [string]$DbUrl = "jdbc:mariadb://localhost:3306/murang?sslMode=disable",
    [string]$DbUsername = "murang_app",
    [string]$DbPassword = "change-me",
    [string]$JwtSecret = "test-secret-for-local-dev-must-be-at-least-sixty-four-bytes-long-2026",
    [string]$Profile = "dev"
)

$ErrorActionPreference = "Stop"

$env:SPRING_PROFILES_ACTIVE = $Profile
$env:DB_URL = $DbUrl
$env:DB_USERNAME = $DbUsername
$env:DB_PASSWORD = $DbPassword
$env:MURANG_JWT_SECRET = $JwtSecret
$env:GRADLE_USER_HOME = Join-Path $PSScriptRoot ".gradle-user-home"

Set-Location $PSScriptRoot
./gradlew bootRun
