# Trombone — 슬라이드·MIDI

**Parent:** [`_index.md`](../_index.md)

## Why

트럼본의 핵심 연주 메커니즘을 구현한다. 오른손 X 이동이 슬라이드를 제어하고,
슬라이드 위치가 MIDI 노트로 변환되어 리듬게임·오디오 시스템과 연동된다.

## What

오른손의 트럼본 로컬 X 좌표를 슬라이드 위치에 실시간 매핑하고,
슬라이드 이동 범위를 7개 구간으로 분할해 구간 진입마다 해당 MIDI 노트를 발음한다.

- 오른손 로컬 X 좌표 → 슬라이드 오브젝트 위치 실시간 업데이트 (Min·Max 클램프)
- 7구간 분할 → 구간 변경 시 이전 NoteOff + 새 NoteOn
- InstrumentBase MIDI 파이프라인과 연동

## Behavior

- **Given** 플레이어가 앵커 안에 있음 (01-anchor-and-grip 활성 상태)
  **When** 오른손이 트럼본 로컬 X 방향으로 이동
  **Then** 슬라이드 GameObject가 해당 X 위치로 이동.
          새 구간 진입 시 이전 노트 NoteOff + 새 노트 NoteOn 발생.

- **Given** 슬라이드가 이동 중
  **When** 오른손 X가 슬라이드 최소·최대 범위를 벗어남
  **Then** 슬라이드가 Min·Max 경계에서 클램프됨.

- **Given** 앵커 이탈
  **When** 이탈 감지
  **Then** 현재 발음 중인 노트가 NoteOff됨.

## Out of Scope

- 앵커 진입·이탈 상태 관리 및 손 교체 (→ 01-anchor-and-grip)
- 슬라이드 7구간 이상의 연속 피치 보간
- 버튼 트리거 기반 발음 (후속 피처)
- 실제 오디오 클립 제작·임포트

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
