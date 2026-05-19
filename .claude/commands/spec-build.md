---
description: root-spec(_index.md) 한 개를 받아 그 피처의 sub-spec 큐를 자동으로 진행한다. 각 sub-spec에 대해 planner → 사용자 plan 검토 → 메인이 직접 implementer+test+reviewer+자동 AC → 사용자 manual-hard 테스트 → plan 단위 atomic commit을 plan 1개씩 반복하고, sub-spec 종료 시 메인이 문서 갱신 + 정리 commit으로 묶는다. plan 본문은 사용자에게 직접 노출하고 검토 게이트를 1회 거친다. 기본 dry-run, --apply로 실제 실행.
argument-hint: "<root-spec 경로 (_index.md)> [--apply] [--max-cascade N]"
allowed-tools: Read, Glob, Grep, Bash, Edit, Write, AskUserQuestion, Skill, Task, mcp__UnityMCP__read_console
---

# /spec-build — 슬림 orchestrator

`/spec-interview`로 박제된 root-spec 1개를 받아, sub-spec 큐를 plan 1개씩 직렬로 진행한다.

## 절대 규칙

1. **plan은 항상 1개씩 작성·검토·구현한다.** 한 sub-spec에 plan이 N개 필요하면 planner를 N회 호출한다. planner는 매 호출당 plan 1개만 만든다.
2. **사용자 게이트 3종만 노출한다.**
   - plan 검토 (planner 직후, plan 본문 + self-check 진단 + 3택)
   - manual-hard 테스트 (3-4 직후, 3택: pass / retry-via-new-plan / stop)
   - destructive 가드 (feature archive 직전 1회)
3. **commit·plan 파일 편집·문서 갱신(README/_index 표·Status·archive 이동)은 메인 전용.** 어느 sub-agent도 하지 않는다. atomic commit은 plan 단위 + sub-spec 종료 시 정리 commit 1개.
4. **재호출 시 `.feature-build-state.json`으로 재개.** 위치: `docs/specs/<feature>/.feature-build-state.json` (`.gitignore`).

## 입력 해석

`$ARGUMENTS`의 첫 토큰은 root-spec 경로(`_index.md`). 그 외는 거부하고 형식 오류 보고.

`--apply`는 두 번째 토큰. `--max-cascade N`은 세 번째 토큰(생략 시 default 2).

## 1. Pre-flight

1. Working tree clean (`git status --porcelain` 비어야 함). 비어 있지 않으면 변경 파일 목록 보고 후 중단.
2. root-spec 존재 + `## Sub-Specs` 표 파싱. 표 없거나 모든 행 Done이면 "구현할 sub-spec 없음" 보고 후 종료.
3. root-spec + 모든 sub-spec의 `## Open Questions` 0건 확인. 미해결 1건 이상이면 중단 후 `/spec-interview` 재호출 권유.
4. Unity MCP 컴파일 상태 확인 (`read_console` types=error 0건). MCP 미가용이면 `AGENTS.md` "Unity MCP 사용 정책"에 따라 "MCP 없이 진행할까요?" 묻기.
5. `.feature-build-state.json` 로드 (있으면 재개 모드).

## 2. Sub-spec 큐 생성

root-spec `## Sub-Specs` 표에서 `Status != Done` 행을 NN prefix zero-pad 2자리 오름차순으로 추출. NN 미부여가 섞이면 등장 순서 fallback + 경고 1줄.

큐 미리보기 출력 (dry-run/--apply 공통).

## 3. Per sub-spec 루프 (`--apply`만 실행)

각 sub-spec에 대해 다음을 plan 1개씩 반복한다.

### 3-1. plan 책임 추론

메인 세션이 sub-spec + parent `_index.md` + (있으면) tech-specs/decisions를 Read해 **다음 plan 1개가 책임질 영역**을 1줄로 추론한다. 이미 완료된 plan들의 `## Handoff` 누적을 함께 본다.

미완료 plan이 sub-spec `## Implementation Plans` 표에 이미 있으면 그 plan을 다음 대상으로 삼고 3-3으로 진입.

### 3-2. planner 호출

`Task` 도구로 `planner` sub-agent 호출. 입력 7종:
1. sub-spec 경로
2. parent `_index.md` 경로
3. 이전 sub-spec의 plan 경로 리스트 — planner가 직접 `## Handoff` 발췌
4. (선택) Caused By 컨텍스트 — 3-4에서 retry-via-new-plan 진입 시만
5. (선택) decisions 파일 경로 리스트
6. (선택) Tech Spec 경로
7. (선택) 메인 추론 한 줄 — 3-1 결과

반환 4-필드 컴팩트 리포트(`plan_created` / `self_check` / `assumed_facts` / `unresolved`) 보관.

- `unresolved` 비어 있지 않으면 `AskUserQuestion`으로 사용자 결정 위임 후 큐 중단. 상태 파일 `pending_user_action` 기록.
- `plan_created`가 비어 있으면 작성 실패 — 사용자 보고 후 멈춤.

상태 파일 `last_step: "planner"` 갱신.

### 3-3. 사용자 plan 검토 게이트

plan 본문(planner가 작성한 파일) 전체와 `self_check` 진단을 사용자에게 노출한다. `AskUserQuestion`으로 3택:

- **approve** → 3-4로.
- **modify** → 사용자 자유 텍스트로 수정 사항 → planner 재호출 (입력 7에 수정 요구 append) → 3-3 회귀.
- **abort** → 큐 중단. 상태 파일 `pending_user_action: "user-aborted-plan"` 기록.

### 3-4. plan 구현·검증

메인이 다음을 순차 실행한다. 코드 변경은 working tree에 남긴다 — commit은 3-6.

1. `Task` → `implementer`. 입력 4종(plan / Linked Spec / parent `_index.md` / 같은 sub-spec의 Done plan 경로 리스트). 반환 4섹션(`## 변경 파일` / `## Commit 후보` / `## Handoff 요약 후보` / `## 미해결`) 보관.
2. `Bash` → `git diff HEAD` 캡처. 빈 diff면 사용자 보고 후 3택으로.
3. `Task` → `unity-test-runner`. 변경 파일에서 도메인 힌트 추출해 `changed_domains` 전달. 반환 첫 줄: `PASS` → 4로. `FAIL`/`COMPILE ERROR` → 사용자 보고 후 3택. `MCP UNAVAILABLE` → "테스트 미실행" 기록 후 4로.
4. `Task` → `reviewer`. 입력 4종(plan / Linked Spec / 2의 diff / plan AC 원문 + test_report). `pass` → 5로. `needs-fix` → 사용자 보고 후 3택.
5. 메인이 plan의 `auto-hard`/`auto-soft` 항목을 직접 파싱해 순회 검증(Grep / `read_console` / Bash). `manual-hard`는 건드리지 않는다. `auto-hard` 실패 → 사용자 보고 후 3택. `auto-soft` 실패 → plan `## Notes` append 후보로 누적.

상태 파일 `last_step: "build"` 갱신.

`manual_hard_pending`(plan의 `manual-hard` 라벨 항목)이 있으면 3-5로, 없으면 3-6으로.

3택은 모두 동일: `retry-via-new-plan` → 3-7. `stop` → 큐 중단 + 상태 파일 `pending_user_action` 기록. 자유 텍스트 수정 → 메인이 즉시 보정 후 해당 단계 회귀.

### 3-5. 사용자 manual-hard 테스트 게이트

`manual_hard_pending` 항목을 사용자에게 한 줄씩 보여주고 각 항목의 검증 시나리오(plan AC evidence 라인)를 함께 표시한다. `AskUserQuestion`으로 3택:

| 옵션 | 동작 |
|---|---|
| **pass** | 모든 manual-hard 통과. evidence를 plan `## Notes`에 박제. 3-6으로. |
| **retry-via-new-plan** | 실패 사유를 받아 후속 plan을 planner에 발주. 3-7로. |
| **stop** | 큐 중단. 상태 파일 `pending_user_action: "manual-hard-failed"` 기록. |

### 3-6. plan 단위 atomic commit

메인이 직접 다음을 순서대로 수행한다.

1. plan 파일 `Status:` 를 `Done`으로 Edit.
2. implementer가 반환한 Handoff/Notes 후보를 plan `## Handoff` / `## Notes` 섹션에 append (Edit). 빈 후보면 생략.
3. sub-spec의 `## Implementation Plans` 표에서 해당 plan 행 Status 컬럼을 `Done`으로 Edit.
4. 같은 plan을 참조하는 외부 링크가 깨지지 않는지 `Grep` 1회로 확인.
5. `git-workflow` skill로 atomic commit 생성. 메시지는 implementer가 반환한 `message_candidate` 우선, 없으면 plan 제목 + Linked Spec slug.
6. commit 완료 후 같은 sub-spec에 plan이 더 필요한지 판단 → 필요하면 3-1 회귀, 아니면 3-8로.

> plan 파일은 본 단계까지 원위치(`docs/specs/<feature>/plans/`)에 남는다. feature-archive 시점(§4)에 `_archive/<feature>/plans/`로 일괄 이동한다.

### 3-7. retry-via-new-plan 분기

cascade_depth 검사. 상태 파일 `cascade_depth >= max-cascade`이면 거부 + 사용자 호출 후 큐 중단.

조건 통과 시:
- planner 입력 4(Caused By)에 선행 plan 경로 + 실패 AC 발췌를 채워 재호출.
- planner가 새 plan 작성 (Caused By 헤더 + Context 인용 + 재검증 AC 자동 부착).
- `cascade_depth += 1` 갱신.
- 3-3로 회귀.

### 3-8. sub-spec 종료 처리

메인이 직접 다음을 순서대로 수행한다.

1. sub-spec frontmatter `Status:` 를 `Done`으로 Edit.
2. parent `_index.md` 의 `## Sub-Specs` 표에서 해당 sub-spec 행의 Status 컬럼을 `Done`으로 Edit.
3. `docs/specs/README.md` 상태 보드 표의 해당 feature 행에서 `Plans (Done/Total)` / `Sub-Specs` 카운트를 재계산해 Edit.
4. 같은 feature의 모든 sub-spec이 `Done`이고 `_index.md` `## Open Questions` 가 비어 있으면 §4 진입 후보로 표시.
5. sub-spec 정리 commit 1개 (`git-workflow` skill). 메시지: `chore(<feature>): <sub-spec slug> 완료 + docs 동기화`.
6. 다음 sub-spec으로.

## 4. Feature 종료

모든 sub-spec Done + Open Q 0건 + manual-hard 모두 pass + working tree clean + MCP error 0건이면 archive 분기 진입. 메인이 직접 다음을 순서대로 수행한다.

1. **외부 참조 grep** — `Grep "docs/specs/<feature>/"` 로 본 feature 경로를 가리키는 외부 링크를 수집한다 (파일·라인·인용 텍스트).
2. **이동 예정 경로 미리보기** — `docs/specs/<feature>/` → `docs/specs/_archive/<feature>/`. 대상 폴더에 동명 자식이 이미 있으면 안 겹치는 항목만 병합 이동.
3. `AskUserQuestion`으로 destructive 가드 1회 (1번 외부 참조 + 2번 이동 대상을 함께 보여줌).
4. 사용자 승인 → 실제 이동·갱신:
   - `Bash git mv docs/specs/<feature>/ docs/specs/_archive/<feature>/` (대상 부재 시 `mkdir -p` 선행).
   - 1번에서 발견한 외부 참조 경로를 `docs/specs/<feature>/...` → `docs/specs/_archive/<feature>/...` 로 일괄 Edit. 이동 대상 폴더 내부의 상호 참조는 상대경로라 그대로 둔다.
   - `docs/specs/README.md` 상태 보드 표에서 해당 feature 행 링크를 `[<feature>](_archive/<feature>/_index.md)` 로 교체하고 Status `Done` 으로 Edit.
   - working tree에 `.feature-build-state.json` 잔존 시 `Bash rm -f`.
5. atomic commit (`git-workflow` skill). 메시지: `chore(<feature>): feature archive 이동 + README 보드 갱신`.

조건 미충족이거나 사용자 거절 시 사유 1줄 보고 후 종료.

## 상태 파일

위치: `docs/specs/<feature>/.feature-build-state.json` (`.gitignore`).

```json
{
  "input_path": "docs/specs/<feature>/_index.md",
  "feature": "<feature-kebab>",
  "started_at": "YYYY-MM-DDTHH:MM:SS+09:00",
  "max_cascade": 2,
  "cascade_depth": 0,
  "sub_spec_queue": [
    {"sub_spec_path": "docs/specs/<feature>/specs/<NN>-<sub>.md", "status": "queued | in_progress | done | blocked"}
  ],
  "current_sub_spec_index": 0,
  "current_plan_path": null,
  "last_step": "preflight | planner | review | build | manual-hard | commit | sub-spec-done | completed",
  "pending_user_action": null
}
```

재호출 시 `current_sub_spec_index` + `last_step` 다음 단계부터 재개.

## 모드

- **dry-run (기본)** — Pre-flight + 큐 미리보기 + per-sub-spec 진행 계획 1줄씩 보고 후 멈춤. **sub-agent spawn 없음.** 파일·코드·git 변경 없음.
- **`--apply`** — 1~4단계 실제 실행.

## 출력 형식

진행 메시지는 한국어, 짧게. 사용자 결정 게이트는 `AskUserQuestion`. 단계 진입 직전마다 한 줄 상태 보고("3-2. sub-spec [2/4] planner 호출" 같은).
