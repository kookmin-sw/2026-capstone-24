# validate-compose.ps1 — docker-compose.ec2-dev.yml syntax / structure 검증.
# aws-dev topology plan AC #1 [auto-hard] 충족용. CI 또는 로컬에서 직접 호출.
#
# 사용법:
#   tools/validate-compose.ps1
#   $env:ENV_FILE = '~/.env.aws-dev'; tools/validate-compose.ps1
#
# .env.aws-dev.example 을 기본 ENV_FILE 로 사용해 placeholder 값으로
# interpolation 한다 (실 secret 불필요). 통과 시 exit 0, 실패 시 throw.
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$envFile = if ($env:ENV_FILE) { $env:ENV_FILE } else { Join-Path $repoRoot '.env.aws-dev.example' }
$composeFile = if ($env:COMPOSE_FILE) { $env:COMPOSE_FILE } else { Join-Path $repoRoot 'docker-compose.ec2-dev.yml' }

Write-Host "Validating $composeFile with --env-file $envFile"
& docker compose -f $composeFile --env-file $envFile config --quiet
if ($LASTEXITCODE -ne 0) { throw "docker compose config 검증 실패 (exit $LASTEXITCODE)" }
Write-Host 'OK'
