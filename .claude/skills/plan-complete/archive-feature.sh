#!/usr/bin/env bash
set -euo pipefail

feature="${1:-}"
[ -n "$feature" ] || { echo "ERR: feature name required. Usage: archive-feature.sh <feature-kebab>"; exit 1; }

src="docs/specs/$feature"
dst="docs/specs/_archive/$feature"

# 1. 안전성 검증
[ -d "$src" ] || { echo "ERR: $src missing"; exit 1; }
[ ! -d "$dst" ] || { echo "ERR: $dst already exists (partial archive?). 수동 점검 후 재실행."; exit 1; }
[ -z "$(git status --porcelain)" ] || { echo "ERR: working tree dirty. commit 또는 stash 후 재실행."; exit 1; }

# 2. _index.md Sub-Specs 표 모든 Done 검증
index_file="$src/_index.md"
[ -f "$index_file" ] || { echo "ERR: $index_file missing"; exit 1; }

not_done=$(grep -E '^\|' "$index_file" | grep -v 'Sub-Spec\|---|Done' | grep -v '^\| *Sub-Spec' | grep -E '\|' | grep -v '| Done |' || true)
if [ -n "$not_done" ]; then
  echo "ERR: Sub-Specs 표에 Done이 아닌 항목이 있습니다:"
  echo "$not_done"
  exit 1
fi

# 3. Open Questions 0건 검증
open_q_files=$(grep -rln '## Open Questions' "$src" 2>/dev/null || true)
if [ -n "$open_q_files" ]; then
  for f in $open_q_files; do
    # Open Questions 섹션 이후 비어 있지 않은 항목 탐색
    has_open=$(awk '/^## Open Questions/{found=1; next} found && /^##/{found=0} found && /^- /{print}' "$f" || true)
    if [ -n "$has_open" ]; then
      echo "ERR: $f 에 미해결 Open Questions가 있습니다:"
      echo "$has_open"
      exit 1
    fi
  done
fi

# 4. 외부 참조 grep (점검만, 갱신은 메인 세션)
echo "=== external_references ==="
grep -rln --include='*.md' "$feature" docs/ 2>/dev/null \
  | grep -v "^$src/" \
  | grep -v "^$dst/" \
  || echo "(없음)"

# 5. archive 디렉터리 생성 + 이동
mkdir -p "$dst"

[ -f "$src/_index.md" ] && git mv "$src/_index.md" "$dst/_index.md"

[ -d "$src/specs" ] && git mv "$src/specs" "$dst/specs"

[ -d "$src/decisions" ] && git mv "$src/decisions" "$dst/decisions"

if [ -d "$src/plans" ]; then
  if [ -d "$dst/plans" ]; then
    # _archive에 plans가 이미 있으면 병합
    find "$src/plans" -maxdepth 1 -name '*.md' | while IFS= read -r plan_file; do
      git mv "$plan_file" "$dst/plans/"
    done
    rmdir "$src/plans" 2>/dev/null || echo "WARN: $src/plans not empty after merge"
  else
    git mv "$src/plans" "$dst/plans"
  fi
fi

# 6. .feature-build-state.json 삭제 (.gitignore라 git에 영향 없음)
[ -f "$src/.feature-build-state.json" ] && rm "$src/.feature-build-state.json"

# 7. 빈 src 폴더 정리
rmdir "$src" 2>/dev/null || echo "WARN: $src not empty (수동 점검 필요)"

# 8. 결과 출력
echo "=== moved ==="
git status --porcelain

echo "=== readme_board_update ==="
sub_count=$(find "$dst/specs" -maxdepth 1 -name '*.md' 2>/dev/null | wc -l | tr -d ' ')
plan_done=$(find "$dst/plans" -maxdepth 1 -name '*.md' 2>/dev/null | wc -l | tr -d ' ')
echo "| [$feature](_archive/$feature/_index.md) | Done | $sub_count | $plan_done/$plan_done | |"
