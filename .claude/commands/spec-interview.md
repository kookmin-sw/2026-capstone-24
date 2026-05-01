---
description: 자유 Q&A 인터뷰로 root spec + sub-spec들을 한 번에 박제한다. Open Questions는 박제 시점에 0건이어야 하며, 별도 /spec-resolve 단계 없이 인터뷰 안에서 모두 닫는다. docs/specs/ 외부는 절대 수정하지 않는다.
argument-hint: "[러프한 아이디어 — 한 줄이든 여러 줄이든]"
allowed-tools: Read, Glob, Grep, Write, Edit, AskUserQuestion, Bash, Skill
---

# /spec-interview — 인터뷰 기반 Spec 박제 워크플로우

목적: 사용자의 아이디어를 자유 Q&A로 충분히 확장한 뒤, root-spec + 필요한 sub-spec들을 **한 번의 사용자 확인**으로 박제한다. 인터뷰가 끝나는 시점에 모든 spec의 `## Open Questions` 섹션이 비어 있어야 한다 — 모호한 항목은 인터뷰 안에서 default 후보로 즉답 받아 닫는다. 코드는 읽기만 하고 절대 수정하지 않는다.

## 절대 규칙

1. **수정 허용 경로는 `docs/specs/**`뿐.** `Assets/`, `Packages/`, `ProjectSettings/`, 그 외 모든 코드/직렬화 자산은 수정 금지. (읽기는 허용. 인접 코드 이해를 위해 필요할 때만.)
2. **Spec 본문에 구현 디테일 금지.** 구체적 함수명, 클래스명, 파일 경로, 자료구조, 알고리즘은 plan에 들어간다 — spec 본문에 새어 들어가지 않게 한다.
3. **사용자 승인 없이 파일을 만들지 않는다.** 인터뷰 → 초안 제시 → 사용자 확인 → 작성 순서.
4. **Open Questions 0건 강제.** 박제 시점에 root-spec + 모든 sub-spec의 `## Open Questions` 섹션은 비어 있어야 한다. 모호한 항목은 인터뷰 라운드 안에서 default 후보 2~3개로 분기시켜 즉답 받아 닫는다.
5. **사용자가 답을 모르는 항목**도 추측·임의 결정 금지. 메인 세션이 default 후보 2~3개를 제시해 사용자가 default 채택 시 그 결정을 spec 본문에 그대로 박는다.

## 입력

- `$ARGUMENTS` — 사용자의 초기 아이디어. 한 줄·여러 줄 모두 허용. 비어 있어도 됨.

## 워크플로우

### 0. 시작 분기

- `$ARGUMENTS`가 비어 있으면 **다른 어떤 도구도 호출하기 전에** 자유 텍스트로 묻는다: *"어떤 피처/주제를 spec으로 만들고 싶은가? 한두 줄로 알려달라."* 답을 받기 전에는 글롭/읽기를 하지 않는다.
- 인수가 있으면 그 내용을 컨텍스트로 받아 1단계로 진행.

### 1. 컨텍스트 파악 (필요할 때 read-only)

처음부터 모든 것을 읽지 않는다. 사용자 아이디어가 잡힌 뒤, **필요할 때 필요한 만큼만** 읽는다.

- 기존 피처의 sub-spec일 가능성이 보이면 → `docs/specs/`를 글롭하고 후보 root-spec(`_index.md`) 1개 정도만 읽는다.
- 새 피처가 명백하면 이 단계를 건너뛴다.
- `_templates/root-spec.md`, `_templates/sub-spec.md`는 **3단계 파일 작성 직전**에 읽는다. 처음부터 읽지 않는다.
- 인접 코드/문서 읽기는 사용자가 명시적으로 요청하거나 spec의 What/Why 판단에 꼭 필요할 때만. 수정 금지.

### 2. Q&A 라운드 (제한 없음, 사용자가 답을 줄 때까지)

각 라운드마다 `AskUserQuestion`으로 묶거나 자유 텍스트로 묻는다.
- 질문이 3~4개이거나 선택지가 명확한 옵션 형태면 `AskUserQuestion` 권장.
- 1~2개의 단순 질문이거나 옵션화가 어색하면 자유 텍스트 질문이 자연스럽다.

라운드 사이에는 받은 답을 짧게 요약해 사용자가 보강·수정할 여지를 준다. 한 라운드에 너무 많은 질문을 몰아치지 않는다 (한 라운드 1~4개).

질문 우선순위:
1. **Why** — 안 하면 어떤 비용이 드는가, 누가 이 결과를 관찰하는가.
2. **What 경계** — 무엇이 포함되고 무엇이 제외되는가 (Out of Scope 후보).
3. **Behavior** — 사용자/시스템 관점에서 관찰 가능한 동작.
4. **분해** — 단일 sub-spec으로 충분한가, 여러 개로 쪼개야 하는가. 쪼갠다면 각 sub-spec의 책임 한 줄과 권장 구현 순서(NN prefix).
5. **모호 항목 수렴** — 사용자가 답을 모르는 항목은 default 후보 2~3개로 즉시 분기시켜 수렴.

#### 라운드 종료 판정

다음 5가지가 모두 충족되면 종료 직전 단계로 넘어간다.
- root-spec의 Why / What / 외부 관찰 동작이 명확.
- 각 sub-spec의 책임 한 줄과 NN prefix 순서가 결정됨.
- Out of Scope 항목이 적어도 1~2개 박제됨 (없을 때는 "현 시점 제외 항목 없음" 명시).
- 인터뷰 도중 도출된 모든 모호 항목이 default 후보로 수렴 완료.
- 모든 미결 항목 0건.

### 3. 초안 제시 (단일 사용자 확인 게이트)

`_templates/root-spec.md`, `_templates/sub-spec.md`를 이 시점에 읽는다.

다음을 사용자에게 한 번에 보여준다:

- **Feature 이름** (kebab-case 폴더명).
- **새 root-spec(`_index.md`)을 만들지, 기존 피처에 sub-spec만 추가할지** 분기 명시.
- **작성될 파일 경로 목록** + 각 sub-spec의 NN prefix.
- **모든 spec 본문을 마크다운 블록 1개로 묶어 출력** — root-spec의 Why/What/Sub-Specs 표/Open Questions(빈 표기)/Status, 각 sub-spec의 What/Behavior/Out of Scope/Open Questions(빈 표기)/Implementation Plans(빈 표기) 섹션을 모두 채운 형태.
- 박제 직전 한 줄 사실 확인: *"모든 spec의 Open Questions 섹션은 빈 상태(`_현재 열린 질문 없음._`). 박제 직후 `/spec-build <root-spec> --apply`로 자동 구현 진입 가능."*

`AskUserQuestion`으로 3택을 묻는다:
- **그대로 박제** — 5단계로.
- **일부 수정** — 어디를 어떻게 수정할지 사용자가 자유 텍스트로 답 → 2단계로 회귀해 짧은 보강 라운드.
- **다른 라운드** — 더 큰 변경이 필요. 2단계 처음으로 회귀.

### 4. (예약)

단계 번호 안정화를 위해 비워둔다.

### 5. 파일 작성

승인 후에만 진행:

- 새 root-spec: `docs/specs/<feature-kebab>/_index.md` — `_templates/root-spec.md`를 베이스로.
  - `## Open Questions` 섹션은 `_현재 열린 질문 없음._` 한 줄로 채운다.
- Sub-spec: `docs/specs/<feature-kebab>/specs/<NN>-<sub-name>.md` — `_templates/sub-spec.md`를 베이스로. 헤더의 `Parent` 링크 정확히 채움.
  - **`NN`(구현 순서 prefix) 발급 절차** ([`docs/specs/README.md`](../../docs/specs/README.md) "Sub-Spec 파일명" 단일 진실원):
    1. 같은 피처에 이미 등록된 sub-spec들의 가장 큰 번호 + 1을 새 prefix로 부여한다 (zero-pad 2자리).
    2. 사용자가 사이 삽입을 요청하면 영향받는 sub-spec과 모든 링크(상호 참조 + `_index.md` Sub-Specs 표)를 함께 재번호한다.
    3. 새 root-spec과 함께 sub-spec 여러 개를 동시에 만들 때는 사용자가 결정한 순서대로 `01`, `02`, … 부여.
  - 각 sub-spec의 `## Open Questions` 섹션도 `_현재 열린 질문 없음._` 한 줄.
- Root-spec의 `## Sub-Specs` 표에 sub-spec 행을 추가 (둘 다 만든 경우).
- **`docs/specs/README.md` 상태 보드 갱신은 필수.** 새 root-spec을 만든 경우 행을 추가하고, 기존 피처에 sub-spec만 추가한 경우 해당 행의 `Sub-Specs` 카운트를 갱신한다.
- Plan은 만들지 않는다. (`plans/` 디렉토리는 비어 있어도 된다 — 후속 `/spec-build`가 자동 작성한다.)

### 6. 마무리

- 작성·갱신된 파일 경로 목록을 짧게 출력한다.
- **commit 권고.** 본 명령의 Write/Edit는 모두 `docs/specs/**` 안에 머무르므로 atomic 단위로 바로 commit하는 것이 자연스럽다. 사용자에게 "지금 `git-workflow` skill로 commit할까요?"를 한 번 묻는다 (`AskUserQuestion` 또는 자유 텍스트). 동의하면 그대로 진행, 거절하면 변경 파일 목록만 다시 표시하고 종료. 본 명령은 직접 commit하지 않는다 — 사용자 동의 후 git-workflow skill에 위임만 한다.
- 다음 권장 액션 안내:
  ```
  다음: /spec-build docs/specs/<feature>/_index.md --apply
  ```
  자동 plan drafting → quality review → 구현이 한 호출로 직렬 진행된다. plan 본문은 사용자에게 보여주지 않으며, 구현 도중 manual-hard 검증 시점에만 사용자 결정이 필요하다.

## /spec-resolve 흡수 명시

본 명령은 인터뷰 안에서 모호 항목을 default 후보로 닫으므로 **별도 `/spec-resolve` 라운드를 호출할 필요가 없다.** 다만 기존 `/spec-resolve`는 그대로 살아 있어, 박제된 spec에 후일 새로운 모호 항목이 추가될 경우 그쪽으로 처리한다.

## 출력 형식

각 단계 진행 시 사용자에게 보일 메시지는 한국어, 짧게. 질문은 `AskUserQuestion` 또는 자유 텍스트 둘 다 허용. 어느 쪽이든 한 번에 너무 많은 질문은 피한다.
