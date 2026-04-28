---
description: 기존 spec의 Open Questions 항목을 사용자와 함께 닫고, 결정된 사항을 spec 본문에 반영한다. docs/specs/ 외부는 절대 수정하지 않는다.
argument-hint: "<spec 파일 경로>"
allowed-tools: Read, Glob, Grep, Edit, AskUserQuestion
---

# /spec-resolve — Spec Open Questions 닫기 워크플로우

목적: spec 작성 단계에서 미결로 남겨둔 `Open Questions` 항목을 사용자와 함께 결정하고, 결정된 사항을 spec 본문(What / Behavior / Out of Scope)으로 승격시킨다. 코드는 읽기만 하고 수정하지 않는다.

## 절대 규칙

1. **수정 허용 경로는 `docs/specs/**`뿐.** 그 외는 읽기 전용.
2. **본문에 구현 디테일 금지.** spec 작성과 동일한 anti-pattern: 구체 함수명·클래스명·파일 경로·자료구조·알고리즘은 plan으로 미룬다.
3. **사용자 승인 없이 본문을 수정하지 않는다.** 결정 → 어디에 어떻게 반영할지 제시 → 승인 → Edit 순서.
4. **Edit는 최소 범위.** 본문 전체 재작성 금지. 닫힌 Open Question 한 줄과 본문에 한두 줄 추가/수정만.
5. **무한 라운드 금지.** 한 번의 호출에서 최대 3라운드. 남은 항목은 Open Questions로 유지한다.
6. **모르겠으면 닫지 않는다.** 결정이 부족하면 닫지 말고 Open Question에 남긴다(혹은 더 구체화된 형태로 교체).

## 입력

- `$ARGUMENTS` — 대상 spec 파일 경로 (예: `docs/specs/rhythm-game/specs/chart-format.md`). 비어 있으면 사용자에게 묻는다.

## 워크플로우

### 1. 컨텍스트 적재 (read-only)

순서대로 읽는다.

1. 대상 spec 파일.
2. 같은 feature의 `_index.md` (parent root-spec).
3. 같은 feature의 sibling sub-specs 전부. (이번 결정이 다른 sub-spec과 충돌하는지 확인용)
4. 필요시 관련 코드/문서. 수정 금지.

### 2. 질문 라운드 (최대 3회)

각 라운드마다 `AskUserQuestion`으로 항목 1~4개를 묶어 묻는다.

질문 작성 가이드:
- 한 항목 = 한 질문. 결정의 분기를 옵션으로 제시.
- 사용자가 답하기 어려운 추상적 항목은 더 구체적인 후보 2~3개로 쪼개 제시.
- 한 항목이 sibling sub-spec과 얽혀 있으면 그 사실을 질문 설명에 명시.
- 항목이 5개를 넘으면 관련 항목끼리 묶어 라운드를 나눈다 (라운드당 1~4개).

라운드 사이마다 받은 답을 짧게 요약해 사용자가 보강할 여지를 준다.

### 3. 반영 위치 제안

각 결정 항목에 대해 **어디에 어떻게 반영할지** 사용자에게 묶어 제시한다.

가능한 반영 위치:
- **What** — 시스템이 무엇을 하는가에 대한 결정.
- **Behavior** — 관찰 가능한 동작이 추가/명확화되는 결정.
- **Out of Scope** — 제외 결정.
- **Open Questions에 더 구체화된 형태로 갱신** — 부분 결정만 났을 때.
- **Spec 본문에 반영하지 않음** — plan-level 디테일이므로 plan으로 미룸. 이 경우 Open Question은 닫되 그 사유를 한 줄 메모.

각 항목별 반영 위치와 추가/수정될 문장 초안을 한 번에 제시하고 명시 승인을 받는다.

### 4. Edit 적용

승인된 사항만 반영한다.

- 본문 추가/수정은 **최소 범위 Edit**로. 기존 문장 톤과 맞춘다.
- 닫힌 Open Question 항목은 해당 줄을 삭제하거나 구체화된 형태로 교체한다.
- spec 작성 anti-pattern을 다시 점검: 함수명·파일 경로·알고리즘이 본문에 새어 들어가지 않았는지.

### 5. 다른 sub-spec 영향 확인

이번 결정이 sibling sub-spec의 What/Behavior와 충돌하거나 갱신이 필요한지 확인한다.

- 영향이 있으면 어느 sub-spec의 어느 부분이 갱신 대상인지 사용자에게 보고한다.
- **자동으로 갱신하지 않는다.** 사용자가 명시 승인하면 같은 워크플로우를 그 sub-spec에도 적용한다 (재귀적으로 한 번 더).

### 6. 마무리

- 닫힌 항목 / 갱신된 항목 / 남은 항목을 짧게 요약.
- 다음 권장 액션 (남은 Open Questions를 이어서 닫을지, `/plan-new`로 넘어갈지) 안내.

## 출력 형식

진행 메시지는 한국어, 짧게. 질문은 `AskUserQuestion`으로만 묶는다.
