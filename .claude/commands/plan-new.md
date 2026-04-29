---
description: 기존 spec을 받아 self-contained한 구현 plan을 작성한다. 날짜·작성자·slug 기반 파일명을 자동 부여하고 spec과 양방향 링크를 맺는다. 검증 실패에서 파생된 plan은 `--from-failure <failed-plan-path>` 모드로 작성한다.
argument-hint: "<spec 파일 경로> | --from-failure <failed-plan-path>"
allowed-tools: Read, Glob, Grep, Write, Edit, AskUserQuestion, Bash
---

# /plan-new — Plan 작성 워크플로우

목적: 주어진 spec(보통 sub-spec)을 읽어 **How를 담은 self-contained plan**을 작성한다. 한 plan = 한 세션 분량. 코드는 읽기만 하고 수정하지 않는다.

## 절대 규칙

1. **수정 허용 경로는 `docs/specs/**`뿐.** 그 외는 읽기 전용. 단, `Bash`는 `git config user.name` 호출 한 가지에만 사용한다.
2. **Plan은 self-contained.** 다른 plan이나 이전 세션 컨텍스트를 가정하지 않는다. 필요한 배경은 `Context` 섹션에 적어 넣는다.
3. **사용자 승인 없이 plan 파일을 만들지 않는다.** 분할안 제안 → 승인 → 작성 순서를 지킨다.
4. **파일명 충돌 시 `-2`, `-3` 접미사로 자동 디스앰비.** 그래도 해소되지 않으면 사용자에게 보고하고 멈춘다.

> 검증 실패에서 파생된 plan은 `--from-failure <failed-plan-path>` 모드로 작성한다. 정책 단일 진실원: [`docs/specs/README.md`](../../docs/specs/README.md) "검증 실패 시 후속 plan 시드" 섹션.

## 입력

- `$ARGUMENTS` — 다음 셋 중 하나.
  - 빈 문자열 — lazy 모드(단계 0).
  - `<spec-path>` — 일반 모드. 그 spec에 대한 plan을 새로 작성.
  - `--from-failure <failed-plan-path>` — **실패 파생 모드**. 다음 자동 절차가 활성화된다:
    - step 0.5 "실패 컨텍스트 적재" 실행 (Linked Spec 자동 상속, 실패 acceptance 항목 자동 추출).
    - step 4의 헤더에 `**Caused By:**` 라인 자동 부여, Context에 표준 인용 블록 자동 삽입, AC 마지막에 "선행 plan 재검증" manual-hard 자동 추가.
    - step 5에서 선행 plan의 `## Notes`에 후속 plan 추가 사실 한 줄 append.

## 워크플로우

### 0. 인수가 없을 때: 피처 → sub-spec 단계적 선택

`$ARGUMENTS`가 비어 있으면 **lazy 모드**로 진행한다. 모든 `_index.md`/sub-spec을 선제 적재하지 않는다.

1. `docs/specs/README.md` 상태 보드만 읽어 `Active`/`Draft` 피처명 목록을 추출한다. (이 단계에서 `_index.md`나 sub-spec 본문은 읽지 않는다.)
2. 사용자에게 **어느 피처의 plan을 작성할지** 묻는다 (`AskUserQuestion` 권장, 자유 텍스트 응답도 허용). 사용자가 sub-spec 경로까지 직접 짚어주면 즉시 step 1로 진입한다.
3. 사용자가 피처를 지정하면 그제서야 해당 피처의 `_index.md`를 읽고, 거기 등록된 sub-spec 파일들의 `Implementation Plans` 표만 가볍게 훑어 다음 형식으로 정리해 출력한다:

   **[feature] 현황**
   | Sub-Spec | Plan 상태 |
   |---|---|
   | chart-format | ✅ 완료 (2/2 Done) |
   | timing-clock | ⏳ plan 미작성 |
   | … | … |

   판단 기준:
   - 표가 없거나 `_아직 없음_` 행만 있으면 → plan 미작성
   - 행이 있고 Status가 모두 `Done`이면 → 완료
   - 행이 있고 하나라도 `Ready` / `In Progress`이면 → 진행 중

   **추천 다음 구현:**
   > `<spec-name>` — 한 줄 이유 (예: "런타임 클락. Accompaniment·Judgment 모두 이 컴포넌트에 의존하므로 선행 필수.")

4. 진행할 sub-spec을 묻는다. `AskUserQuestion` 권장(추천 항목을 첫 옵션으로, 레이블 끝에 "(추천)"). 자유 응답도 허용.

### 0.5. 실패 컨텍스트 적재 (`--from-failure` 모드 전용)

`$ARGUMENTS`가 `--from-failure <failed-plan-path>`로 들어왔을 때만 실행한다. 다른 모드에서는 이 단계를 건너뛴다.

1. `<failed-plan-path>` 파일을 Read한다. 존재하지 않으면 사용자에게 보고하고 멈춘다.
2. 헤더 `**Linked Spec:**` 라인에서 spec 경로를 추출한다 — 새 plan의 Linked Spec으로 **자동 상속**한다. step 1의 spec 컨텍스트 적재는 이 경로로 진행하며, 사용자에게 spec을 다시 묻지 않는다.
3. 같은 sub-spec 폴더(`docs/specs/<feature>/`)의 `.orchestrator-state.json`을 Read한다.
   - `per_plan_history[]`에서 `plan_path == <failed-plan-path>`인 항목을 찾는다.
   - 그 항목의 `acceptance_results[]` 중 `status: fail` 또는 `status: skipped` 항목을 모두 추출 — `failed_acs` 리스트로 보관한다 (각 항목은 `{label, criteria, evidence}`).
4. 상태 파일이 없거나 매칭 실패 시 사용자에게 자유 텍스트로 "실패한 acceptance 항목과 evidence를 알려달라"고 묻고, 답을 그대로 `failed_acs`로 사용한다. 이 폴백은 `.orchestrator-state.json`이 어떤 이유로 사라졌어도 흐름을 막지 않기 위한 경로다.
5. **lazy 모드의 step 0은 건너뛴다.** 피처/sub-spec은 이미 Linked Spec에서 결정됨.

### 1. Spec 컨텍스트 적재

순서대로 읽는다:

1. 대상 spec 파일.
2. 같은 feature의 `_index.md` (parent root-spec).
3. 같은 sub-spec의 `Implementation Plans` 표에 이미 등록된 plan들 (있으면 모두 읽어 중복/연속성 파악).
4. `_templates/plan.md` (작성 형식 확인).

필요하면 관련 코드/문서를 *읽기만* 한다. Step 0에서 이미 읽은 파일은 다시 읽지 않는다.

### 2. 분할 제안

`--from-failure` 모드일 때는 우선 다음 질문을 한 번 더 한다:

> 실패 항목 N건(`step 0.5`의 `failed_acs`)을 한 plan으로 풀 수 있는가, 여러 plan으로 쪼개야 하는가? 한 plan으로 풀어도 되는 경우(예: 단일 시스템의 튜닝)와 쪼개야 하는 경우(예: 신호 처리 + 콜라이더 자산 보강처럼 책임이 다른 작업이 섞임)를 구분해서 답해 주세요.

다음을 사용자에게 제시하고 **명시 승인을 받는다**:

- 이 spec을 **plan 1개**로 충분히 다룰 수 있는지, 아니면 **여러 plan으로 쪼개야 하는지**.
- 여러 개라면 각 plan의 제목, 한 줄 책임, 권장 실행 순서.
- 각 plan의 대략적 분량(파일 수/난이도) 추정.

판단 기준:
- 한 plan은 "한 세션에 끝낼 수 있는" 크기.
- 자연스러운 의존성(예: 데이터 → 로직 → UI)이 있으면 그 경계로 자른다.
- 검증 가능한 Acceptance Criteria가 plan 단위로 나오는지 확인.

### 3. 파일명 발급

파일명 규칙(`<YYYY-MM-DD>-<author>-<slug>.md`)의 정의는 [`docs/specs/README.md`](../../docs/specs/README.md)의 "Plan 파일명" 섹션을 단일 진실원으로 한다. 실행 알고리즘은 다음과 같다.

1. **작성자 슬러그** 산출 — `git config user.name`을 `Bash`로 한 번만 호출(read-only)한 뒤 README 규칙에 따라 정규화. 빈 문자열이면 사용자에게 보고하고 멈춘다.
2. **날짜** 산출 — `YYYY-MM-DD` (시스템 로컬 기준).
3. **slug** 산출 — 사용자가 승인한 plan 제목을 kebab-case로 변환. 한국어 제목이면 영문 slug 후보를 사용자에게 제안하고 확인받는다 (자동 변환 신뢰 금지).
4. **충돌 검사** — 대상 `docs/specs/<feature>/plans/` 안에 같은 이름 파일이 있는지 `Glob`으로 확인. 충돌 시 slug 끝에 `-2`, `-3`, … 접미사를 붙여 첫 번째 비어 있는 후보를 사용한다.
5. 한 번의 `/plan-new` 실행 안에서 여러 plan을 만들 때는 같은 날짜·작성자를 공유하므로 slug만 각각 다르면 된다.

### 4. 파일 작성

승인 후에만 진행:

- 경로: `docs/specs/<feature>/plans/<date>-<author>-<slug>.md`.
- `_templates/plan.md`를 베이스로 채운다.
- `Linked Spec`은 대상 spec 파일을 상대경로로 정확히 가리킨다 (`../specs/<NN>-<sub>.md` 형태).
- `Status`는 `Ready`로 시작.
- `Context` 섹션은 다른 세션에서 이 파일만 읽고도 작업을 시작할 수 있을 만큼 충분한 배경을 담는다 (Linked Spec 핵심 요약, 현재 코드 상태, 제약, 의사결정 근거).
- `Approach`는 단계별로. `Deliverables`는 생성/수정될 파일 경로 목록.
- `Acceptance Criteria`는 검증 가능한 항목만 적는다 (모호한 표현 금지).
- `Acceptance Criteria` 각 항목은 `[auto-hard]` / `[auto-soft]` / `[manual-hard]` 중 하나의 라벨을 인라인 코드로 부여한다 (`- [ ] \`[auto-hard]\` <항목>` 형태). 라벨이 빠진 항목이 하나라도 있으면 `/spec-implement`가 plan 실행을 거부하므로, 본문 작성 직후 라벨 부여 여부를 한 번 검증하고 누락분은 사용자에게 분류를 묻는다. 라벨은 위 3종으로만 한정 — 사람이 직접 검증하는 항목은 항상 중단 사유로 처리한다.

#### `--from-failure` 모드의 자동 채움

본 모드에서 step 4를 실행할 때 추가로 다음을 자동 부여한다 (사용자가 손으로 적은 본문에 prepend/append).

- **헤더 `**Caused By:**` 라인 부착.** `**Linked Spec:**`과 `**Status:**` 사이에 한 줄을 추가한다:
  ```
  **Caused By:** [`<failed-plan-filename>`](./<failed-plan-filename>)
  ```
  값은 `<failed-plan-path>`의 파일명 부분만 — 같은 `plans/` 폴더 안이므로 `./<filename>` 상대경로.

- **`Context` 섹션 첫 단락에 표준 인용 블록 자동 삽입.** 사용자가 적는 Context 본문 **앞에** 다음 블록을 prepend한다:
  ```
  > **선행 plan 검증 실패에서 파생됨.** 선행: `<failed-plan-filename>`.
  > 실패한 Acceptance Criteria:
  > - `[<label>]` <criteria 원문> — <evidence 한 줄>
  > - `[<label>]` <criteria 원문> — <evidence 한 줄>
  >
  > 본 plan은 위 항목을 다시 통과 가능하게 만드는 부속 작업을 다룬다.
  ```
  `failed_acs` 리스트 길이만큼 항목을 반복한다. 사용자에게 "이 인용 블록 뒤에 추가 Context를 적어 self-contained로 만들라"고 안내한다.

- **`Acceptance Criteria` 마지막에 "선행 plan #N 재검증" 자동 부여.** 사용자가 작성한 AC 항목 검증을 끝낸 뒤, `failed_acs` 각 항목당 한 줄을 append한다:
  ```
  - [ ] `[manual-hard]` 선행 plan `<failed-plan-filename>`의 실패 AC ("<criteria 원문 앞 60자>") 가 이 plan 적용 후 재검증에서 통과한다.
  ```
  이 항목은 `/spec-implement`의 자동 reflect 로직이 매칭하는 키이므로 **criteria 문구를 임의로 바꾸지 않는다.** 매칭 키 정의는 [`docs/specs/README.md`](../../docs/specs/README.md) "재검증 AC 매칭 키" 박스를 단일 진실원으로 한다 — 자동 reflect substring은 ` 가 이 plan 적용 후 재검증에서 통과한다`이다.

### 5. 역링크 갱신

- 대상 sub-spec 파일의 `Implementation Plans` 표에 새 plan 행을 **Edit**로 추가한다.
  - 행 형식: `| <YYYY-MM-DD> | <Plan Title> | Ready | [<filename>.md](../plans/<filename>.md) |`
  - 기존 표가 `_아직 없음_` 행만 갖고 있다면 그 행을 새 행으로 대체한다.
- 여러 plan을 한 번에 만든 경우 모두 추가.

#### `--from-failure` 모드의 추가 역링크

위 sub-spec 표 갱신 외에 다음을 수행한다.

- **선행 plan의 `## Notes` 섹션에 한 줄 Edit append.** 형식:
  ```
  - <YYYY-MM-DD>: 검증 실패에서 파생된 후속 plan `<new-plan-filename>` 추가. 완료 후 본 plan의 `[<label>]` "<criteria 원문 앞 60자>" 항목 재검증 필요.
  ```
  실패 항목이 여러 개면 한 줄씩.
- 선행 plan에 `## Notes` 섹션이 없으면 `## Out of Scope` 다음, `## Handoff` 앞에 새로 만든다.

### 6. 마무리

- 작성된 plan 파일 경로 목록과 권장 실행 순서를 사용자에게 짧게 안내한다.
- **commit 권고.** 본 명령의 Write/Edit는 모두 `docs/specs/**` 안에 머무르므로 atomic 단위로 바로 commit하는 것이 자연스럽다. `/spec-implement`는 working tree가 clean해야 plan 실행을 시작하므로, 정리되지 않으면 다음 단계에서 막힌다. 사용자에게 "지금 `git-workflow` skill로 commit할까요?"를 한 번 묻는다. 동의하면 그대로 진행, 거절하면 변경 파일 목록만 다시 표시하고 종료. 본 명령은 직접 commit하지 않는다 — 사용자 동의 후 git-workflow skill에 위임만 한다. (`--from-failure` 모드에서는 선행 plan `## Notes` 갱신과 신규 plan 추가를 한 commit으로 묶는 것이 자연스럽다.)
- "각 plan을 실행하려면 plan 파일 경로를 새 세션의 Claude에게 전달하면 된다 (Linked Spec과 parent `_index.md`까지 자동으로 읽도록 `docs/specs/README.md` 'Plan 실행 시 읽기 순서'에 규칙이 적혀 있음)"는 안내를 한 번 추가한다.

## 출력 형식

진행 메시지는 한국어, 짧게. 질문은 기본적으로 `AskUserQuestion`을 쓴다. 단순 확인이나 자유 형식 응답이 자연스러우면 일반 텍스트 질문도 허용한다.
