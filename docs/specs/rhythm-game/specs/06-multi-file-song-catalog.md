# Multi-File Song Catalog

**Parent:** [`_index.md`](../_index.md)

**Tech Spec:** skipped

## Why

현재 한 `.vmsong` 파일이 여러 악기 트랙을 동시에 담는다. 같은 곡의 piano-easy와 drum-hard를 독립적으로 작성·검증하기 어렵고, 난이도·악기별 수정 시 파일 충돌이 빈번하다. 곡명·악기·난이도를 각각 분리해 작성하고 카탈로그가 같은 곡명 파일을 묶어 보여주면 작성 단위가 명확해진다.

## What

`StreamingAssets/Songs/` 폴더의 `.vmsong` 파일은 `<songname>-<instrument>-<difficulty>.vmsong` 형식으로 명명된다. 파일명의 마지막 두 하이픈 부분이 `<instrument>-<difficulty>`로 파싱되고, 앞부분은 모두 `<songname>`(하이픈 포함 가능). 카탈로그는 같은 `<songname>` 파일들을 하나의 곡 엔트리로 묶고 그 곡에 존재하는 (instrument, difficulty) 조합을 노출한다.

- 같은 `<songname>`이면 곡 목록에 한 번만 표시.
- 곡 엔트리는 그 곡에 존재하는 instrument 키 집합 + 각 instrument별 존재하는 difficulty 집합을 노출.
- 플레이어가 잡은 악기 키와 일치하는 파일이 한 개도 없는 곡은 비활성.
- 기존 단일 파일 다중-트랙 포맷은 지원하지 않는다.

## Behavior

- **Given** `Songs/`에 `test_song-piano-easy.vmsong`, `test_song-drum-easy.vmsong`, `test_song2-piano-easy.vmsong` 존재
  **When** 카탈로그 초기화
  **Then** 곡 목록에 `test_song`과 `test_song2` 두 항목 등록.

- **Given** 어떤 곡에 `piano-easy` 파일만 있고 `piano-hard` 파일은 없음
  **When** 그 곡 선택 후 난이도 컨트롤을 봄
  **Then** `easy` 난이도 버튼만 노출, `hard` 미노출.

- **Given** `cool-song-piano-easy.vmsong`(songname에 하이픈 포함)
  **When** 카탈로그 초기화
  **Then** songname=`cool-song`, instrument=`piano`, difficulty=`easy`로 파싱.

- **Given** 잡은 악기가 piano인데 어떤 곡에 piano 파일이 전혀 없음
  **When** 곡 목록을 봄
  **Then** 그 곡은 비활성 상태로 표시.

## Out of Scope

- 기존 단일 파일 다중-트랙 포맷 (`test.vmsong` 형식) — 제거·마이그레이션.
- 인스트루먼트 토글·템포 조절 — `07-session-options-instrument-toggle-tempo`.
- 핫리로드(파일 추가 감지) — 재시작 시에만 갱신.
- `.vmsong` 내부 포맷 변경 — 파일명만 다루며 본문 파싱은 기존 `VmSongParser` 위임.

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-18 | Multi-File Song Catalog — 파일명 파싱·그루핑·인터페이스 확장 | Done | [`2026-05-18-linksky0311-multi-file-song-catalog.md`](../plans/2026-05-18-linksky0311-multi-file-song-catalog.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
