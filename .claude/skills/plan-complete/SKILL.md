---
name: plan-complete
description: docs/specs/<feature>/plans/ 아래 plan 파일의 구현이 끝났을 때 사용. plan Status를 Done으로 갱신하고 docs/specs/README.md 상태 보드, 해당 sub-spec의 Implementation Plans 표, parent _index.md의 Sub-Specs 표를 동기화한 뒤, 완료된 plan/sub-spec 파일을 docs/specs/_archive/ 아래로 이동한다. 사용자가 "plan 끝났어", "이 plan 구현 완료", "plan Done 처리", "아카이브 정리" 같은 요청을 할 때 트리거한다.
allowed-tools: Read, Edit, Write, Glob, Grep, Bash
---

# Plan 라이프사이클 규칙

Plan 구현을 완료하면 다음 네 곳을 반드시 갱신한다.

## 1. Plan 파일 상태 갱신

완료된 plan 파일의 Status 필드를 `Done`으로 변경한다.

```
**Status:** `Done`
```

## 2. docs/specs/README.md 상태 보드 갱신

해당 피처 행의 **Plans (Done/Total)** 카운트를 갱신한다.

- 피처 행이 없으면 새로 추가한다.
- Total은 현재 파일로 작성된 plan 수 기준 (미래 계획 예정 수 아님).
- Feature Status 판단 기준:

| 조건 | Status |
|---|---|
| Plan 없음, sub-spec만 Draft 상태 | `Draft` |
| 1개 이상 plan 완료, 아직 진행 중 | `Active` |
| 더 이상 추가할 plan 없이 전부 Done | `Done` |
| 작업 중단 결정 | `Abandoned` |

## 3. Sub-Spec Implementation Plans 표 상태 갱신

완료된 plan이 등록된 sub-spec 파일을 찾아
`Implementation Plans` 표에서 해당 plan 행의 Status 열을 `Done`으로 변경한다.

```
| <YYYY-MM-DD> | <Plan Title> | Done | [<filename>.md](…) |
```

## 4. `_index.md` Sub-Specs 표 상태 갱신

완료된 plan이 속한 sub-spec의 상위 `_index.md`를 열어 Sub-Specs 표에서 해당 sub-spec 행의 상태를 갱신한다.

판단 기준:

| 조건 | Status |
|---|---|
| 해당 sub-spec의 plan이 하나도 없거나 전부 미착수 | `Draft` |
| 1개 이상 plan 완료, 추가 plan이 남아 있거나 Open Questions 미결 | `Active` |
| 모든 plan이 Done이고 Open Questions가 없음 | `Done` |
| 작업 중단 결정 | `Abandoned` |

## 5. 완료된 파일 아카이브

Plan 파일은 `Status: Done` 처리 직후 `docs/specs/_archive/<feature>/plans/`로 이동하고, 해당 sub-spec의 `Implementation Plans` 표 링크를 새 경로로 갱신한다.

이동 후, 같은 sub-spec(또는 cross-feature) 안에 헤더 `**Caused By:** <이전 plan 경로>`로 이 plan을 가리키는 후속 plan이 있으면 그 헤더 링크를 새 아카이브 경로로 Edit 갱신한다 (`./<filename>` → `../../_archive/<feature>/plans/<filename>` 형태).

해당 sub-spec의 모든 plan이 Done이고 Open Questions가 없어 Status가 `Done`이면, sub-spec 파일도 `docs/specs/_archive/<feature>/specs/`로 이동하고 `_index.md`의 `Sub-Specs` 표 링크를 새 경로로 갱신한다.

전체 sub-spec이 Done이고 Open Questions가 없으면, feature 폴더 전체를 `docs/specs/_archive/<feature>/` 아래로 이동한다.

> Caused By 백링크 갱신 정책 단일 진실원: [`docs/specs/README.md`](../../../docs/specs/README.md) "검증 실패 시 후속 plan 시드" 섹션.