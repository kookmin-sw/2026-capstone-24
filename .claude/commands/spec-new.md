---
description: 질문-응답으로 새 Spec(또는 sub-spec)을 점진적으로 작성한다. docs/specs/ 외부는 절대 수정하지 않는다.
argument-hint: "[러프한 아이디어 한 줄]"
allowed-tools: Read, Glob, Grep, Write, Edit, AskUserQuestion
---

# /spec-new — Spec 작성 워크플로우

목적: 사용자의 러프한 아이디어를 받아 **What/Why만 담는 얇은 spec**으로 점진 구체화한다. 코드는 읽기만 하고 절대 수정하지 않는다.

## 절대 규칙

1. **수정 허용 경로는 `docs/specs/**`뿐.** `Assets/`, `Packages/`, `ProjectSettings/`, 그 외 모든 코드/직렬화 자산은 수정 금지. (읽기는 허용)
2. **Spec 본문에 구현 디테일 금지.** 구체적 함수명, 클래스명, 파일 경로, 자료구조, 알고리즘은 spec이 아니라 plan에 들어간다.
3. **사용자 승인 없이 파일을 만들지 않는다.** 폴더/파일 구조 제안 → 명시적 승인 → 작성 순서를 지킨다.
4. **무한 clarify 금지.** 3라운드까지만 질문하고, 그 이후에는 남은 항목을 Open Questions로 정리할지 사용자에게 묻는다.

## 입력

- `$ARGUMENTS` — 사용자의 초기 아이디어 한두 줄. 비어 있을 수 있다.

## 워크플로우

### 0. 시작 분기

- `$ARGUMENTS`가 비어 있으면 **다른 어떤 도구도 호출하기 전에** 먼저 "어떤 피처/주제를 spec으로 만들고 싶은지" 사용자에게 자유 텍스트로 묻는다. 답을 받기 전에는 글롭/읽기를 하지 않는다.
- 인수가 있으면 그 내용을 그대로 받아 다음 단계로 진행한다. 단, spec 작성에 정보가 부족하다고 판단되면 2단계 clarifying으로 분기한다.

### 1. 컨텍스트 파악 (필요 시, read-only)

처음부터 모든 것을 읽지 않는다. 사용자 아이디어가 잡힌 뒤, **필요할 때 필요한 만큼만** 읽는다.

- 기존 피처의 sub-spec일 가능성이 보이면 → `docs/specs/`를 글롭하고 후보 root-spec 1개 정도만 읽는다.
- 새 피처가 명백하면 → 이 단계를 건너뛰어도 된다.
- `_templates/root-spec.md`, `_templates/sub-spec.md`는 **4단계 파일 작성 직전**에 읽는다. 처음부터 읽지 않는다.
- 인접 코드/문서 읽기는 사용자가 명시적으로 요청하거나, spec의 What/Why 판단에 꼭 필요할 때만. 수정 금지.

### 2. Clarifying 질문 라운드 (최대 3회)

사용자가 이미 충분한 정보를 줬다면 이 단계를 건너뛴다. **정보가 부족하다고 판단될 때만 진입한다.**

각 라운드의 질문은 `AskUserQuestion`으로 묶어 던지거나 자유 텍스트로 직접 물을 수 있다.
- 질문이 3개 이상이거나 선택지가 명확한 옵션 형태면 `AskUserQuestion` 권장.
- 1~2개의 단순 질문이거나 옵션화가 어색하면 자유 텍스트 질문이 자연스럽다.

라운드 사이에는 받은 답을 짧게 요약해 사용자가 보강·수정할 여지를 준다. 한 라운드에 너무 많은 질문을 몰아치지 않는다.

질문 우선순위:
1. **Why** — 안 하면 어떤 비용이 드는가, 누가 이 결과를 관찰하는가.
2. **What 경계** — 무엇이 포함되고 무엇이 제외되는가 (Out of Scope 후보).
3. **Behavior** — 사용자/시스템 관점에서 관찰 가능한 동작.
4. **분해** — 단일 sub-spec으로 충분한가, 여러 개로 쪼개야 하는가.

3라운드가 끝나면 다음을 사용자에게 묻는다:

> 핵심 결정은 충분히 모인 것 같습니다. 남은 모호한 항목은 Open Questions로 남겨두고 spec을 만들까요? 아니면 한 라운드 더 진행할까요?

### 3. 구조 제안

결정사항이 모이면 다음을 **사용자에게 제시하고 명시 승인을 받는다**:

- Feature 이름 (kebab-case 폴더명).
- 새 root-spec(`_index.md`)을 만들지, 기존 피처에 sub-spec을 추가할지.
- Sub-spec이 여러 개라면 각자의 이름과 책임 한 줄, 그리고 권장 구현 순서(파일명 prefix `NN`을 결정하는 근거).
- 작성될 파일 경로 목록.

승인 전에는 어떤 파일도 만들지 않는다.

### 4. 파일 작성

승인 후에만 진행:

- 새 root-spec: `docs/specs/<feature-kebab>/_index.md` — `_templates/root-spec.md`를 베이스로.
- Sub-spec: `docs/specs/<feature-kebab>/specs/<NN>-<sub-name>.md` — `_templates/sub-spec.md`를 베이스로. 헤더의 `Parent` 링크를 정확히 채운다.
  - **`NN`(구현 순서 prefix) 발급 절차**:
    1. 같은 피처에 이미 등록된 sub-spec들의 가장 큰 번호 + 1 을 새 prefix로 부여한다 (zero-pad 2자리, 예: `01`, `02`, …).
    2. 사용자가 명시적으로 사이 삽입(예: 기존 `02` 앞)을 요청하면 영향받는 sub-spec과 그 모든 링크(상호 참조 + `_index.md` Sub-Specs 표)를 함께 재번호한다.
    3. 새 root-spec과 함께 sub-spec 여러 개를 동시에 만들 때는 사용자가 결정한 순서대로 `01`, `02`, … 부여.
- Root-spec의 `Sub-Specs` 표에 신규 sub-spec 행을 추가한다 (둘 다 만든 경우).
- **`docs/specs/README.md` 상태 보드 갱신은 필수.** 새 root-spec을 만든 경우 행을 추가하고, 기존 피처에 sub-spec만 추가한 경우 해당 행의 `Sub-Specs` 카운트를 갱신한다.
- Plan은 만들지 않는다. (`plans/` 디렉토리는 비어 있어도 된다. 필요해지면 `/plan-new` 호출.)

### 5. 마무리

- 작성·갱신된 파일 경로 목록을 짧게 출력한다.
- **commit 권고.** 본 명령의 Write/Edit는 모두 `docs/specs/**` 안에 머무르므로 atomic 단위로 바로 commit하는 것이 자연스럽다. `/spec-implement`는 working tree가 clean해야 plan 실행을 시작하므로, 정리되지 않으면 다음 단계에서 막힌다. 사용자에게 "지금 `git-workflow` skill로 commit할까요?"를 한 번 묻는다 (`AskUserQuestion` 또는 자유 텍스트). 동의하면 그대로 진행, 거절하면 변경 파일 목록만 다시 표시하고 종료. 본 명령은 직접 commit하지 않는다 — 사용자 동의 후 git-workflow skill에 위임만 한다.
- 다음 권장 액션(`/plan-new <spec-path>`)을 한 줄로 안내.

## 출력 형식

각 단계 진행 시 사용자에게 보일 메시지는 한국어, 짧게. 질문은 `AskUserQuestion` 또는 자유 텍스트 둘 다 허용. 어느 쪽이든 한 번에 너무 많은 질문은 피한다.
