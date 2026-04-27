<!--
구현 plan 템플릿. 이 파일은 docs/specs/<feature>/plans/<YYYY-MM-DD>-<author>-<slug>.md로 복사해서 사용한다.

작성 지침:
- 파일명 규칙: `YYYY-MM-DD-<author>-<slug>.md` (`/plan-new`가 자동 발급. 작성자는 `git config user.name` 슬러그).
- self-contained: 이 plan 파일과 Linked Spec만 읽고도 다른 세션에서 작업을 시작할 수 있어야 한다.
- "한 plan = 한 세션 분량" 기준. 너무 크면 쪼개고, 너무 작으면 합친다.
- Approach에는 어떻게 구현할지 적는다. 의사코드/파일 경로/주요 함수 단위까지 OK. (Spec과의 차이는 여기서 발생)
- Acceptance Criteria는 검증 가능한 항목으로 적는다. "잘 동작한다" 같은 모호한 표현 금지.
-->

# <Plan Title>

**Linked Spec:** [`<sub-spec-name>.md`](../specs/<sub-spec-name>.md)
**Status:** `Ready`

<!-- Status 값: Ready / In Progress / Done -->

## Goal

<이 plan을 실행하면 무엇이 달성되는가. 한두 문장.>

## Context

<왜 이 plan이 지금 필요한가. Linked Spec의 어떤 What을 어떻게 풀어내는가. 다른 세션에서 이 파일만 읽고도 작업을 시작할 수 있도록 필요한 배경(현재 코드 상태, 제약, 결정 근거)을 충분히 적는다.>

## Approach

<어떻게 구현할 것인가. 단계별로.>

1. <단계 1 — 무엇을, 어디에, 왜>
2. <단계 2>
3. <단계 3>

## Deliverables

- `<생성/수정될 파일 경로 1>` — <역할 한 줄>
- `<생성/수정될 파일 경로 2>` — <역할 한 줄>

## Acceptance Criteria

- [ ] <검증 가능한 항목 1>
- [ ] <검증 가능한 항목 2>
- [ ] <검증 가능한 항목 3>

## Out of Scope

- <이 plan이 다루지 않는 것 — 다른 plan으로 미루는 항목>

## Notes

<선택. 작업 중 발견한 사항, 의사결정 메모, 후속 plan 후보 등.>
