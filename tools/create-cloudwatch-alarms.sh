#!/usr/bin/env bash
# CloudWatch alarms (aws-dev topology) 일괄 생성 래퍼.
#
# 사전 조건:
#   - aws CLI v2 인증 + cloudwatch:PutMetricAlarm 권한.
#   - SNS topic 'murang-ops' 가 같은 region 에 존재해야 한다.
#     없다면: aws sns create-topic --name murang-ops --region ap-northeast-2
#
# 사용법:
#   tools/create-cloudwatch-alarms.sh
#   REGION=ap-northeast-2 tools/create-cloudwatch-alarms.sh
#   SNS_TOPIC_ARN=arn:aws:sns:...:murang-ops tools/create-cloudwatch-alarms.sh
#
# 멱등: 동일 alarm name 이면 덮어쓴다.
set -euo pipefail

REGION="${REGION:-ap-northeast-2}"
NAMESPACE="${NAMESPACE:-Murang/Room}"
SNS_TOPIC_ARN="${SNS_TOPIC_ARN:-}"

if [[ -z "$SNS_TOPIC_ARN" ]]; then
  ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
  SNS_TOPIC_ARN="arn:aws:sns:${REGION}:${ACCOUNT_ID}:murang-ops"
  echo "SNS_TOPIC_ARN 미지정 -> 자동 유도: $SNS_TOPIC_ARN"
fi

put_alarm() {
  local name="$1" metric="$2" stat="$3" threshold="$4" period="$5" evals="$6" cmp="$7" desc="$8"
  echo "  - $name ($metric $cmp $threshold over ${period}s x $evals)"
  aws cloudwatch put-metric-alarm \
    --region "$REGION" \
    --alarm-name "$name" \
    --alarm-description "$desc" \
    --namespace "$NAMESPACE" \
    --metric-name "$metric" \
    --statistic "$stat" \
    --threshold "$threshold" \
    --period "$period" \
    --evaluation-periods "$evals" \
    --comparison-operator "$cmp" \
    --treat-missing-data notBreaching \
    --alarm-actions "$SNS_TOPIC_ARN" \
    --ok-actions "$SNS_TOPIC_ARN"
}

echo "Creating CloudWatch alarms in region=$REGION namespace=$NAMESPACE"

put_alarm "murang-room-RunTaskFailure" "run_task_failure_count.count" "Sum" 1 300 1 \
  "GreaterThanOrEqualToThreshold" \
  "ECS RunTask 호출 실패 (capacity/AccessDenied 등) 가 5분 안에 1건 이상 발생"

put_alarm "murang-room-ReadyTimeout" "room_provision_latency.max" "Maximum" 120000 600 1 \
  "GreaterThanThreshold" \
  "provision 요청부터 READY 까지 10분 윈도우 최대 latency 가 120s 초과"

put_alarm "murang-room-UnhealthySurge" "unhealthy_termination_count.count" "Sum" 3 600 1 \
  "GreaterThanOrEqualToThreshold" \
  "UNHEALTHY -> TERMINATED 전이가 10분 윈도우 3건 이상"

put_alarm "murang-room-EcsCapacity" "run_task_failure_count.count" "Sum" 5 600 1 \
  "GreaterThanOrEqualToThreshold" \
  "ECS Fargate capacity 부족 의심 (전용 capacity metric 도입 전 fallback) — RunTask 실패가 10분 윈도우 5건 이상"

echo "Done. 확인: aws cloudwatch describe-alarms --alarm-name-prefix murang-room- --region $REGION"
