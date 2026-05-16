# CloudWatch alarms (aws-dev topology) 일괄 생성 래퍼.
# Linux 호스트는 create-cloudwatch-alarms.sh 를 사용한다.
#
# 사전 조건:
#   - aws CLI v2 인증 + cloudwatch:PutMetricAlarm 권한.
#   - SNS topic 'murang-ops' 가 같은 region 에 존재해야 한다.
#     없다면: aws sns create-topic --name murang-ops --region ap-northeast-2
#
# 사용법:
#   tools/create-cloudwatch-alarms.ps1
#   tools/create-cloudwatch-alarms.ps1 -Region ap-northeast-2
#   tools/create-cloudwatch-alarms.ps1 -SnsTopicArn arn:aws:sns:...:murang-ops
#
# 멱등: 동일 alarm name 이면 덮어쓴다.
param(
    [string]$Region = 'ap-northeast-2',
    [string]$Namespace = 'Murang/Room',
    [string]$SnsTopicArn = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($SnsTopicArn)) {
    $accountId = (aws sts get-caller-identity --query Account --output text)
    if ($LASTEXITCODE -ne 0) { throw 'aws sts get-caller-identity 실패' }
    $SnsTopicArn = "arn:aws:sns:${Region}:${accountId}:murang-ops"
    Write-Host "SnsTopicArn 인자 미지정 -> 자동 유도: $SnsTopicArn"
}

function Put-Alarm {
    param(
        [string]$Name,
        [string]$Metric,
        [string]$Statistic,
        [double]$Threshold,
        [int]$Period,
        [int]$Evaluations,
        [string]$Comparison,
        [string]$Description
    )
    Write-Host "  - $Name ($Metric $Comparison $Threshold over ${Period}s x $Evaluations)"
    & aws cloudwatch put-metric-alarm `
        --region $Region `
        --alarm-name $Name `
        --alarm-description $Description `
        --namespace $Namespace `
        --metric-name $Metric `
        --statistic $Statistic `
        --threshold $Threshold `
        --period $Period `
        --evaluation-periods $Evaluations `
        --comparison-operator $Comparison `
        --treat-missing-data notBreaching `
        --alarm-actions $SnsTopicArn `
        --ok-actions $SnsTopicArn
    if ($LASTEXITCODE -ne 0) { throw "put-metric-alarm 실패: $Name (exit $LASTEXITCODE)" }
}

Write-Host "Creating CloudWatch alarms in region=$Region namespace=$Namespace"

Put-Alarm -Name 'murang-room-RunTaskFailure' `
    -Metric 'run_task_failure_count.count' `
    -Statistic 'Sum' `
    -Threshold 1 `
    -Period 300 `
    -Evaluations 1 `
    -Comparison 'GreaterThanOrEqualToThreshold' `
    -Description 'ECS RunTask 호출 실패 (capacity/AccessDenied 등) 가 5분 안에 1건 이상 발생'

Put-Alarm -Name 'murang-room-ReadyTimeout' `
    -Metric 'room_provision_latency.max' `
    -Statistic 'Maximum' `
    -Threshold 120000 `
    -Period 600 `
    -Evaluations 1 `
    -Comparison 'GreaterThanThreshold' `
    -Description 'provision 요청부터 READY 까지 10분 윈도우 최대 latency 가 120s 초과'

Put-Alarm -Name 'murang-room-UnhealthySurge' `
    -Metric 'unhealthy_termination_count.count' `
    -Statistic 'Sum' `
    -Threshold 3 `
    -Period 600 `
    -Evaluations 1 `
    -Comparison 'GreaterThanOrEqualToThreshold' `
    -Description 'UNHEALTHY -> TERMINATED 전이가 10분 윈도우 3건 이상'

Put-Alarm -Name 'murang-room-EcsCapacity' `
    -Metric 'run_task_failure_count.count' `
    -Statistic 'Sum' `
    -Threshold 5 `
    -Period 600 `
    -Evaluations 1 `
    -Comparison 'GreaterThanOrEqualToThreshold' `
    -Description 'ECS Fargate capacity 부족 의심 (전용 capacity metric 도입 전 fallback) — RunTask 실패가 10분 윈도우 5건 이상'

Write-Host "Done. 확인: aws cloudwatch describe-alarms --alarm-name-prefix murang-room- --region $Region"
