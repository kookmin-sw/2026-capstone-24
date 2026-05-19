# base-tone-asset-convention

**Sub-Spec:** [`03-blow-and-pitch-sound.md`](../specs/03-blow-and-pitch-sound.md)
**From Tech Spec:** [`tech-specs/03-blow-and-pitch-sound.md`](../tech-specs/03-blow-and-pitch-sound.md) §Open Tech Decisions #2
**Status:** `Accepted`
**Date:** 2026-05-17

## Context

사용자는 트럼본 베이스 톤 wav 한 개를 외부에서 제공하기로 약속했다 (인터뷰 라운드 2). 본 결정은 그 wav 를 프로젝트 어디에 어떤 이름으로 두고, 트럼본 컴포넌트가 어떤 키로 해당 클립을 로드할지 결정한다. 컨벤션이 명확해야 사용자가 다른 음정의 wav 로 바꾸고 싶을 때 매핑이 자동으로 따라간다.

## Options Considered

- **`Assets/Instruments/Trombone/Sound/<NoteName>.wav` 파일명=노트명 컨벤션** — Piano (`Assets/Instruments/Piano/Sound/A0.wav~Ds7.wav`) 와 동일 규약. 파일명만 보면 기준 음정 명확. 사용자가 다른 음정으로 바꾸려면 파일명을 바꾸면 됨. `spec_what_coverage`: W4(베이스 톤 로드) 만족.
- **컴포넌트 SerializedField `baseTone: AudioClip` + `baseToneMidiNote: int`** — Inspector 에서 드래그 앤 드롭. 파일명 무관. 단 기준 음정은 별도 SerializedField 로 명시 필요. `spec_what_coverage`: W4 만족.
- **InstrumentLaneConfig SO 에 베이스 톤 클립 등록** — Piano 가 쓰는 SO 와 동일 구조. 단일 클립용으로는 overkill, 키 하나만 채워지는 SO 가 됨. `spec_what_coverage`: W4 만족.

## Decision

**`Assets/Instruments/Trombone/Sound/C4.wav` 파일명=노트명 컨벤션** — 사용자가 권장 옵션 선택. Piano 와 동일 규약이라 도메인 일관성 + 파일명만 보면 기준 음정 자명. 사용자가 다른 음정 wav 를 주면 파일명만 바꾸면 매핑이 자동으로 따라간다.

## Spec What Coverage

| What | 옵션 A (선택) | 옵션 B | 옵션 C |
|---|---|---|---|
| W4: 사용자가 단일 베이스 톤 wav 제공해 음원 로딩 | 만족 | 만족 | 만족 |

## Consequences

- 트럼본 컴포넌트의 AudioBank 는 `Trombone/Sound/` 디렉토리에서 파일명 키로 클립을 로드한다 (Piano 동일 진입).
- 사용자가 추후 다른 음정의 베이스 톤(예: A3.wav) 으로 바꾸면 컴포넌트의 `baseToneMidiNote` (또는 파일명 파싱) 가 새 음정을 기준으로 잡아야 한다 — plan 단계에서 파일명 파싱 vs SerializedField 둘 중 1개 박제 (본 ARD 의 사정권 외, plan 결정).
- 파일이 비어 있거나 누락되면 Trombone 발음이 silent → 인터뷰 정책상 진단/경고 로깅은 사용자 명시 요청 없으면 추가하지 않는다.
