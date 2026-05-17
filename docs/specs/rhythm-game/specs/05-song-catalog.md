# Song Catalog

**Parent:** [`_index.md`](../_index.md)

**Tech Spec:** skipped

## Why

현재 `ISongCatalog` 구현체는 ScriptableObject 데이터베이스에 의존하며 차트 경로를 하드코딩한다. 곡을 추가하려면 ScriptableObject를 매번 새로 만들어야 한다. `Songs` 폴더에 `.vmsong` 파일만 넣으면 자동으로 곡이 등록되도록 한다.

## What

`StreamingAssets/Songs/` 폴더를 게임 시작 시 스캔해 `.vmsong` 파일을 자동 탐지하고, 각 파일의 메타·채널 정보를 파싱해 곡 목록을 구성하는 카탈로그 구현체.

- `Songs/` 폴더에 `.vmsong` 파일을 추가하는 것만으로 곡이 카탈로그에 등록된다.
- 지원 악기 목록은 파일의 `[Channels]` 섹션에서 자동 추출된다.
- 곡 ID·제목·아티스트는 `[Meta]` 섹션에서 추출된다.

## Behavior

- **Given** `StreamingAssets/Songs/` 에 `.vmsong` 파일이 1개 이상 존재
  **When** 게임 시작(카탈로그 초기화)
  **Then** 해당 파일들이 곡 목록에 등록되어 세션 패널에서 선택 가능하다.

- **Given** 새 `.vmsong` 파일을 `Songs/` 에 추가 후 게임 재시작
  **When** 세션 패널의 곡 목록을 봄
  **Then** 새 파일이 목록에 포함된다.

- **Given** `.vmsong` 파일의 `[Channels]` 섹션에 `instrument=DrumKit`
  **When** DrumKit을 잡고 세션 패널을 열어 곡 목록을 봄
  **Then** 해당 곡이 활성 상태로 표시된다.

## Out of Scope

- 파일 형식(`.vmsong`) 파싱 규칙 변경
- 런타임 중 파일 추가 감지(핫리로드) — 재시작 시에만 갱신
- 난이도별 별도 파일 지원 — 파일 1개 = 곡 1개·단일 난이도
- 기존 `RhythmSongDatabase` ScriptableObject 제거

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-17 | Folder-Scan Song Catalog | Done | [`../plans/2026-05-17-linksky0311-folder-scan-song-catalog.md`](../plans/2026-05-17-linksky0311-folder-scan-song-catalog.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
