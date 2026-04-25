<!--
하위 spec 템플릿. 이 파일은 docs/specs/<feature>/specs/<sub-name>.md로 복사해서 사용한다.

작성 지침:
- 이 sub-spec은 "독립적으로 What이 성립하는 단위"여야 한다. 다른 sub-spec에 강하게 의존하면 합치거나 재분해.
- Behavior에는 사용자/시스템 관점에서 관찰 가능한 동작만 적는다. 구현 방법(클래스, 알고리즘) 금지.
- Implementation Plans 테이블은 /plan-new 가 plan을 추가할 때 자동으로 갱신한다.
- 구현 디테일이 떠올라도 본문에 적지 말고, 해당 plan 파일로 옮겨 적는다.
-->

# <Sub-Spec Name>

**Parent:** [`_index.md`](../_index.md)

## Why

<이 sub-spec이 parent 안에서 왜 필요한가. parent의 어떤 What을 책임지는가.>

## What

<이 sub-spec이 책임지는 범위. 한 단락으로 시작해서 필요하면 불릿으로 보강.>

## Behavior

<관찰 가능한 동작을 시나리오/상태/입출력 단위로 기술.>

- **Given** <전제>
  **When** <트리거>
  **Then** <기대 결과>

## Out of Scope

- <이 sub-spec이 다루지 않는 것 — 혼동되기 쉬운 인접 영역을 명확히>

## Implementation Plans

| 번호 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 번호는 전역 일련번호.

## Open Questions

- [ ] <아직 결정되지 않은 질문>
