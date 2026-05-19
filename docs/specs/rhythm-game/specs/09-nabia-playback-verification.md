# 나비야 플레이백 & 반주 연동 검증

**Parent:** [`_index.md`](../_index.md)

**Tech Spec:** skipped

## Why

VMSong 파일이 파서 수준에서 유효하더라도 Unity 씬에서 곡 목록 표시·난이도 선택·반주 토글이
올바르게 동작하는지 사용자가 직접 확인해야 한다. 특히 "반주 토글에 표시되는 악기가 선택된
난이도와 동일한 파일을 사용하는지", "토글 OFF가 실제 반주 발화를 막는지"를 Unity 씬에서
검증한다.

## What

Unity 씬(SampleScene 또는 TestSceneSanyo)에서 다음 동작을 검증한다:
- 피아노를 잡은 상태에서 nabia 곡 선택 → Easy/Normal/Hard 버튼 표시
- 각 난이도 선택 시 드럼 토글 버튼이 해당 난이도 파일 기준으로 재구성되어 표시
- 드럼을 잡은 상태에서 nabia 선택 시 피아노 토글 버튼 표시
- 드럼 토글 OFF → 세션 시작 → 드럼 반주 소리 없음
- 드럼 토글 ON → 세션 시작 → 드럼 반주 소리 들림
- 나비야 피아노 노트가 시각적으로 내려오고 판정 가능

## Behavior

- **Given** 피아노를 잡고 nabia 선택, Easy 선택
  **When** 반주 토글 영역 확인
  **Then** "drum" 토글 버튼 1개 표시 (기본 ON), `nabia-drum-easy.vmsong` 기준

- **Given** 위 상태에서 drum 토글 OFF 후 플레이
  **When** 세션 진행
  **Then** 드럼 소리 없이 피아노 노트만 판정됨

- **Given** 피아노를 잡고 nabia + Normal 선택
  **When** 반주 토글 확인
  **Then** drum 토글 표시 (`nabia-drum-normal.vmsong` 기준)

- **Given** 드럼을 잡고 nabia + Hard 선택
  **When** 반주 토글 확인
  **Then** "piano" 토글 버튼 표시 (`nabia-piano-hard.vmsong` 기준)

## Out of Scope

- 노트 판정 점수 / 결과 화면 — judgment spec
- BPM 슬라이더 기능 검증 — 07-session-options spec
- 반주 음량 조절 — session-panel/03-volume-section
- test_song 플레이백 회귀 확인

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-18 | 나비야 플레이백 & 반주 연동 — Unity 씬 검증 | Ready | [`../plans/2026-05-18-linksky0311-nabia-playback-verification.md`](../plans/2026-05-18-linksky0311-nabia-playback-verification.md) |
| 2026-05-18 | 나비야 검증 실패 3건 수정 — DrumKit instrumentId 일치 + 난이도 숫자화 | Ready | [`../plans/2026-05-18-linksky0311-nabia-playback-fix.md`](../plans/2026-05-18-linksky0311-nabia-playback-fix.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
