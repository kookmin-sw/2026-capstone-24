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
| NNN | <Plan Title> | Done | [NNN-<kebab>.md](…) |
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
