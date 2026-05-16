#!/usr/bin/env bash
# mysqldump-cron.sh — EC2 control-plane mariadb 의 일 1회 백업.
# cron 또는 수동으로 호출. 결과는 /var/backups/mariadb/ 에 gzip 압축으로 저장.
#
# 사용법:
#   sudo tools/mysqldump-cron.sh                              # 기본값
#   REPO_DIR=/srv/murang ENV_FILE=/srv/.env tools/mysqldump-cron.sh
#
# cron 예시 (/etc/cron.d/murang-mariadb-dump):
#   SHELL=/bin/bash
#   PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
#   30 4 * * * root /home/ubuntu/2026-capstone-24/tools/mysqldump-cron.sh >> /var/log/mariadb-dump.log 2>&1
#
# 환경변수 (override 가능):
#   REPO_DIR       리포지토리 루트 (기본 /home/ubuntu/2026-capstone-24)
#   ENV_FILE       compose --env-file 위치 (기본 /home/ubuntu/.env.aws-dev)
#   BACKUP_DIR     덤프 파일 보관 디렉터리 (기본 /var/backups/mariadb)
#   RETENTION_DAYS RETENTION_DAYS 이상 된 파일은 자동 삭제 (기본 14)
set -euo pipefail

REPO_DIR="${REPO_DIR:-/home/ubuntu/2026-capstone-24}"
ENV_FILE="${ENV_FILE:-/home/ubuntu/.env.aws-dev}"
COMPOSE_FILE="$REPO_DIR/docker-compose.ec2-dev.yml"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/mariadb}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "ERROR: $COMPOSE_FILE not found" >&2
  exit 1
fi
if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: $ENV_FILE not found" >&2
  exit 1
fi

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

TS="$(date -u +%Y%m%d-%H%M%S)"
OUT="$BACKUP_DIR/murang-${TS}.sql.gz"

# mariadb 컨테이너 안에 이미 MARIADB_ROOT_PASSWORD / MARIADB_DATABASE 환경변수가
# compose 의 mariadb.environment 로 주입돼 있어 sh -c 안에서 직접 참조 가능.
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T mariadb \
  sh -c 'mariadb-dump --single-transaction --quick --routines --triggers -u root -p"$MARIADB_ROOT_PASSWORD" "$MARIADB_DATABASE"' \
  | gzip -9 > "$OUT"

SIZE="$(stat -c%s "$OUT")"
if [[ "$SIZE" -lt 1024 ]]; then
  echo "ERROR: dump 파일이 비정상적으로 작음 ($SIZE bytes): $OUT" >&2
  exit 2
fi
chmod 600 "$OUT"

# rotation
find "$BACKUP_DIR" -type f -name 'murang-*.sql.gz' -mtime +"$RETENTION_DAYS" -delete

echo "$(date -Iseconds) mariadb backup -> $OUT ($SIZE bytes)"
