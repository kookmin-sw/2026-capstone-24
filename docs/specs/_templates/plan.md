<!--
구현 plan 템플릿. 이 파일은 docs/specs/<feature>/plans/<YYYY-MM-DD>-<author>-<slug>.md로 복사해서 사용한다.

작성 지침:
- 파일명 규칙: `YYYY-MM-DD-<author>-<slug>.md` (`/plan-new`가 자동 발급. 작성자는 `git config user.name` 슬러그).
- self-contained: 이 plan 파일과 Linked Spec만 읽고도 다른 세션에서 작업을 시작할 수 있어야 한다.
- "한 plan = 한 세션 분량" 기준. 너무 크면 쪼개고, 너무 작으면 합친다.
- Approach에는 어떻게 구현할지 적는다. 의사코드/파일 경로/주요 함수 단위까지 OK. (Spec과의 차이는 여기서 발생)
- Acceptance Criteria는 검증 가능한 항목으로 적는다. "잘 동작한다" 같은 모호한 표현 금지.
- **Acceptance Criteria 각 항목은 라벨을 반드시 붙인다.** `[auto-hard]` / `[auto-soft]` / `[manual-hard]` 중 하나. 라벨 미부여 항목이 하나라도 있으면 `/spec-implement`가 plan 실행을 거부한다.
-->

# <Plan Title>

**Linked Spec:** [`<sub-spec-name>.md`](../specs/<sub-spec-name>.md)
**Status:** `Ready`

<!--
선택 헤더: `**Caused By:** [<선행 plan 파일>](./<선행 plan 파일>)`
검증 실패에서 파생된 plan에만 둔다. `/plan-new --from-failure`가 자동 부여하므로
직접 작성 시에만 수동 추가. 위치: `Linked Spec`과 `Status` 사이.
-->

<!-- Status 값: Ready / In Progress / Done -->

## Goal

<이 plan을 실행하면 무엇이 달성되는가. 한두 문장.>

## Context

<왜 이 plan이 지금 필요한가. Linked Spec의 어떤 What을 어떻게 풀어내는가. 다른 세션에서 이 파일만 읽고도 작업을 시작할 수 있도록 필요한 배경(현재 코드 상태, 제약, 결정 근거)을 충분히 적는다.>

## Verified Structural Assumptions

<!--
plan의 Approach가 가정하는 Unity 자산 구조(GameObject 경로·컴포넌트 부착 위치·prefab vs
scene instance 구분·ScriptableObject/material/animation 자산 값 등)를 한 번에 점검한 결과를 적는다.

작성 규칙:
- 최소 1개 항목, 최대 ~6개 권장.
- 각 항목 끝에 출처를 명시한다:
  - `unity-scene-reader 보고 (YYYY-MM-DD)` — `/plan-new` step 1.5에서 sub-agent로 검증한 경우. (권장)
  - `MCP 미사용 — 가정` — Unity MCP 사용 불가 fallback 경로일 때만. (사용자 동의 박제)
- 검증한 GameObject·컴포넌트·자산 경로를 plan 본문 안 어디에선가 다시 인용하므로 정확한 표기를 쓴다.
- Unity 자산 의존이 0인 plan(순수 C# 로직 변경, 문서 정리 등)은 본문 한 줄로 비워둔다:
  `_해당 없음 — 순수 로직 변경_`

이 섹션은 모든 plan에 필수다. 비어 있어도 fallback 한 줄을 둬야 `/spec-implement`가 plan 가정의
검증 출처를 추적할 수 있다.
-->

- <검증 항목 1 — 예: "VR Player.prefab의 controller-tracked transform은 `Camera Offset/Hands/Left/LeftControllerHandRoot`(TrackedPoseDriver 부착) — `Left` 자체는 정적 컨테이너"> — `unity-scene-reader 보고 (YYYY-MM-DD)`
- <검증 항목 2> — `unity-scene-reader 보고 (YYYY-MM-DD)`

## Approach

<어떻게 구현할 것인가. 단계별로.>

1. <단계 1 — 무엇을, 어디에, 왜>
2. <단계 2>
3. <단계 3>

## Deliverables

- `<생성/수정될 파일 경로 1>` — <역할 한 줄>
- `<생성/수정될 파일 경로 2>` — <역할 한 줄>

## Acceptance Criteria

<!--
각 항목은 반드시 다음 3종 라벨 중 하나를 인라인 코드로 붙인다. 라벨이 빠진 항목이 하나라도 있으면
`/spec-implement`가 plan 실행을 거부한다.

- `[auto-hard]` — 자동 테스트/스크립트로 검증. 실패 시 plan 중단.
- `[auto-soft]` — 자동 테스트/스크립트로 검증. 실패 시 노트에 기록하고 다음 plan으로 진행.
- `[manual-hard]` — 사용자가 직접 검증(헤드셋·Editor·VR 등). 실패 시 plan 중단.

라벨은 위 3종으로 한정한다. 사람이 직접 검증하는 항목은 항상 중단 사유로 처리한다.
-->

- [ ] `[auto-hard]` <자동 검증 가능, 실패시 중단되어야 하는 항목>
- [ ] `[auto-soft]` <자동 검증 가능, 실패해도 진행 가능한 항목>
- [ ] `[manual-hard]` <사용자 직접 검증 항목>

## Out of Scope

- <이 plan이 다루지 않는 것 — 다른 plan으로 미루는 항목>

## Notes

<선택. 작업 중 발견한 사항, 의사결정 메모, 후속 plan 후보 등.>

## Handoff

<선택. `/spec-implement`가 plan 완료 시 자동 갱신한다. 다음 plan이 알아야 할 공개 API 시그니처·자산 경로·결정 사항 5~15줄. 빈 채로 둬도 OK.>
