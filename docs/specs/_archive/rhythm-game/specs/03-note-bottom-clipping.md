# Note Bottom Clipping

**Parent:** [`_index.md`](../_index.md)

## Why

현재 노트가 판정선을 지나쳐 UI 패널 바닥 아래까지 낙하하는 모습이 보인다. 판정이 끝난 노트가 화면 밖으로 계속 내려가면 시각적 노이즈가 발생하고, 플레이어는 이미 놓친 노트가 아직 판정 가능한 것처럼 착각할 수 있다. 판정선 하단에서 노트를 즉시 숨겨 UI 경계를 명확하게 한다.

## What

노트 오브젝트가 판정선(패널 하단 경계) 아래로 내려가는 순간, 화면에서 보이지 않게 처리한다. 노트가 생성되는 상단 방향 클리핑(패널 위로 삐져나오는 것)은 기존 동작을 유지한다.

- 판정선은 노트 낙하 패널의 하단 끝으로 정의한다.
- 노트가 판정선 아래로 완전히 벗어나면 노트 오브젝트가 보이지 않는다.
- 판정·Miss 판정 타이밍 로직은 이 sub-spec의 범위 밖이다 (변경 없음).

## Behavior

- **Given** 세션이 진행 중이고 노트가 낙하하고 있을 때
  **When** 노트의 하단 끝이 판정선(패널 하단)을 넘을 때
  **Then** 노트가 화면에서 보이지 않게 된다 (오브젝트 파괴 또는 비활성화)

- **Given** 노트가 판정선 위에 있을 때
  **When** 플레이어가 타이밍에 맞게 연주할 때
  **Then** 노트는 평상시와 동일하게 표시된다 (이 spec의 영향 없음)

- **Given** 패널 상단 바깥으로 노트가 생성될 때
  **When** 노트가 낙하 시작하여 패널 안으로 진입할 때
  **Then** 기존 동작 유지 — 상단 클리핑 처리는 변경 없음

## Out of Scope

- 판정(Perfect / Good / Miss) 타이밍 로직 변경
- 노트가 판정선을 지나는 순간에 Miss를 발생시키는 로직 (Judgment sub-spec 담당)
- 상단 클리핑 동작 변경

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-04 | Note Bottom Clipping — 판정선 하단 노트 숨김 | Done | [2026-05-04-linksky0311-note-bottom-clipping.md](../plans/2026-05-04-linksky0311-note-bottom-clipping.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

