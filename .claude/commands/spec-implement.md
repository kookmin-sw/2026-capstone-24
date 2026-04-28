---
description: 한 plan 파일을 받아 컨텍스트 적재 → implementer sub-agent → reviewer sub-agent → commit → Acceptance Criteria 검증 → Handoff 갱신/아카이브를 orchestrate한다. 기본 dry-run, --apply로 실제 실행.
argument-hint: "<plan 파일 경로> [--apply]"
allowed-tools: Read, Glob, Grep, Bash, Edit, Write, AskUserQuestion, Skill, Task
---

# /spec-implement — Plan 실행 orchestrator

목적: 작성된 plan 한 개를 받아 (1) 컨텍스트 적재 → (2) `plan-implementer` 호출 → (3) `plan-reviewer` 호출 → (4) commit (`git-workflow` 위임) → (5) Acceptance Criteria 검증 → (6) `## Handoff` 갱신 + `plan-complete` 호출까지 일관된 한 줄 흐름으로 진행한다.

기본 모드는 **dry-run**. 어떤 명령을 실행할 예정인지만 보고하고 멈춘다. `--apply`가 명시될 때만 sub-agent 호출·commit·검증·plan 파일 갱신을 실제로 수행한다.

## 절대 규칙

1. **한 plan = 한 atomic commit.** 두 plan을 한 번에 진행하지 않는다.
2. **plan 파일은 orchestrator만 수정한다.** sub-agent에게 plan 파일 편집을 위임하지 않는다(`Status` 갱신·`Handoff` 작성 모두 이 명령이 직접 한다).
3. **Pre-flight 실패 시 멈춘다.** dry-run에서도 working tree dirty면 진행하지 않는다.
4. **사용자 승인 없이 destructive·publishing 명령을 실행하지 않는다** (force push, branch 삭제, commit amend, reset --hard 등).
5. **메인 세션 사고를 sub-agent에 흘려보내지 않는다.** implementer/reviewer에는 정의된 입력만 전달한다.

## 입력

- `$ARGUMENTS` — `<plan 경로>` 또는 `<plan 경로> --apply`. plan 경로가 없으면 사용자에게 안내하고 멈춘다.

## 워크플로우

### 1. Pre-flight 검증

다음 항목을 순서대로. 하나라도 실패하면 무엇이 막혔는지 보고하고 멈춘다.

1. **Working tree clean** — `git status --porcelain`이 비어 있는지. 비어 있지 않으면 변경 파일 목록을 보여주고 멈춘다.
2. **plan 파일 존재** — 인수로 받은 경로가 실재하는지.
3. **Linked Spec 추출 + 존재** — plan의 `**Linked Spec:**` 라인에서 경로를 파싱해 그 파일이 실재하는지.
4. **Acceptance Criteria 라벨 부여** — plan `## Acceptance Criteria` 섹션의 모든 `- [ ] ...` 항목이 `[auto-hard]` / `[auto-soft]` / `[manual-hard]` 중 하나를 인라인 코드로 갖는지. 미부여 항목이 있으면 그 줄을 보여주고 plan 수정 또는 라벨 분류를 요청한 뒤 멈춘다.
5. **Open Questions 경고** — Linked Spec(또는 parent `_index.md`)의 `## Open Questions` 섹션에 `- [ ]` 미해결 항목이 있으면 목록을 보여주고 "이 plan과 무관해 진행해도 좋습니까?"를 묻는다 (`AskUserQuestion`). 사용자가 "no"면 멈추고 `/spec-resolve` 권유. "yes"면 결정 사실을 상태 파일에 기록하고 진행.
6. **Unity MCP 컴파일 상태** — Unity MCP 도구가 세션에 노출돼 있으면 `read_console`로 컴파일 에러 0건을 확인. 도구가 없거나 인스턴스가 죽어 있으면 `AGENTS.md` "Unity MCP 사용 정책"에 따라 사용자에게 "MCP 없이 진행할까요?"를 묻는다.

### 2. 컨텍스트 적재

(`docs/specs/README.md` "Plan 실행 시 읽기 순서"와 동일)

1. plan 파일.
2. Linked Spec.
3. parent `_index.md` (Linked Spec과 같은 feature 폴더).

### 3. 이전 plan handoff 적재

같은 sub-spec의 `Implementation Plans` 표에서 **Status가 `Done`인 이전 plan들**의 `## Handoff` 섹션을 모은다. 없거나 빈 섹션이면 빈 문자열로 처리.

### 4. Implementation sub-agent 호출

`Task` 도구로 `plan-implementer`를 호출한다. 입력 4종만 전달:

1. plan 파일 경로
2. Linked Spec 경로
3. parent `_index.md` 경로
4. 이전 plan handoff 요약 (3단계 산출물)

implementer가 반환한 `## 변경 파일`·`## Commit 후보`·`## Handoff 요약 후보`·`## 미해결`을 받아 보관.

### 5. Review sub-agent 호출

`git diff HEAD`로 staged+unstaged diff를 캡처한 뒤, `Task` 도구로 `plan-reviewer`를 호출한다. 입력 4종만 전달:

1. plan 파일 경로
2. Linked Spec 경로
3. `git diff` 출력
4. plan의 Acceptance Criteria 목록 (라벨 포함 원문)

`pass` → 6단계로. `needs-fix` → 사유를 보여주고 멈춘다. 상태 파일 `pending_user_action`에 "implementer 재호출 또는 plan 수정 후 재시작" 기록.

### 6. Commit

`Skill` 도구로 `git-workflow`를 호출한다. 위임 정보:

- plan 제목 (plan 파일 H1)
- plan 경로
- implementer가 보고한 commit 메시지 후보
- 변경 파일 목록

커밋 결과 SHA를 받아 상태 파일에 기록. orchestrator가 직접 `git commit`을 작성하지 않는다.

### 7. Acceptance Criteria 검증

plan의 라벨된 항목을 순회한다.

- **`[auto-hard]` / `[auto-soft]`** — 항목 본문을 읽고 검증 방법을 추론한다(예: "컴파일 에러 0건" → `read_console`; "함수 X가 존재" → `Grep`; "빌드 성공" → 해당 빌드 명령). 추론이 불확실하면 사용자에게 "이 항목 어떻게 검증할까요?"를 묻고 답을 받아 실행. 결과를 상태 파일 `acceptance_results`에 기록.
- **`[manual-hard]`** — 항목 원문을 보여주고 "통과/실패/보류"를 묻는다 (`AskUserQuestion`). 응답을 기록.

검증 도중 추가 파일 수정이 생기면(예: 빌드 산출물·로그) 사용자에게 보고하고 별도 정리 또는 진행 여부를 묻는다.

### 8. 결과 분기

- **`*-hard` 실패** → pause. plan `Status`는 `In Progress` 유지. `pending_user_action`에 다음 행동 한 줄 기록. 사용자에게 보고하고 멈춘다.
- **`auto-soft` 실패** → plan의 `## Notes`(또는 `## Handoff`)에 한 줄 기록하고 진행.
- **전부 통과** → 9단계로.

### 9. 마무리

1. implementer가 제출한 `## Handoff 요약 후보`를 사용자에게 보여주고 승인을 받아 plan의 `## Handoff` 섹션에 적용 (5~15줄, 다음 plan이 알아야 할 공개 API/자산 경로).
2. `Skill` 도구로 `plan-complete`를 호출. plan `Status` → `Done` 갱신, `Implementation Plans` 표·parent `_index.md`·상태 보드 동기화·아카이브 이동까지 위임.
3. 상태 파일을 삭제하거나 `last_step: "completed"`로 마킹.
4. commit SHA, 변경 파일 수, 다음 plan 후보(있으면)를 한 줄로 보고하고 종료.

## 모드

- **dry-run (기본값)** — 1~3단계까지 실제로 수행한 뒤, 4~9단계는 "어떤 sub-agent/skill을 어떤 입력으로 호출할 예정인지"만 보고하고 멈춘다. plan 파일·코드·자산·git 상태를 수정하지 않는다.
- **`--apply`** — 1~9단계를 모두 실제 실행.

## 상태 파일

위치: `docs/specs/<feature>/.orchestrator-state.json` (`.gitignore` 처리됨).

```json
{
  "plan_path": "docs/specs/.../plans/....md",
  "started_at": "2026-04-28T10:00:00+09:00",
  "last_step": "preflight | context | implement | review | commit | verify | handoff | completed",
  "commit_sha": "abc123 또는 null",
  "acceptance_results": [
    {"label": "auto-hard", "criteria": "...", "status": "pass | fail | skipped", "evidence": "..."}
  ],
  "open_questions_acknowledged": [],
  "unity_compile_check": "pass | skip | fail",
  "pending_user_action": null
}
```

이중 실행 방지 차원에서 plan 본문의 `Status: In Progress`도 4단계 진입 직전에 기록한다(`--apply` 모드에서만). 재개 호출 시 `last_step` 다음 단계부터 실행.

## 출력 형식

진행 메시지는 한국어, 짧게. 사용자 결정이 필요한 지점에서는 `AskUserQuestion` 권장. 단계 진입 직전마다 한 줄 상태 보고("4. implementer 호출 시작" 같은) 한 번씩.
