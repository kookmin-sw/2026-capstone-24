# room-server 이미지를 ECR 에 push + Fargate task definition 의 image tag 갱신 래퍼 (PowerShell).
# 사용법:
#   tools/push-room-server-image.ps1 -Tag v0.1.3
#
# 사전 조건은 push-room-server-image.sh 와 동일.
param(
    [Parameter(Mandatory=$true)][string]$Tag,
    [string]$AwsRegion = 'ap-northeast-2',
    [string]$AwsAccountId,
    [string]$EcrRepoName = 'murang-room-server',
    [string]$TaskDefFamily = 'murang-room-server',
    [switch]$SkipTaskDefUpdate
)

$ErrorActionPreference = 'Stop'

if (-not $AwsAccountId) {
    $AwsAccountId = (& aws sts get-caller-identity --query Account --output text)
    if ($LASTEXITCODE -ne 0 -or -not $AwsAccountId) {
        throw 'aws sts get-caller-identity 실패. AWS 자격증명 확인 필요.'
    }
}

$registry = "$AwsAccountId.dkr.ecr.$AwsRegion.amazonaws.com"
$imageUri = "$registry/$EcrRepoName`:$Tag"

Write-Host "==> ECR 로그인 $registry"
& aws ecr get-login-password --region $AwsRegion | docker login --username AWS --password-stdin $registry
if ($LASTEXITCODE -ne 0) { throw 'docker login 실패' }

Write-Host "==> docker build (docker/dedicated-server/Dockerfile)"
& docker build -f docker/dedicated-server/Dockerfile -t $imageUri .
if ($LASTEXITCODE -ne 0) { throw 'docker build 실패' }

Write-Host "==> docker push $imageUri"
& docker push $imageUri
if ($LASTEXITCODE -ne 0) { throw 'docker push 실패' }

if ($SkipTaskDefUpdate) {
    Write-Host '==> SkipTaskDefUpdate — task definition 갱신 skip'
    Write-Host "image: $imageUri"
    exit 0
}

Write-Host "==> 기존 task definition '$TaskDefFamily' 최신 revision 조회"
$current = & aws ecs describe-task-definition --task-definition $TaskDefFamily --region $AwsRegion --query taskDefinition | ConvertFrom-Json
if (-not $current) {
    throw "task definition family '$TaskDefFamily' 가 존재하지 않습니다. 콘솔에서 최초 1회 등록 후 재시도."
}

foreach ($container in $current.containerDefinitions) {
    if ($container.name -eq 'room-server') {
        $container.image = $imageUri
    }
}

$current.PSObject.Properties.Remove('taskDefinitionArn') | Out-Null
$current.PSObject.Properties.Remove('revision') | Out-Null
$current.PSObject.Properties.Remove('status') | Out-Null
$current.PSObject.Properties.Remove('requiresAttributes') | Out-Null
$current.PSObject.Properties.Remove('compatibilities') | Out-Null
$current.PSObject.Properties.Remove('registeredAt') | Out-Null
$current.PSObject.Properties.Remove('registeredBy') | Out-Null

$json = $current | ConvertTo-Json -Depth 32
$tmp = New-TemporaryFile
# AWS CLI 의 file:// 파서는 UTF-8 BOM 을 binary 로 판단해 거부한다 (Windows
# PowerShell 5.1 의 Set-Content -Encoding utf8 은 BOM 을 붙이므로 .NET API 로
# BOM-less UTF-8 을 직접 기록).
[System.IO.File]::WriteAllText($tmp, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host '==> 새 revision 등록'
$registered = & aws ecs register-task-definition --region $AwsRegion --cli-input-json "file://$tmp" --query 'taskDefinition.taskDefinitionArn' --output text
Remove-Item $tmp -ErrorAction SilentlyContinue
if ($LASTEXITCODE -ne 0) { throw 'register-task-definition 실패' }

Write-Host "registered: $registered"
Write-Host "image:      $imageUri"
