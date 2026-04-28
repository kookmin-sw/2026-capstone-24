---
description: plan 파일 또는 sub-spec 경로를 받아, 해당 plan(들)을 plan-orchestrator sub-agent에 위임해 순차 실행한다. 메인 세션은 큐 관리·사용자 상호작용·plan 파일 갱신만 담당해 컨텍스트를 최소화한다. 기본 dry-run, --apply로 실제 실행.
argument-hint: "<plan 또는 sub-spec 경로> [--apply]"
allowed-tools: Read, Glob, Grep, Bash, Edit, Write, AskUserQuestion, Skill, Task
---

# /spec-implement — Plan/Spec 실행 orchestrator

목적: 작성된 plan 한 개 또는 sub-spec 한 개의 미완료 plan들을 받아 다음 흐름을 일관되게 진행한다.

1. 입력 분기 (plan 경로 → 단일 큐 / sub-spec 경로 → 미완료 plan 큐)
2. 통합 Pre-flight (working tree·라벨·Open Q·MCP)
3. 큐 안의 각 plan에 대해:
   - plan 파일 `Status: In Progress` 기록
   - `plan-orchestrator` sub-agent 호출 (implementer → reviewer → commit → 자동 AC 검증)
   - 컴팩트 리포트 수신 → `next_action`으로 분기
   - 사용자 상호작용 4종(Open Q 경고는 1단계, manual-hard 검증·Handoff 승인·중단 결정은 plan별)
   - plan 파일 `## Handoff`/`## Notes` 갱신 → `plan-complete` skill 호출
4. 큐 종료 또는 중단 시점에서 상태 파일 갱신 + 한 줄 보고

기본 모드는 **dry-run**. 어떤 명령을 실행할 예정인지만 보고하고 멈춘다. `--apply`가 명시될 때만 sub-agent 호출·commit·검증·plan 파일 갱신을 실제로 수행한다.

## 절대 규칙

1. **한 plan = 한 atomic commit.** 두 plan을 한 번에 묶지 않는다.
2. **plan 파일은 메인 세션만 수정한다.** plan-orchestrator를 포함한 어떤 sub-agent에도 plan 파일 편집 권한을 주지 않는다 (도구 레벨에서 `Edit`/`Write` 미부여로 강제). `Status` 갱신·`Handoff` 작성·`Notes` 추가 모두 이 명령이 직접 한다.
3. **Pre-flight 실패 시 멈춘다.** dry-run에서도 working tree dirty면 진행하지 않는다.
4. **사용자 승인 없이 destructive·publishing 명령을 실행하지 않는다** (force push, branch 삭제, commit amend, reset --hard 등).
5. **메인 세션 사고를 sub-agent에 흘려보내지 않는다.** plan-orchestrator에는 정의된 입력 5종만 전달한다.
6. **plan-orchestrator는 사용자에게 질문하지 않는다.** 모든 사용자 결정은 메인 세션이 `AskUserQuestion`으로 처리한다.

## 입력 해석

`$ARGUMENTS`의 첫 토큰을 받아 경로 패턴으로 분기한다. plan 경로가 없으면 사용자에게 안내하고 멈춘다.

| 패턴 | 모드 | 큐 |
|---|---|---|
| `docs/specs/<feature>/plans/<...>.md` | `single` | `[<그 경로>]` |
| `docs/specs/<feature>/specs/<NN>-<sub>.md` | `spec` | sub-spec의 `## Implementation Plans` 표에서 `Status != Done` 행을 **`Caused By` 체인 + 작성일 위상정렬**로 추출 (아래 "Sub-spec 모드 큐 재생성 알고리즘" 박스 참조) |
| 그 외 | — | 안내 후 종료 (`docs/specs/README.md` 사용법 링크) |

`--apply`는 두 번째 토큰으로 인식한다.

분기 직후 한 줄 출력: `mode=<single|spec>, plans=<N>`. 사용자가 큐가 잘못된 경우 즉시 인식할 수 있도록.

## 워크플로우

### 1. 통합 Pre-flight (큐 시작 직전 1회)

다음 항목을 순서대로. 하나라도 실패하면 무엇이 막혔는지 보고하고 멈춘다.

1. **Working tree clean** — `git status --porcelain`이 비어 있는지. 비어 있지 않으면 변경 파일 목록을 보여주고 멈춘다.
2. **큐의 모든 plan 파일 존재** — 단일 모드면 1개, spec 모드면 큐 길이만큼.
3. **각 plan의 Linked Spec 추출 + 존재** — plan의 `**Linked Spec:**` 라인에서 경로를 파싱해 그 파일이 실재하는지.
4. **각 plan의 Acceptance Criteria 라벨 부여** — `## Acceptance Criteria` 섹션의 모든 `- [ ] ...` 항목이 `[auto-hard]` / `[auto-soft]` / `[manual-hard]` 중 하나를 인라인 코드로 갖는지. 미부여 항목이 있으면 plan 경로 + 해당 줄을 보여주고 plan 수정 또는 라벨 분류를 요청한 뒤 멈춘다.
5. **통합 Open Questions 경고** — 큐 plan들의 Linked Spec과 parent `_index.md`의 `## Open Questions` 섹션에서 `- [ ]` 미해결 항목을 모두 모아 한 번 표시. `AskUserQuestion`으로 "이 큐와 무관해 진행해도 좋습니까?"를 묻는다. 사용자가 "no"면 멈추고 `/spec-resolve` 권유. "yes"면 결정 사실을 상태 파일 `open_questions_acknowledged`에 기록하고 진행. **plan별 반복 X — 큐 시작 직전 1회만.**
6. **Unity MCP 컴파일 상태** — Unity MCP 도구가 세션에 노출돼 있으면 `read_console`로 컴파일 에러 0건을 확인. 도구가 없거나 인스턴스가 죽어 있으면 `AGENTS.md` "Unity MCP 사용 정책"에 따라 사용자에게 "MCP 없이 진행할까요?"를 묻는다.

### 2. 큐 미리보기 출력

각 plan의 `[N] <plan_path> (작성일, 현재 Status)`를 한 줄씩 출력. dry-run이든 `--apply`든 동일.

### 3. Per-plan 루프 (`--apply`일 때만 실제 실행)

각 plan에 대해 순서대로 다음을 수행한다. 어느 단계에서 멈추든 상태 파일에 진행 상황을 기록한다.

#### 3-1. 진입 검증
- `git status --porcelain` 재확인 (이전 plan의 commit 후 clean 상태여야 함). 비어있지 않으면 멈추고 사용자에게 보고.

#### 3-2. AC 라벨 분류
- 해당 plan의 `## Acceptance Criteria`를 다시 파싱해 항목별 `{label, criteria}` 배열 생성. plan-orchestrator의 입력 5번에 전달할 것.

#### 3-3. 이전 plan handoff 누적
- 같은 sub-spec의 `## Implementation Plans` 표에서 **Status가 `Done`인 모든 plan**의 `## Handoff` 섹션을 작성일 오름차순으로 모은다.
- 각 섹션 위에 `### <plan title> (YYYY-MM-DD)` 헤더를 부착해 단일 문자열로 합친다.
- 아카이브된 plan(`_archive/<feature>/plans/`)도 표 링크가 가리키는 한 포함한다. 없거나 빈 섹션이면 빈 문자열.

#### 3-4. plan Status 갱신
- 메인이 plan 파일의 `**Status:**` 라인을 `In Progress`로 Edit. 상태 파일 `last_step: implement`.

#### 3-5. plan-orchestrator 호출
`Task` 도구로 `plan-orchestrator`를 호출한다. 입력 5종만 전달:

1. plan 파일 경로
2. Linked Spec 경로
3. parent `_index.md` 경로
4. 이전 plan handoff 누적 (3-3 산출물)
5. AC 라벨 분류 배열 (3-2 산출물)

반환된 9섹션 리포트를 받아 보관.

#### 3-6. 결과 분기

리포트의 `next_action`으로 분기한다.

| `next_action` | 메인 처리 |
|---|---|
| `review-failed` | 사유·관찰을 표시. "implementer 재호출 / plan 수정 / 중단" 중 사용자 결정 묻기 (`AskUserQuestion`). 결정 즉시 반영하지 못하면 상태 파일 `pending_user_action` 기록 후 멈춤. |
| `implementer-blocked` | unresolved 사유 표시. plan 수정 안내 후 멈춤. |
| `compile-error` | acceptance_results의 실패 항목과 콘솔 출력 위치 표시. 수동 수정 후 재시작 안내 후 멈춤. |
| `tree-dirty` | working tree dirty 사유 표시. 정리 또는 별도 commit 옵션 묻기. |
| `mcp-down` | MCP 재연결 또는 skip 옵션 묻기. |
| `manual-hard-verification` | `manual_hard_pending` 항목별로 `AskUserQuestion`(통과/실패/보류). 실패 또는 보류가 하나라도 있으면 plan Status `In Progress` 유지 + 큐 중단 후, 추가로 "지금 후속 plan을 시드할까요?"를 `AskUserQuestion` (옵션: yes/no/later). **yes** → `Skill` 도구로 `plan-new`를 `--from-failure <current-plan-path>` 인수로 위임. skill 종료 후 상태 파일에 `pending_user_action: "후속 plan 시드 완료 — /spec-implement <sub-spec-path> --apply 재호출 대기"` 기록 후 멈춤. **no/later** → 기존대로 `pending_user_action`만 기록 후 멈춤. 전부 통과 → 3-7로. |
| `handoff-approval` | (manual-hard 0건이라 바로 도달) 3-7로. |
| `none` | 정상 종료 — 거의 없음. status가 `completed`인데 next_action이 `none`이면 3-7로. |

`status: failed`인 모든 분기에서 plan Status는 `In Progress` 유지. 큐 진행 중단.

#### 3-7. auto-soft 실패 노트 적용
- 리포트의 `auto_soft_failed_notes`가 비어있지 않으면 plan 파일의 `## Notes` 섹션에 메인이 직접 append (없으면 새로 만든다).

#### 3-8. Handoff 승인 + 적용
- 리포트의 `handoff_candidate`을 사용자에게 보여주고 `AskUserQuestion`으로 승인/수정/거절 묻기.
- 승인 또는 수정안 확정 시 plan 파일의 `## Handoff` 섹션에 메인이 Edit (5~15줄, 다음 plan이 알아야 할 공개 API/자산 경로).
- 거절은 빈 섹션 유지.

#### 3-8.5. 선행 plan 재검증 reflect (Caused By 자동 reflect)

현재 plan이 검증 실패에서 파생된 후속 plan일 때, 선행 plan의 fail AC를 자동으로 pass로 갱신한다.

1. 현재 plan 헤더 `**Caused By:**` 라인을 parse — 없으면 noop, 있으면 다음.
2. 선행 plan의 같은 sub-spec(또는 cross-feature 가능성도 있음) `.orchestrator-state.json`을 Read.
3. `per_plan_history[]`에서 선행 plan_path를 찾는다. 없으면 noop + Notes에 한 줄 경고 append (`수동 폴백 필요`).
4. 그 항목의 `acceptance_results[]`에서 `status: fail` 또는 `skipped`인 항목과, 현재 plan의 acceptance_results 중 criteria가 `선행 plan ... 의 실패 AC ... 가 이 plan 적용 후 재검증에서 통과한다` 형식으로 시작하고 status가 `pass`인 "재검증" 항목을 매칭한다.
5. 매칭된 선행 plan 항목의 `status`를 `pass`로, `evidence`에 `"후속 plan <current-plan-filename> manual-hard 재검증 통과 (<YYYY-MM-DD>)"`를 append.
6. 매칭 실패 시 noop + 메인이 현재 plan의 `## Notes`에 한 줄 경고 append (`자동 reflect 매칭 실패 — 사용자가 선행 plan을 큐에 다시 올려 manual로 통과 처리 필요`).

매칭 실패는 후속 plan의 "재검증" AC 문구가 임의로 바뀌었거나 상태 파일이 사라진 경우에 일어난다. 자동 reflect가 안 되어도 사용자가 manual로 선행 plan의 manual-hard를 다시 통과 처리할 수 있는 경로(폴백)가 항상 살아 있어야 한다.

#### 3-9. plan-complete skill 호출
- `Skill` 도구로 `plan-complete` 호출. plan 경로 위임.
- skill이 plan Status `Done` 갱신, sub-spec의 Implementation Plans 표 갱신, parent `_index.md` 동기화, `docs/specs/README.md` 상태 보드 갱신, 완료된 plan 파일을 `_archive/`로 이동까지 처리.

#### 3-10. 다음 plan으로
- 상태 파일에 `per_plan_history` 항목 추가 (commit_sha, ac_results, auto_soft_notes).
- `current_plan_index` 증가. 큐에 plan이 더 있으면 3-1로.

### 4. 큐 종료

- 모든 plan `Done` → 상태 파일 `last_step: completed`. 한 줄 요약 출력 (총 N plan, M commit, 다음 plan 후보 sub-spec이 있으면 안내).
- 중간에 멈춘 경우 → `pending_user_action` + 다음에 무엇을 해야 하는지 한 줄 출력. 재호출 시 상태 파일에서 `current_plan_index`부터 재개.

## 모드

- **dry-run (기본값)** — 입력 분기, Pre-flight 1~6 모두 실제로 수행한 뒤, 큐 미리보기 + "각 plan별로 plan-orchestrator(plan=…, prev_handoff=<N자>)를 어떤 입력으로 호출할 예정인지"만 보고하고 멈춘다. **plan-orchestrator를 단 한 번도 spawn하지 않는다.** plan 파일·코드·자산·git 상태 모두 변경 없음.
- **`--apply`** — 위 1~4단계를 모두 실제 실행. 각 plan별로 plan-orchestrator를 spawn하고, manual-hard·handoff 결정을 받아 plan 파일을 갱신하고, `plan-complete`로 마무리.

## 상태 파일

위치: `docs/specs/<feature>/.orchestrator-state.json` (`.gitignore` 처리됨).

```json
{
  "mode": "single | spec",
  "input_path": "docs/specs/.../plans/....md 또는 .../specs/....md",
  "started_at": "2026-04-28T10:00:00+09:00",
  "plan_queue": [
    {
      "plan_path": "docs/specs/.../plans/....md",
      "created_at": "YYYY-MM-DD",
      "status": "queued | in_progress | done | blocked",
      "commit_sha": null
    }
  ],
  "current_plan_index": 0,
  "per_plan_history": [
    {
      "plan_path": "...",
      "commit_sha": "abc123",
      "acceptance_results": [
        {"label": "auto-hard", "criteria": "...", "status": "pass | fail | skipped", "evidence": "..."}
      ],
      "auto_soft_notes": []
    }
  ],
  "open_questions_acknowledged": [],
  "unity_compile_check": "pass | skip | fail",
  "last_step": "preflight | implement | review | commit | verify | handoff | completed",
  "pending_user_action": null
}
```

이중 실행 방지 차원에서 plan 본문의 `Status: In Progress`도 3-4단계에 기록한다(`--apply` 모드에서만). 재개 호출 시 `current_plan_index`와 `last_step` 다음 단계부터 실행.

`spec` 모드에서는 매 plan 시작 직전 sub-spec 표를 다시 읽어 큐를 재생성하고, `done` 처리된 plan만 history에서 보존한다. 사용자가 중간에 plan을 추가/삭제했어도 큐가 자연스럽게 따라간다.

### Sub-spec 모드 큐 재생성 알고리즘

`spec` 모드에서 큐를 만들 때(또는 매 plan 시작 직전 재생성 시) 정렬 키는 단순 작성일 오름차순이 아니라 **`Caused By` 체인 + 작성일 위상정렬**이다.

1. sub-spec의 `## Implementation Plans` 표에서 `Status != Done` 행을 모두 수집한다.
2. 각 plan 파일을 Read해 헤더 `**Caused By:**` 라인을 parse한다 — 의존 그래프 구축. 라인이 없으면 그 plan은 의존 없음.
3. 위상정렬: `Caused By`로 가리켜진 선행 plan은 후속 plan보다 큐에 **먼저** 들어가는 것이 원칙.
4. **단, 선행 plan의 Status가 `In Progress`(=blocked)이면 후속 plan을 먼저 처리한다** — 선행이 막혀 있으니 후속을 풀고 와야 재검증이 가능하기 때문이다. 이 경우 우선순위가 뒤집힌다.
5. 같은 우선순위 안에서는 작성일 오름차순.
6. 큐 미리보기(2단계) 출력 시 정렬 사유를 한 줄 추가한다 (예: `[1] plan3 — plan1이 In Progress(blocked)이고 plan3.Caused By=plan1이라 먼저 처리`).

`Caused By` 체인이 cross-feature(다른 sub-spec/feature를 가리킴)인 경우는 본 알고리즘 범위 밖이다 — 그 경우 위상정렬은 같은 sub-spec 안에서만 작동하고, cross-feature 의존성은 사용자가 수동으로 큐 순서를 정한다.

## 출력 형식

진행 메시지는 한국어, 짧게. 사용자 결정이 필요한 지점에서는 `AskUserQuestion` 권장. 단계 진입 직전마다 한 줄 상태 보고("3-5. plan [2/4] orchestrator 호출 시작" 같은) 한 번씩.
