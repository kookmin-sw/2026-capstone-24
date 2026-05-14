#!/usr/bin/env bash
# room-server 이미지를 ECR 에 push + Fargate task definition 의 image tag 갱신 래퍼.
# 사용법:
#   tools/push-room-server-image.sh <tag>
#   (예) tools/push-room-server-image.sh v0.1.3
#
# 환경변수:
#   AWS_REGION           기본 ap-northeast-2
#   AWS_ACCOUNT_ID       필수. aws sts get-caller-identity --query Account 로 확인 가능
#   ECR_REPO_NAME        기본 murang-room-server
#   TASK_DEF_FAMILY      기본 murang-room-server (해당 family 의 최신 revision 을 기반으로 새 revision 등록)
#   SKIP_TASK_DEF_UPDATE 1 이면 task definition 갱신 단계 skip
#
# 사전 조건:
#   - aws CLI v2 인증 완료 (aws sts get-caller-identity 통과)
#   - docker 데몬 동작
#   - Unity Editor 에서 RoomServerBuildMenu → Build Dedicated Server (Linux) 완료
#     산출: Builds/RoomAutomation/LinuxServer/RoomServer.x86_64
set -euo pipefail

TAG="${1:-}"
if [[ -z "$TAG" ]]; then
  echo "사용: $0 <tag>" >&2
  exit 2
fi

AWS_REGION="${AWS_REGION:-ap-northeast-2}"
AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:-}"
ECR_REPO_NAME="${ECR_REPO_NAME:-murang-room-server}"
TASK_DEF_FAMILY="${TASK_DEF_FAMILY:-murang-room-server}"
SKIP_TASK_DEF_UPDATE="${SKIP_TASK_DEF_UPDATE:-0}"

if [[ -z "$AWS_ACCOUNT_ID" ]]; then
  AWS_ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
fi

REGISTRY="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
IMAGE_URI="${REGISTRY}/${ECR_REPO_NAME}:${TAG}"

echo "==> ECR 로그인 ${REGISTRY}"
aws ecr get-login-password --region "$AWS_REGION" \
  | docker login --username AWS --password-stdin "$REGISTRY"

echo "==> docker build (docker/dedicated-server/Dockerfile)"
docker build -f docker/dedicated-server/Dockerfile -t "$IMAGE_URI" .

echo "==> docker push ${IMAGE_URI}"
docker push "$IMAGE_URI"

if [[ "$SKIP_TASK_DEF_UPDATE" == "1" ]]; then
  echo "==> SKIP_TASK_DEF_UPDATE=1 — task definition 갱신 skip"
  echo "image: $IMAGE_URI"
  exit 0
fi

echo "==> 기존 task definition '${TASK_DEF_FAMILY}' 최신 revision 조회"
CURRENT_DEF="$(aws ecs describe-task-definition \
  --task-definition "$TASK_DEF_FAMILY" \
  --region "$AWS_REGION" \
  --query taskDefinition)"

if [[ -z "$CURRENT_DEF" || "$CURRENT_DEF" == "null" ]]; then
  echo "ERROR: task definition family '${TASK_DEF_FAMILY}' 가 존재하지 않습니다." >&2
  echo "       콘솔에서 최초 1회 등록 후 재시도하세요." >&2
  exit 1
fi

# 새 revision 등록: image 만 교체, 나머지는 그대로.
NEW_DEF="$(echo "$CURRENT_DEF" | jq --arg img "$IMAGE_URI" '
  .containerDefinitions |= map(if .name == "room-server" then .image = $img else . end)
  | del(.taskDefinitionArn, .revision, .status, .requiresAttributes, .compatibilities, .registeredAt, .registeredBy)
')"

echo "==> 새 revision 등록"
REGISTERED="$(aws ecs register-task-definition \
  --region "$AWS_REGION" \
  --cli-input-json "$NEW_DEF" \
  --query 'taskDefinition.taskDefinitionArn' \
  --output text)"

echo "registered: $REGISTERED"
echo "image:      $IMAGE_URI"
