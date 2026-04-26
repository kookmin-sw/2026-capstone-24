# Chart Format

**Parent:** [`_index.md`](../_index.md)

## Why

곡 데이터는 Unity 외부에서 사람이 읽고 편집할 수 있어야 하며, 한 파일이 다음 정보를 모두 담아야 한다: 곡 메타데이터, 마스터 시계(BPM/박자), 여러 악기 트랙의 노트, 채널과 악기의 매핑. 바이너리 MIDI 파일은 텍스트 편집이 불가하고, 산업 표준 텍스트 포맷(.chart, .ssc, BMS)은 게임 특화 레인 코드에 묶여 있어 우리 시스템의 "MIDI 노트 번호 직접 표현" 요구와 어긋난다. 따라서 우리 시스템 전용의 텍스트 차트 포맷을 정의해야 한다.

## What

곡 한 개는 한 개의 `.vmsong` 텍스트 차트 파일로 표현된다. 한 파일에는 다음이 포함된다.

- **곡 메타데이터** — 제목, 작곡가 등 곡 자체의 정보. 난이도 정보는 포함하지 않는다.
- **채널-악기 매핑** — 채널 번호(1–16 범위)가 어떤 악기에 대응하는지의 테이블. 채널 10은 항상 드럼에 예약된다 (일반 MIDI 컨벤션).
- **마스터 시계 정보** — 곡 전체 또는 구간별 BPM과 박자, 노트 위치를 결정짓는 tick 해상도.
- **채널별 노트 트랙** — 각 채널마다의 노트 목록. 노트는 시간 위치, MIDI 노트 번호, 길이, 세기로 구성된다.

같은 차트 파일을 같은 파서로 두 번 읽으면 같은 결과가 나온다. 차트 파일에 정의된 정보만으로 모든 노트의 절대 시각(초)을 결정할 수 있다.

## Behavior

- **Given** 차트 파일에 BPM과 tick 해상도가 명시됨
  **When** 한 노트가 어떤 tick 위치에 있음
  **Then** 그 노트의 절대 시각(초)이 BPM·해상도로부터 결정된다.

- **Given** 한 채널 트랙이 여러 노트를 가짐
  **When** 차트가 파싱됨
  **Then** 각 노트는 시간 위치, MIDI 노트 번호, 길이, 세기 정보를 가진 시퀀스로 표현된다.

- **Given** 곡 안에 채널-악기 매핑이 정의됨
  **When** 런타임이 차트를 사용함
  **Then** 채널 번호만으로 어떤 악기 트랙인지 식별 가능하다.

- **Given** 곡이 진행되는 도중 BPM이 바뀌는 구간이 차트에 정의됨
  **When** 차트가 파싱됨
  **Then** 각 노트의 절대 시각은 BPM 변화 구간을 누적 반영하여 계산된다.

## Out of Scope

- 차트 파일을 Unity 에셋으로 가져오는 임포트 메커니즘 → `chart-import`.
- 런타임에서 노트가 발화되거나 채점되는 동작 → `timing-clock` / `judgment` / `accompaniment`.
- 차트 외부 작성 도구나 MIDI ↔ 차트 변환기.
- 곡과 난이도의 연결 — 차트 파일은 자신이 어느 난이도인지 모른다. 곡 ↔ 난이도 매핑은 차트 파일 외부 자료구조의 책임이다.

## Implementation Plans

| 번호 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 001 | `.vmsong` Syntax & Runtime Data Model | Ready | [001-chart-syntax-and-data-model.md](../plans/001-chart-syntax-and-data-model.md) |
| 002 | `.vmsong` Parser & Tick→Seconds Resolver | Ready | [002-chart-parser-and-time-resolver.md](../plans/002-chart-parser-and-time-resolver.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 번호는 전역 일련번호.

## Open Questions

_없음. 모든 항목이 본문에 반영됨._
