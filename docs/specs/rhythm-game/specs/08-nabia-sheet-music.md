# 나비야 악보 제작 & instrumentKey 버그 수정

**Parent:** [`_index.md`](../_index.md)

**Tech Spec:** skipped

## Why

실제로 연주 연습할 수 있는 곡이 없어서 리듬게임 모드의 핵심 기능을 온전히 체험하기 어렵다.
테스트 곡(`test_song`)은 파서 검증용 데이터로 음악적 완결성이 없어 사용자가 즐기기 어렵다.
한국 동요 '나비야'를 피아노 3난이도·드럼 3난이도로 제작해 실제 연주 연습 경험을 제공한다.
동시에 기존 `test_song-drum-easy.vmsong`의 파일명 instrument 키(`drum`)와 파일 내부 `instrument`
값(`DrumKit`)이 불일치해 드럼 반주 토글 OFF가 실제로 적용되지 않는 버그를 수정한다.

## What

- `StreamingAssets/Songs/`에 6개 VMSong 파일 추가:
  `nabia-piano-easy`, `nabia-piano-normal`, `nabia-piano-hard`,
  `nabia-drum-easy`, `nabia-drum-normal`, `nabia-drum-hard` (확장자 `.vmsong`)
- 각 파일 내부 `instrument` 값은 파일명 instrument 키와 동일 (`piano` 또는 `drum`)
- 곡 설정: C장조 4/4박자 120BPM, 8마디
  - 1마디: C코드 4박 | 멜로디 G4(1박)·E4(1박)·E4(2박)
  - 2마디: G7코드 4박 | 멜로디 F4(1박)·D4(1박)·D4(2박)
  - 3마디: C코드 4박 | 멜로디 C4·D4·E4·F4 (각 1박)
  - 4마디: C코드(2박)+G7코드(2박) | 멜로디 G4·G4(각 1박)·G4(2박)
  - 5마디: 1마디와 동일
  - 6마디: 2마디와 동일
  - 7마디: C코드 4박 | 멜로디 C4·E4·G4·G4 (각 1박)
  - 8마디: C코드 4박 | 멜로디 E4(1박)·D4(1박)·C4(2박)
- 피아노 Easy: 코드 근음 4분음표(전 박) + 멜로디
- 피아노 Normal: 코드 근음+5도 4분음표 + 멜로디
  (C코드 = C4+G4, G7코드 = G4+D5)
- 피아노 Hard: 8분음표 아르페지오(3음 반복 상행) + 멜로디
- 드럼 Easy: 킥(beat 1·3)·스네어(beat 2·4) 교대, 동시 타격 최대 1개
- 드럼 Normal: 8분음표 하이햇 + 비트마다 킥 또는 스네어 동시, 최대 2개 동시
- 드럼 Hard: 16분음표 하이햇 + 킥·스네어 + 마지막 2마디 톰 필인, 최대 2개 동시
- 기존 `test_song-drum-easy.vmsong` 내부 `instrument=DrumKit` → `instrument=drum` 수정
- 기존 `VmSongParserTests.cs`에 6개 신규 파일 파싱 성공 테스트 케이스 추가

## Behavior

- **Given** `Songs/` 폴더에 nabia 6개 파일 배치
  **When** 카탈로그 초기화
  **Then** 곡 목록에 `nabia` 항목 1개 등록, `piano`·`drum` 두 악기 지원, Easy/Normal/Hard 난이도 노출

- **Given** `nabia-piano-easy.vmsong`을 `VmSongParser.Parse()` 입력
  **When** 파싱
  **Then** 오류 없이 성공, 트랙 1개, 노트 수 > 0

- **Given** `test_song-drum-easy.vmsong`을 파싱
  **When** `channelMap.entries[0].instrumentKey` 확인
  **Then** 값이 `"drum"` (대소문자 무관)

- **Given** `nabia-drum-hard.vmsong`을 파싱
  **When** 모든 tick별 동시 노트 수 확인
  **Then** 어느 tick에서도 동시 노트 수 ≤ 2

## Out of Scope

- 나비야 외 추가 곡 제작
- 피아노·드럼 외 다른 악기 차트
- 악보 음악적 편곡 품질 검수
- `test_song` 관련 다른 파일 변경

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-18 | 나비야 6개 VMSong + drumkit instrumentKey 정합화 + 파서 테스트 6건 추가 | Done | [`2026-05-18-linksky0311-nabia-sheet-music.md`](../plans/2026-05-18-linksky0311-nabia-sheet-music.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
