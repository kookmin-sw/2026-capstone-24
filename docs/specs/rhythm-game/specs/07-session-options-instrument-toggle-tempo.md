# Session Options — Instrument Toggle & Tempo

**Parent:** [`_index.md`](../_index.md)

## Why

플레이어가 (a) 자기 악기만 단독으로, 또는 다른 악기와 함께 듣는 옵션, (b) 빠른 곡을 느리게 또는 빠르게 연습하는 옵션이 필요하다. 현재 트랙 on/off UI는 `accompaniment`/`session-panel/02-start-menu-section` spec이 후속으로 미뤄둔 상태이며, 템포 컨트롤은 노트 낙하 속도에만 적용되고 자동 반주 사운드에는 적용되지 않는다.

## What

세션 시작 메뉴 섹션에서 사용자는 곡·난이도 선택 외에 (a) 플레이어 악기를 제외한 모든 악기 트랙의 on/off, (b) 템포 슬라이더를 조작한다. 시작 시 결정된 값이 그 세션 전체에 적용된다.

- 곡 선택 후 그 곡이 가진 (플레이어 악기를 제외한) 모든 악기 각각에 대해 on/off 토글 버튼 노출.
- 토글 기본 상태는 **모두 ON**.
- 토글 ON 악기는 선택된 난이도 차트를 자동 반주로 발화.
- 토글 OFF 악기는 사운드 발화 없음.
- 템포 컨트롤 초기값은 **플레이어 악기·선택 난이도** 파일의 `[Tempo]` BPM.
- 사용자 템포 조절 → 노트 낙하 속도와 모든 활성 자동 반주의 발화 타이밍이 그 BPM에 맞춰짐.
- 난이도 변경 시 토글 목록과 템포 초기값 재구성.

## Behavior

- **Given** `test_song`에 piano-easy, drum-easy, violin-easy 파일이 있고 플레이어는 piano를 잡음
  **When** `test_song`+`easy` 선택
  **Then** drum과 violin 토글 버튼 노출, 둘 다 ON 시작.

- **Given** 위 상태에서 violin 토글을 OFF
  **When** 시작 버튼을 누름
  **Then** drum 자동 반주는 발화, violin 자동 반주는 발화 없음.

- **Given** 곡 선택 직후 템포 슬라이더가 차트 [Tempo](예: 100 BPM) 값으로 표시됨
  **When** 사용자가 슬라이더를 80 BPM으로 조절
  **Then** 시작 후 노트는 80 BPM 기준 시각에 판정선 도달, 자동 반주 트랙도 80 BPM 기준 시각에 발화.

- **Given** 어떤 곡에 플레이어 악기 외 다른 악기 파일이 전혀 없음
  **When** 그 곡 선택
  **Then** 인스트루먼트 토글 버튼 미노출 (난이도+템포만 노출).

- **Given** 곡·난이도가 선택된 상태에서 사용자가 난이도 변경
  **When** 새 난이도 적용
  **Then** 토글 버튼 목록과 템포 초기값이 새 난이도 파일들 기준으로 재구성.

## Out of Scope

- 자동 반주 트랙의 노트를 NoteDisplayPanel에 시각화 — 사운드만 발화 (`accompaniment.md` 정책 유지).
- 트랙별 음량/믹스 — `session-panel/03-volume-section` 또는 후속.
- 세션 도중 템포·토글 변경 — 시작 전 설정만.
- 토글·템포 값 영구 저장 (앱 재시작·곡·난이도 변경 시 리셋).

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
