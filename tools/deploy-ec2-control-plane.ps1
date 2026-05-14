# EC2 control-plane (spring + mariadb) 기동/재배포 래퍼 (Windows host -> EC2 SSH 경유 또는 EC2 안에서 실행).
# Linux 호스트는 deploy-ec2-control-plane.sh 를 사용한다. 본 스크립트는 같은 작업을 PowerShell 에서 호출.
# 사용법:
#   tools/deploy-ec2-control-plane.ps1 -Command up
#   tools/deploy-ec2-control-plane.ps1 -Command redeploy
#   tools/deploy-ec2-control-plane.ps1 -Command down
#   tools/deploy-ec2-control-plane.ps1 -Command status
param(
    [ValidateSet('up','redeploy','down','status')]
    [string]$Command = 'up',
    [string]$EnvFile = "$HOME\.env.aws-dev"
)

$ErrorActionPreference = 'Stop'
$composeFile = 'docker-compose.ec2-dev.yml'

if (-not (Test-Path $EnvFile)) {
    Write-Error "env file not found: $EnvFile`n       cp .env.aws-dev.example $EnvFile 후 값을 채우세요."
}

if (-not (Test-Path $composeFile)) {
    Write-Error "$composeFile not found. 리포지토리 루트에서 실행하세요."
}

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments)] $rest)
    & docker compose -f $composeFile --env-file $EnvFile @rest
    if ($LASTEXITCODE -ne 0) { throw "docker compose 실패 (exit $LASTEXITCODE)" }
}

switch ($Command) {
    'up' {
        Invoke-Compose build spring
        Invoke-Compose up -d
    }
    'redeploy' {
        git pull --ff-only
        if ($LASTEXITCODE -ne 0) { throw 'git pull 실패' }
        Invoke-Compose build spring
        Invoke-Compose up -d
    }
    'down' {
        Invoke-Compose down
    }
    'status' {
        Invoke-Compose ps
        Write-Host '---'
        try { Invoke-WebRequest -Uri 'http://localhost:8080/actuator/health' -UseBasicParsing | Select-Object -ExpandProperty Content }
        catch { Write-Host "actuator/health 응답 실패: $($_.Exception.Message)" }
    }
}
