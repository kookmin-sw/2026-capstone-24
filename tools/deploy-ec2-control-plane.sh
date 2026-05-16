#!/usr/bin/env bash
# EC2 control-plane (spring + mariadb) 기동/재배포 래퍼.
# 본 스크립트는 EC2 인스턴스 안에서 직접 실행한다.
# 사용법:
#   tools/deploy-ec2-control-plane.sh up         # 빌드 + up
#   tools/deploy-ec2-control-plane.sh redeploy   # git pull + rebuild + up
#   tools/deploy-ec2-control-plane.sh down       # 정지 (볼륨 보존)
#   tools/deploy-ec2-control-plane.sh status     # ps + actuator/health
set -euo pipefail

ENV_FILE="${ENV_FILE:-$HOME/.env.aws-dev}"
COMPOSE_FILE="docker-compose.ec2-dev.yml"
COMMAND="${1:-up}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: env file not found: $ENV_FILE" >&2
  echo "       cp .env.aws-dev.example $ENV_FILE 후 값을 채워 넣으세요." >&2
  exit 1
fi

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "ERROR: $COMPOSE_FILE not found. 리포지토리 루트에서 실행하세요." >&2
  exit 1
fi

compose() {
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"
}

case "$COMMAND" in
  up)
    compose build spring
    compose up -d
    ;;
  redeploy)
    git pull --ff-only
    compose build spring
    compose up -d
    ;;
  down)
    compose down
    ;;
  status)
    compose ps
    echo "---"
    curl -fsS http://localhost:8080/actuator/health || echo "actuator/health 응답 실패"
    ;;
  *)
    echo "ERROR: unknown command '$COMMAND'" >&2
    echo "사용: $0 {up|redeploy|down|status}" >&2
    exit 2
    ;;
esac
