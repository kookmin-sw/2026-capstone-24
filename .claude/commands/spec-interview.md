---
description: 자유 Q&A 인터뷰로 root spec + sub-spec들 + (양성 신호 발화 시) Tech Spec + ARD를 한 번에 박제한다. Tech Spec 양성 신호 점검·Prefab 구조 박제·ARD 후보 추출을 한 번의 사용자 확인 게이트로 묶는다. Open Questions는 박제 시점에 0건이어야 하며, 인터뷰 안에서 모두 닫는다. docs/specs/ 외부는 절대 수정하지 않는다.
argument-hint: "[러프한 아이디어 — 한 줄이든 여러 줄이든]"
allowed-tools: Read, Glob, Grep, Write, Edit, AskUserQuestion, Bash, Skill, Task
---

# /spec-interview — 인터뷰 기반 Spec+Tech Spec+ARD 박제 워크플로우

목적: 사용자의 아이디어를 자유 Q&A로 충분히 확장한 뒤, root-spec + sub-spec들 + (양성 신호 발화 시) Tech Spec + ARD까지 **한 번의 사용자 확인**으로 모두 박제한다.

## 절대 규칙

1. **수정 허용 경로는 `docs/specs/**`뿐.** `Assets/`, `Packages/`, `ProjectSettings/`, 그 외 모든 코드/직렬화 자산은 수정 금지. (읽기는 허용.)
2. **Spec 본문에 구현 디테일 금지.** 구체적 함수명, 클래스명, 파일 경로, 자료구조, 알고리즘은 Tech Spec/ARD에 들어간다 — spec 본문에 새어 들어가지 않게 한다.
3. **사용자 승인 없이 파일을 만들지 않는다.** 인터뷰 → 통합 초안 제시 → 사용자 확인 → 작성 순서.
4. **Open Questions 0건 강제.** 박제 시점에 root-spec + 모든 sub-spec의 `## Open Questions`는 비어 있어야 한다. 모호 항목은 인터뷰 라운드 안에서 후보 2~3개로 분기시켜 즉답 받아 닫는다.
5. **Tech Spec/ARD는 양성 신호 발화 시에만 작성.** 신호 0건이면 둘 다 skip.

## 입력

- `$ARGUMENTS` — 사용자의 초기 아이디어. 한 줄·여러 줄 모두 허용. 비어 있어도 됨.

## 워크플로우

### 0. 시작 분기

- `$ARGUMENTS`가 비어 있으면 **다른 어떤 도구도 호출하기 전에** 자유 텍스트로 묻는다: *"어떤 피처/주제를 spec으로 만들고 싶은가?"* 답을 받기 전에는 Glob/Read를 하지 않는다.
- 인수가 있으면 그 내용을 컨텍스트로 받아 1단계로 진행.

### 1. 컨텍스트 파악 (필요할 때 read-only)
- 기존 피처의 sub-spec일 가능성이 보이면 → `docs/specs/`를 Glob하고 후보 root-spec(`_index.md`) 1개 정도만 읽는다.
- 새 피처가 명백하면 이 단계를 건너뛴다.

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

다음 5가지가 모두 충족되면 2.5단계로 넘어간다.
- root-spec의 Why / What / 외부 관찰 동작이 명확.
- 각 sub-spec의 책임 한 줄과 NN prefix 순서가 결정됨.
- Out of Scope 항목이 적어도 1~2개 박제됨 (없을 때는 "현 시점 제외 항목 없음" 명시).
- 인터뷰 도중 도출된 모든 모호 항목이 default 후보로 수렴 완료.
- 모든 미결 항목 0건.

### 2.5. 설계 결정 추출 (Tech Spec + ARD 통합 게이트)


#### 2.5-1. Tech Spec 양성 신호 4종 점검

각 sub-spec에 대해:
1. **신규 클래스/컴포넌트 2개 이상 + 그들 사이 통신·의존**이 있는가?
2. **기존 클래스의 public API 접속 또는 frame loop·event 구독에 끼어들기**가 필요한가?
3. **데이터/제어 흐름이 한 컴포넌트 안에서 닫히지 않는가**?
4. **Comparable Siblings** — `Assets/` 또는 `docs/specs/` 산하에 동급 자산이 1+개 존재하는데 sub-spec 본문에 비교 박제가 없는가?

1건이라도 발화하면 `AskUserQuestion`으로 Tech Spec 작성 여부 2택:
- **yes** → 2.5-2로.
- **no** → Tech Spec·ARD 둘 다 만들지 않음. 바로 3단계로.

양성 신호 0건이면 Tech Spec 단계 통째로 skip하고 2.5-3로.

#### 2.5-2. Tech Spec 인터뷰 (yes 선택 시)

`_templates/tech-spec.md`를 Read. Q&A 라운드 0~2회로 7섹션(Components, Data/Control Flow, Boundaries, Invariants, Assumptions, Comparable Siblings, Open Tech Decisions)을 채운다. `Open Tech Decisions` 항목은 2.5-3 ARD 후보 시드로 자동 승격.

**Prefab 구조 박제 (필수, sub-spec이 prefab 계층에 의존할 때).**

- MCP 사용 가능: `unity-scene-reader` Pattern A를 1회 호출 → 반환 `data.hierarchy[]`를 Tech Spec `## Components` 또는 `## Assumptions`의 "**Prefab Hierarchy**" 서브섹션에 박제. 출처는 `unity-scene-reader Pattern A (YYYY-MM-DD)` 표기.
- MCP 미가용: 사용자에게 "MCP 없이 진행할까요?"를 묻고 yes면 `.prefab` YAML을 Read해 계층만 발췌 박제 + 정확도 낮음 1줄 경고. no면 인터뷰 멈춤.

#### 2.5-3. ARD 후보 추출

각 sub-spec에 대해 도메인 CLAUDE.md + spec 본문(+ Tech Spec이 있으면 Open Tech Decisions 우선)으로 **사용자 입력이 필요한 설계 결정 후보 0~5개**를 메인이 추출한다. 각 후보:

- **결정 이름** (kebab-case, ARD 파일명 후보)
- **배경 한 줄**
- **options 2~3개**, 각 option은 spec의 `## What` 항목 1:1 매핑한 `spec_what_coverage`를 함께 박제 ("만족 못 함" 옵션은 라벨 끝 ⚠️).
- **recommended** — `spec_what_coverage`가 모두 "만족"인 옵션 우선.

추출된 후보를 `AskUserQuestion`으로 batch 질문(한 번에 최대 4개씩). 사용자 답을 받아 ARD 본문에 박을 `## Decision` + `## Rationale` + `## Spec What Coverage` + `## Consequences` 골격을 구성한다.

모든 옵션이 ⚠️인 경우 경고 한 줄을 사용자에게 표시하고 spec `## What` 재검토 여부 1회 확인.

후보 0개면 ARD 작성 없이 다음 단계로.

### 3. 통합 초안 제시 (단일 사용자 확인 게이트)

이 시점에 `_templates/root-spec.md`, `_templates/sub-spec.md`, (필요 시) `_templates/tech-spec.md` + `_templates/decision.md`를 Read.

다음을 사용자에게 한 번에 보여준다:

- **Feature 이름** (kebab-case 폴더명).
- **새 root-spec(`_index.md`)을 만들지, 기존 피처에 sub-spec만 추가할지** 분기 명시.
- **작성될 파일 경로 목록** — root-spec, 각 sub-spec(NN prefix 포함), 각 Tech Spec(있으면), 각 ARD(있으면).
- **모든 본문을 마크다운 블록 1개로 묶어 출력** — root-spec(Why/What/Sub-Specs 표/Open Questions 빈 표기/Status), 각 sub-spec(What/Behavior/Out of Scope/Open Questions 빈 표기/Implementation Plans 빈 표기), 각 Tech Spec(7섹션 + Prefab Hierarchy), 각 ARD(Decision/Rationale/Spec What Coverage/Consequences).
- 박제 직전 한 줄 사실 확인: *"모든 spec의 Open Questions는 빈 상태. 박제 직후 `/spec-build <root-spec> --apply`로 자동 구현 진입 가능."*

`AskUserQuestion`으로 3택:
- **그대로 박제** → 5단계로.
- **일부 수정** — 어디를 어떻게 수정할지 사용자가 자유 텍스트로 답 → 2단계로 회귀해 짧은 보강 라운드.
- **다른 라운드** — 더 큰 변경이 필요. 2단계 처음으로 회귀.

### 4. (예약)

단계 번호 안정화를 위해 비워둔다.

### 5. 파일 작성

승인 후에만 진행:

- 새 root-spec: `docs/specs/<feature-kebab>/_index.md` — `_templates/root-spec.md` 베이스. `## Open Questions`는 `_현재 열린 질문 없음._` 한 줄.
- Sub-spec: `docs/specs/<feature-kebab>/specs/<NN>-<sub-name>.md` — `_templates/sub-spec.md` 베이스. `Parent` 링크 정확히 채움. `## Open Questions`는 `_현재 열린 질문 없음._` 한 줄.
  - `NN` 발급: 같은 피처에 이미 등록된 sub-spec들의 가장 큰 번호 + 1. 새 root-spec과 함께 sub-spec 여러 개 만들 때는 사용자가 결정한 순서대로 `01`, `02`, …
  - Tech Spec을 skip-permanently로 선택한 sub-spec은 헤더에 `**Tech Spec:** skipped` 한 줄 박제.
- Tech Spec: `docs/specs/<feature-kebab>/tech-specs/<NN>-<title>.md` — `_templates/tech-spec.md` 베이스. NN은 대응 sub-spec과 동일. 7섹션 + (해당 시) Prefab Hierarchy 박제.
- ARD: `docs/specs/<feature-kebab>/decisions/<NN>-<title>.md` — `_templates/decision.md` 베이스. NN은 같은 feature의 decisions/ 내 가장 큰 NN + 1 (없으면 01). Tech Spec에서 도출된 ARD면 헤더에 `**From Tech Spec:** <path> §Open Tech Decisions #N` 박제. 동시에 Tech Spec의 대응 항목 끝에 `→ decisions/<NN>-*.md` 한 줄 append Edit.
- Root-spec의 `## Sub-Specs` 표에 sub-spec 행 추가 (둘 다 만든 경우).
- **`docs/specs/README.md` 상태 보드 갱신 필수.** 새 root-spec이면 행 추가, 기존 피처에 sub-spec만 추가면 카운트 갱신.
- Plan은 만들지 않는다. (`plans/` 디렉토리는 비어 있어도 된다 — `/spec-build`가 자동 작성.)

### 6. 마무리

- 작성·갱신된 파일 경로 목록을 짧게 출력한다.
- **commit 권고.** 본 명령의 Write/Edit는 모두 `docs/specs/**` 안에 머무르므로 atomic 단위로 바로 commit하는 것이 자연스럽다. 사용자에게 "지금 `git-workflow` skill로 commit할까요?"를 한 번 묻는다. 동의하면 그대로 진행, 거절하면 변경 파일 목록만 다시 표시하고 종료. 본 명령은 직접 commit하지 않는다.
- 다음 권장 액션 안내:
  ```
  다음: /spec-build docs/specs/<feature>/_index.md --apply
  ```

## 출력 형식

각 단계 진행 시 사용자에게 보일 메시지는 한국어, 짧게. 질문은 `AskUserQuestion` 또는 자유 텍스트 둘 다 허용. 어느 쪽이든 한 번에 너무 많은 질문은 피한다.
