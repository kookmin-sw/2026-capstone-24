---
description: root-spec(_index.md) 한 개를 받아 그 피처의 sub-spec 큐를 자동으로 진행한다. 각 sub-spec에 대해 plan-drafter → plan-quality-reviewer → /spec-implement 워크플로우를 직렬로 실행하며, plan 본문은 사용자에게 보여주지 않는다. 사용자 게이트는 manual-hard 검증과 destructive 가드만 남긴다. 기본 dry-run, --apply로 실제 실행.
argument-hint: "<root-spec 경로 (_index.md)> [--apply] [--max-cascade N]"
allowed-tools: Read, Glob, Grep, Bash, Edit, Write, AskUserQuestion, Skill, Task, mcp__UnityMCP__read_console
---

# /spec-build — 자동 파이프라인 orchestrator

목적: `/spec-interview`로 박제된 root-spec 한 개를 받아 그 피처의 sub-spec 큐를 자동으로 진행한다. 각 sub-spec에 대해 다음을 직렬로 실행한다.

1. plan 미작성이면 `plan-drafter` sub-agent로 lazy 작성.
2. 작성된 plan에 대해 `plan-quality-reviewer` sub-agent로 자동 점검.
3. 통과한 plan들을 `/spec-implement <sub-spec> --apply` 워크플로우로 구현.

플랜 본문·코드·라벨 부착·박제 출처는 사용자가 검토하지 않는다. 사용자 결정이 필요한 지점은 다음으로 한정된다.

- spec 박제 후 사용자가 `/spec-build` 트리거 (1회).
- manual-hard 검증 4택 (`pass`/`stop`/`stop-and-seed`/`skip-and-continue`) — `/spec-implement`의 manual-hard 분기 그대로.
- destructive 가드 (sub-spec/feature 폴더 `_archive/` 이동 시 1회) — `plan-complete`의 가드 그대로.
- plan-quality-reviewer가 `stop`을 반환했을 때.
- plan-drafter가 `unresolved`에 막힌 사유를 적어 반환했을 때.
- (선택) 큐 종료 시 deferred 목록 일괄 시드 여부.

## 절대 규칙

1. **플랜 본문을 사용자에게 보여주지 않는다.** 작성된 plan 경로 한 줄 보고만 한다. AC 목록·Approach 본문 등은 사용자 게이트 대상이 아니다.
2. **사용자 승인 없이 destructive·publishing 명령을 실행하지 않는다.** `/spec-implement`와 동일.
3. **메인 세션 사고를 sub-agent에 흘려보내지 않는다.** plan-drafter/plan-quality-reviewer에는 정의된 입력 항목만 전달한다.
4. **plan-drafter·plan-quality-reviewer는 사용자에게 질문하지 않는다.** 모든 사용자 결정은 메인 세션이 `AskUserQuestion`으로 처리.
5. **`/spec-implement` 워크플로우의 단일 진실원성 유지.** spec-build가 sub-spec 1개를 처리할 때 per-plan 루프·`.orchestrator-state.json`·atomic commit 메커니즘은 spec-implement 본문을 그대로 답습한다 — 메인 세션이 spec-implement의 step을 inline으로 실행. 코드 중복 회피 + 안전 가드(working tree clean, AC 라벨 강제, MCP 컴파일 체크) 자동 답습.
6. **`/spec-build` 자체는 git commit을 만들지 않는다.** atomic commit은 spec-implement step 3-9.5가 처리.

## 입력 해석

`$ARGUMENTS`의 첫 토큰은 root-spec 경로(`_index.md`). 그 외 패턴은 거부하고 `/spec-implement`로 안내.

| 패턴 | 동작 |
|---|---|
| `docs/specs/<feature>/_index.md` | 정상 진입. sub-spec 큐 생성 후 진행. |
| `docs/specs/<feature>/specs/<NN>-<sub>.md` | "**`/spec-implement`를 사용하라**"고 안내 후 종료. |
| `docs/specs/<feature>/plans/<filename>.md` | 동일하게 `/spec-implement`로 안내 후 종료. |
| 그 외 | 형식 오류 보고 후 종료. |

`--apply`는 두 번째 토큰. `--max-cascade N`은 세 번째 토큰(생략 시 default 2). 분기 직후 한 줄 출력: `mode=feature, feature=<name>, sub-specs=<N미완료>/<M전체>, max-cascade=<N>`.

## 워크플로우

### 1. Pre-flight (큐 시작 직전 1회)

다음 항목을 순서대로. 하나라도 실패하면 무엇이 막혔는지 보고하고 멈춘다.

1. **Working tree clean** — `git status --porcelain` 출력이 비어 있는지. 비어 있지 않으면 멈춘다 (Caused By 빌드업 분기는 본 명령에서 직접 받지 않으며, 사용자가 `/spec-implement`로 빌드업 plan을 진행 중이라면 그쪽으로 안내).
2. **root-spec 파일 존재 + `## Sub-Specs` 표 파싱** — 표가 없거나 모든 행이 `Done`이면 "구현할 sub-spec이 없음" 보고 후 종료.
3. **`## Open Questions` 0건 확인** — root-spec과 모든 sub-spec의 Open Questions 섹션을 검사. 미해결 항목 1건이라도 있으면 멈추고 `/spec-interview` 또는 `/spec-resolve` 권유.
4. **Unity MCP 컴파일 상태** — Unity MCP 도구가 세션에 노출돼 있으면 `read_console`로 컴파일 에러 0건 확인. 도구가 없거나 인스턴스가 죽어 있으면 `AGENTS.md` "Unity MCP 사용 정책"에 따라 사용자에게 "MCP 없이 진행할까요?"를 묻는다.
5. **`.feature-build-state.json` 위치 결정** — `docs/specs/<feature>/.feature-build-state.json` (`.gitignore` 처리됨). 이미 존재하고 `pending_user_action`이 비어 있지 않으면 재개 모드로 진입.

### 2. Sub-spec 큐 생성

root-spec의 `## Sub-Specs` 표에서 `Status != Done` 행을 다음 정렬 키로 추출.

1. **NN prefix가 있으면** zero-pad 2자리 오름차순. 정책 단일 진실원: [`docs/specs/README.md`](../../docs/specs/README.md) "Sub-Spec 파일명".
2. **NN prefix가 없는 sub-spec이 섞여 있으면** root-spec의 Sub-Specs 표 등장 순서를 fallback 정렬 키로 사용. 동시에 사용자에게 한 줄 경고 — "sub-spec NN prefix 미부여. 큐 순서가 정확하지 않을 수 있음. 수동 cleanup 권장."

큐 미리보기 출력 (`[1] 01-foo Status=Active`, `[2] 02-bar Status=Draft` … / 또는 NN 없는 케이스 `[1] accompaniment Status=Draft (NN 미부여)` …). dry-run이든 `--apply`든 동일.

### 3. Per-sub-spec 루프 (`--apply`일 때만 실제 실행)

각 sub-spec에 대해 순서대로 다음을 수행.

#### 3-1. plan 존재 검사

sub-spec의 `## Implementation Plans` 표에서 `Status != Done` plan이 있는지 확인.

- **있으면** → 3-4로 (phase 0·drafter 건너뜀).
- **없으면** → 3-2.

#### 3-2. phase 0 (Architecture Decision)

`Task` 도구로 `arch-decision-extractor` sub-agent 호출. 입력 3종.

1. sub-spec 경로 (큐의 현재 항목).
2. parent `_index.md` 경로.
3. 기존 decisions 누적 — 같은 feature의 `docs/specs/<feature>/decisions/` 아래 `<NN>-*.md` 파일들을 모두 Read해 합본한 문자열. 없으면 빈 문자열.

반환된 `## decisions_to_resolve` 분기:

- **`_없음._`** → phase 0 종료. 3-3으로 직진.
- **후보 1+개** → `AskUserQuestion`으로 batch 질문. 한 번에 최대 4개씩, 5개면 라운드 분할.
  - 사용자 답을 받은 후 `docs/specs/<feature>/decisions/<NN>-<title>.md` 파일 1+개 작성. NN은 같은 feature의 기존 decisions/ 내 가장 큰 NN + 1 (없으면 01).
  - 작성된 decisions 파일 경로들을 이후 3-3의 plan-drafter 입력 5번으로 전달.

#### 3-3. plan-drafter 호출

`Task` 도구로 `plan-drafter` sub-agent 호출. `model: opus` 강제는 agent frontmatter로 처리. 입력 5종.

1. sub-spec 경로 (큐의 현재 항목).
2. parent `_index.md` 경로 (입력 root-spec).
3. 이전 sub-spec handoff 누적 — 같은 피처 안 NN prefix가 더 작은(또는 fallback에서 더 앞에 있는) sub-spec들의 완료 plan `## Handoff` 섹션을 모은 단일 문자열. 없으면 빈 문자열.
4. (선택) Caused By 컨텍스트 — 본 단계에서는 항상 null. Caused By 모드는 `/spec-implement`의 manual-hard fail 후 자동 시드 시 처리.
5. (신규) decisions 파일 경로 리스트 — phase 0에서 작성된 `decisions/<NN>-*.md` 경로들. phase 0에서 결정이 없었으면 빈 리스트.

반환된 4-필드 컴팩트 리포트(`plans_created` / `split_decision` / `assumed_facts` / `unresolved`) 보관.

- `unresolved` 비어 있지 않으면 `AskUserQuestion`으로 사용자 결정 위임 후 큐 중단. 상태 파일 `pending_user_action` 기록.
- `plans_created`가 비어 있으면 plan-drafter 호출 자체가 실패한 것 — 사용자 보고 후 멈춤.

#### 3-4. plan-quality-reviewer 호출

작성된(또는 기존) 미완료 plan 각각에 대해 `Task` 도구로 `plan-quality-reviewer` sub-agent 호출. 입력 3종.

1. plan 경로.
2. Linked Spec 경로 (= 현재 sub-spec).
3. parent `_index.md` 경로.

반환된 4-필드 컴팩트 리포트(`verdict` / `checks` / `auto_fix_hints` / `human_attention`) 분기:

- **`pass`** → 3-5로.
- **`fix-and-retry`** → plan-drafter Task 재호출 1회. 입력 5종에 추가로 reviewer의 `auto_fix_hints`를 prompt에 첨부. 재호출 후 다시 plan-quality-reviewer 호출. 또 `fix-and-retry`이거나 `stop`이면 `stop`으로 격상.
- **`stop`** → `human_attention[]` 항목을 사용자에게 표시 후 큐 중단. 상태 파일 `pending_user_action: "plan-quality-reviewer stop — 사용자 plan 수정 후 재호출"` 기록.

#### 3-5. `/spec-implement <sub-spec> --apply` 워크플로우 답습

이 시점부터 메인 세션은 [`/spec-implement`](spec-implement.md)의 워크플로우 step 1~4 (Pre-flight / 큐 미리보기 / Per-plan 루프 / 큐 종료)를 그대로 inline 실행한다. spec-implement를 별도 Task/Skill로 호출하지 않는다 — 컨텍스트 중첩 회피.

inline 실행 시 다음을 그대로 답습한다.

- step 1 통합 Pre-flight (working tree, plan 파일 존재, Linked Spec 존재, AC 라벨, Open Q 경고, MCP 컴파일).
- step 3 per-plan 루프 (3-1 진입 검증 → 3-2 AC 라벨 분류 → 3-3 이전 plan handoff 누적 → 3-4 plan Status `In Progress` Edit → 3-5 plan-orchestrator 호출 → 3-6 결과 분기 (manual-hard 4택 포함) → 3-7 (예약) → 3-8 Handoff 승인+적용 → 3-8.5 Caused By reflect → 3-9 plan-complete → 3-9.5 atomic commit → 3-10 다음 plan).
- step 4 큐 종료.

`--max-cascade` 값은 spec-build 인수에서 받은 값을 spec-implement step 3-6에 그대로 전달 (상태 파일 `max_cascade` 필드 갱신).

#### 3-6. sub-spec 완료 처리

inline 실행 결과 분기:

- **모든 plan `Done` + `deferred_failures`도 빈 상태** → 다음 sub-spec으로. plan-complete가 sub-spec을 `_archive/`로 옮기는 단계는 destructive 가드를 통과했을 때만.
- **모든 plan `Done` + `deferred_failures` 1건 이상** → sub-spec은 `_archive/`로 옮기지 않는다. 다음 sub-spec으로 진행하되, 메인이 상태 파일 `deferred_failures`를 누적해 들고 간다. (큐 종료 시 일괄 시드 옵션 제공.)
- **manual-hard `stop`/`stop-and-seed` 또는 plan-quality-reviewer `stop`이 발생해 inline 실행이 중단** → 큐 진행 중단. 상태 파일 `pending_user_action` + 다음 진입점 안내 후 멈춤.

### 4. 큐 종료

모든 sub-spec이 `Done`이고 `deferred_failures`가 비어 있으면 다음 두 경로 중 하나로 분기한다.

#### 4-A. feature archive 자동 트리거

다음 조건을 **모두** 만족할 때 `plan-complete` skill의 "feature 단위 archive" 분기를 호출한다:

1. 모든 sub-spec `Status == Done`.
2. `deferred_failures` 0건.
3. 모든 plan의 auto-hard / manual-hard 검증 pass (`.orchestrator-state.json` 참조).
4. working tree clean (마지막 atomic commit 직후).
5. Unity MCP 노출 시 `read_console` types=error 0건.

조건 충족 시 순서:

1. `archive-feature.sh <feature>` 호출 — 외부 참조 grep 결과 출력.
2. `AskUserQuestion`으로 이동 대상 경로 + 외부 참조 매치를 보여주고 사용자 승인 1회.
3. 승인 후 스크립트가 이미 수행한 `git mv` 결과 확인 + README 보드 갱신 + 외부 링크 Edit 갱신.
4. atomic commit: `chore(<feature>): feature archive 이동 + README 보드 갱신`.

#### 4-B. archive 보류 또는 deferred 처리

조건이 하나라도 충족되지 않으면 archive 분기를 진입하지 않고 사유 한 줄 보고 후 종료.

`deferred_failures`가 비어 있지 않으면:

- 메인이 deferred 목록을 사용자에게 한 번 표시.
- `AskUserQuestion`으로 "각 deferred 항목에 대해 `/plan-new --from-failure`로 후속 plan을 일괄 시드할까요? (yes/no/per-item)"를 묻는다.
- **yes** → 각 항목에 대해 순차적으로 `Skill` 도구로 `plan-new`를 `--from-failure <plan-path> --auto` 인수로 위임 호출. 시드 완료 후 사용자에게 `/spec-build <root-spec> --apply` 재호출을 안내.
- **per-item** → 항목별 yes/no 결정.
- **no** → deferred 목록을 한 번 더 출력하고 종료.

---

모든 sub-spec Done + deferred 0건 + archive 완료 시 한 줄 요약 출력 (총 N sub-spec, M plan, K commit).

중간에 멈춘 경우 → `pending_user_action` + 다음에 무엇을 해야 하는지 한 줄 출력. 재호출 시 상태 파일에서 현재 sub-spec / `current_plan_index`부터 재개.

## 모드

- **dry-run (기본값)** — 입력 분기, Pre-flight 1~5 모두 실제로 수행한 뒤, 큐 미리보기 + "각 sub-spec별로 plan-drafter / plan-quality-reviewer / spec-implement inline 실행 계획"을 한 줄씩 보고하고 멈춘다. **plan-drafter·plan-quality-reviewer를 단 한 번도 spawn하지 않는다.** plan 파일·코드·자산·git 상태 모두 변경 없음.
- **`--apply`** — 위 1~4단계를 모두 실제 실행.

## 상태 파일

위치: `docs/specs/<feature>/.feature-build-state.json` (`.gitignore` 처리됨).

```json
{
  "input_path": "docs/specs/<feature>/_index.md",
  "feature": "<feature-kebab>",
  "started_at": "YYYY-MM-DDTHH:MM:SS+09:00",
  "max_cascade": 2,
  "sub_spec_queue": [
    {
      "sub_spec_path": "docs/specs/<feature>/specs/<NN>-<sub>.md",
      "status": "queued | in_progress | done | blocked"
    }
  ],
  "current_sub_spec_index": 0,
  "deferred_failures": [
    {"plan_path": "...", "criteria": "...", "evidence": "..."}
  ],
  "last_step": "preflight | drafter | reviewer | implement | completed",
  "pending_user_action": null
}
```

`/spec-implement`의 `.orchestrator-state.json`과는 별도 파일이다 — spec-build는 sub-spec 큐를, spec-implement는 plan 큐를 각각 관리. 두 상태 파일이 한 피처 폴더에 공존할 수 있다.

재개 호출 시 `current_sub_spec_index`와 `last_step` 다음 단계부터 실행. 사용자가 중간에 sub-spec을 추가/삭제했어도 매 sub-spec 시작 직전 root-spec의 Sub-Specs 표를 다시 읽어 큐를 재생성.

## 출력 형식

진행 메시지는 한국어, 짧게. 사용자 결정이 필요한 지점에서는 `AskUserQuestion` 권장. 단계 진입 직전마다 한 줄 상태 보고("3-2. sub-spec [2/4] plan-drafter 호출 시작" 같은) 한 번씩.
