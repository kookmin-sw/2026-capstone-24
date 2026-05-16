#!/usr/bin/env bash
# validate-compose.sh — docker-compose.ec2-dev.yml syntax / structure 검증.
# aws-dev topology plan AC #1 [auto-hard] 충족용. CI 또는 로컬에서 직접 호출.
#
# 사용법:
#   tools/validate-compose.sh
#   ENV_FILE=~/.env.aws-dev tools/validate-compose.sh
#
# .env.aws-dev.example 을 기본 ENV_FILE 로 사용해 placeholder 값으로
# interpolation 한다 (실 secret 불필요). 통과 시 exit 0, 실패 시 exit 1.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ENV_FILE:-$REPO_ROOT/.env.aws-dev.example}"
COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/docker-compose.ec2-dev.yml}"

echo "Validating $COMPOSE_FILE with --env-file $ENV_FILE"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" config --quiet
echo "OK"
