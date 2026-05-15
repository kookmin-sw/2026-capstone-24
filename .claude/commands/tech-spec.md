---
description: sub-spec 한 개를 받아 자유 Q&A 인터뷰로 그 sub-spec의 시스템 설계 윤곽(Tech Spec)을 박제한다. /spec-build phase -1이 자동 호출하거나 사용자가 수동 호출한다. ARD 작성 직전 단계로, 결정 자체는 하지 않고 결정해야 할 분기 지점만 골라 ARD로 넘긴다. 라운드는 `/spec-interview` 사상을 답습해 무제한, 박제 직전 단일 사용자 확인 게이트 1회. docs/specs/ 외부는 절대 수정하지 않는다.
argument-hint: "<sub-spec 파일 경로>"
allowed-tools: Read, Glob, Grep, Edit, Write, AskUserQuestion, Bash, Task, Skill
---

# /tech-spec — Tech Spec 인터뷰 박제 워크플로우

목적: 한 sub-spec에 대해 사용자와 인터뷰 라운드로 시스템 설계 윤곽(컴포넌트, 데이터/제어 흐름, 경계, 불변식, 가정)을 박제하고, 닫지 않은 분기 지점은 Open Tech Decisions로 모아 후속 ARD에 시드한다. 결정 자체는 하지 않는다 — 그건 ARD의 책임.

## 절대 규칙

1. **수정 허용 경로는 `docs/specs/**`뿐.** `Assets/`, `Packages/`, `ProjectSettings/`, 그 외 모든 코드/직렬화 자산은 수정 금지. (읽기는 허용. 인접 코드 이해를 위해 필요할 때만.)
2. **본문에 옵션 비교/선택 문장 금지.** "A vs B 중 A 채택" 같은 분기 결정은 ARD에서 다룬다. Tech Spec은 *서술*만, ARD는 *분기 결정*.
3. **본문에 알고리즘·구현 디테일 금지.** 함수 시그니처·의사코드·필드 레이아웃은 plan으로 미룬다.
4. **sub-spec 1개 ↔ Tech Spec 1개 1:1 강제.** 같은 sub-spec에 Tech Spec 2개 이상 만들지 않는다 — 그 신호가 보이면 sub-spec을 쪼개라고 사용자에게 알린다.
5. **사용자 결정 직후 즉시 Edit 적용.** AskUserQuestion으로 받은 결정을 그대로 본문에 반영하고 별도 "이 Edit을 적용해도 될까요?" 식의 명시 승인 라운드를 추가하지 않는다 (`/spec-resolve`와 동일).
6. **자유 Q&A 우선.** 한 라운드에 미리 정해진 섹션을 묶어 옵션화하지 않는다. 사용자가 자기 안을 먼저 자유 텍스트로 던지면 모델이 평가·검증·코드 점검·반박·강화로 응답한다. `AskUserQuestion`은 분기가 명백히 옵션화되고 사용자가 답하기 어려운 항목에만 사용. 라운드 수 제한 없음.
7. **commit은 직접 하지 않는다.** 마무리에서 사용자에게 한 번 묻고 동의 시 `git-workflow` skill에 위임만.

## 입력

- `$ARGUMENTS` — 대상 sub-spec 파일 경로 (예: `docs/specs/rhythm-game/specs/02-chart-import.md`). 비어 있으면 사용자에게 묻는다.

## 모드

- **수동 호출 (default)** — 사용자가 직접 호출. 인터뷰 라운드 진행, 마무리 commit 권유.
- **`--auto`** — `/spec-build` phase -1이 inline 답습할 때만 사용. 다음 차이를 갖는다:
  - spec-design-extractor가 미리 작성해 둔 7 섹션 초안을 입력으로 받는다 (메인 세션이 prompt로 전달).
  - 인터뷰 라운드 0~2회로 단축 (초안이 충분하면 0회로 통과).
  - 마무리 commit 권유 생략 (atomic commit은 spec-build가 처리).

## 워크플로우

### 1. 컨텍스트 적재 (read-only, lazy)

처음부터 모든 것을 읽지 않는다. 시작 시점엔 최소만 읽고, 인터뷰 도중 분기 평가에 필요할 때 추가로 읽는다 (`/spec-interview` §1 사상 답습).

1. **시작 시점**: 대상 sub-spec 파일 + 같은 feature의 `_index.md`(parent root-spec) 1회 read.
2. **(lazy)** 같은 feature의 sibling sub-specs / 기존 `tech-specs/<NN>-*.md` / 인접 코드는 인터뷰 도중 분기 평가에 필요할 때만 read. Assumptions 박제 시 출처로 인용 (`Read <경로> (YYYY-MM-DD)` 표기).

### 2. 1:1 가드 + skip 가드 점검

- 대상 sub-spec과 1:1 대응되는 `tech-specs/<NN>-*.md`가 이미 존재하면 → 사용자에게 "이미 작성됨. 갱신 모드로 진입할까요?"를 한 번 묻는다. yes면 기존 파일을 갱신 모드로, no면 종료.
- sub-spec 헤더에 `**Tech Spec:** skipped` 박제됐으면 → "이 sub-spec은 영구 skip으로 표시됨. 그래도 작성할까요?"를 한 번 묻는다. yes면 진행, no면 종료.

### 3. 양성 신호 점검 (수동 모드만)

`/spec-build` 통합 설계 게이트가 호출한 `--auto` 모드는 이 단계를 건너뛴다 (이미 spec-design-extractor가 점검 완료).

수동 호출 모드는 [`spec-design-extractor`](../agents/spec-design-extractor.md)의 양성 신호 4종(3종 + Comparable Siblings 누락)을 사용자에게 짧게 보여주고 "이 sub-spec에 Tech Spec이 정말 필요한가"를 한 번 확인한다. 사용자가 no면 종료.

### 4. 자유 Q&A 인터뷰 (라운드 무제한)

`/spec-interview`의 운영 사상을 답습한다. 라운드별 주제 강제 분배 없음. 한 번에 옵션 3~4개를 `AskUserQuestion`으로 묶어 던지지 않는다.

#### 4-1. 시작 제안 (1회)

양성 신호 점검 yes 직후 모델은 sub-spec을 한 번 더 정리해 다음을 짧게 출력한다.

- 이 sub-spec에서 모델이 미리 본 분기 후보 1~3개 (자유 텍스트, 옵션 묶음 아님).
- 사용자에게 묻기: *"어디서 시작할까요? 다른 안이 있으면 자유 텍스트로 알려주세요."*

질문은 자유 텍스트 또는 1건의 `AskUserQuestion`. 사용자가 자기 안을 먼저 자유 텍스트로 던지면 모델은 옵션 묶음을 폐기한다.

#### 4-2. 자유 Q&A 라운드 (제한 없음)

각 라운드마다:

- 사용자 자유 텍스트 답을 받음 → 모델이 평가·검증·코드 점검·반박·강화로 응답한다.
- 라운드 끝에 짧은 누적 요약 1~3줄 + 다음에 풀 분기 1개 제안.
- 모델이 옵션을 미리 정해 묶어 던지지 않는다. 사용자가 자유 텍스트로 답하기 어려운 분기(예: 카테고리·범위)에 한해서만 `AskUserQuestion` 1건. 묶음 3~4개 강제 안 함.
- Open Tech Decisions 후보가 도출되면 그 항목이 ARD로 넘어갈 분기인지 확인 — 즉, "Tech Spec에서 닫을 수 있는 사실"인지 "ARD에서 결정해야 할 분기"인지.

질문 우선순위 (참고용, 강제 분배 아님):

1. 메커니즘 / 핵심 데이터·제어 흐름 (Components·Data/Control Flow에 들어갈 사실)
2. 경계 (Boundaries)
3. 깨지면 안 되는 사실 (Invariants)
4. 외부에서 받아오는 사실 (Assumptions, 출처 표기 가능)
5. 닫지 않는 분기 (Open Tech Decisions, ARD 후보)

#### 4-3. 라운드 종료 판정

다음 6가지가 모두 충족되면 5-0(박제 직전 단일 게이트)으로 진입한다.

- **Components**: 등장하는 컴포넌트(신규/기존)가 한 줄 역할로 1+개씩 명시.
- **Data/Control Flow**: 단일 시퀀스 또는 보조 시퀀스 1+개로 frame/event 단위 흐름이 서술 가능.
- **Boundaries**: "건드린다" 1+개, "건드리지 않는다" 1+개.
- **Invariants**: 1+개. plan-drafter가 깨면 안 되는 사실.
- **Assumptions**: 1+개. 출처 표기(`Read <경로> (YYYY-MM-DD)` 등) 가능한 외부 사실.
- **Comparable Siblings (필수)**: 본 sub-spec의 대상이 기존 자산(예: Piano/DrumKit 같은 동급 악기, 또는 같은 카테고리의 다른 sub-spec)과 동일 카테고리이면, **§Comparable Siblings 표**를 박제한다. 컬럼: `대상 자산` / `해당 sub-spec의 산출물` / `차이점 1줄`. 동급 자산이 없으면 `_해당 없음 — 신규 카테고리_`를 한 줄로 명시. 표도 면제 명시도 모두 비어 있으면 5-0 게이트 fail. (실제 사례: Trombone Anchor가 Piano/DrumKit의 `BoxCollider size` / `teleportAnchorTransform` / `InstrumentTeleportColliderBinder` 셋을 누락한 채 spec 통과한 사고를 차단한다.)

Open Tech Decisions는 0~N개 어떤 값이든 라운드 종료 판정에 영향을 주지 않는다 (없어도 박제 가능, 있으면 각 항목이 후속 ARD 1건과 1:1).

`--auto` 모드는 spec-design-extractor의 7 섹션 초안이 충분하면 4-1·4-2를 0회로 통과해 5-0으로 직진한다.

### 5-0. 박제 직전 단일 게이트

`/spec-interview` §3 답습. 4단계가 끝나면 곧장 파일을 작성하지 않고, 사용자 검토 1회를 받는다.

- 7 섹션 본문을 모두 채운 마크다운 블록 1개로 사용자에게 보여준다.
- 옆에 박제 직전 사실 확인 한 줄: *"Open Tech Decisions N건. 박제 직후 `/spec-build <root-spec> --apply`로 통합 설계 게이트의 ARD 단계 진입 가능."*
- `AskUserQuestion`으로 3택:
  - **그대로 박제** → 5단계로.
  - **일부 수정** → 사용자 자유 텍스트 답 → 4-2로 회귀해 짧은 보강 라운드 1회.
  - **다른 라운드** → 4-1 또는 4-2 처음으로 회귀.

`--auto` 모드는 본 게이트를 생략하지 않는다. spec-design-extractor 초안만으로 박제하지 않는다 — 라운드 0회로 통과해도 본 게이트 1회는 받는다.

### 5. 파일 작성

승인 후에만 진행:

- 신규: `docs/specs/<feature>/tech-specs/<NN>-<title>.md` — `_templates/tech-spec.md`를 베이스로.
  - `<NN>` = 대응하는 sub-spec과 동일한 zero-pad 2자리. NN 미부여 sub-spec(예: rhythm-game)이면 sub-spec 파일명 베이스(`<sub-name>.md`)를 사용한다.
  - `<title>` = sub-spec과 같은 폴더 안에서 식별만 되면 됨. kebab-case.
  - `Status: Draft`로 시작. 인터뷰 종료 + 모든 6 섹션 채움 + Open Tech Decisions 후보 1+개 닫힘 또는 ARD 시드 결정 확정 시 `Accepted`로 갱신.
- 갱신 모드면 기존 파일을 최소 범위 Edit. 본문 전체 재작성 금지.

### 6. sub-spec 역링크 갱신

sub-spec 본문에는 역링크를 박지 않는다 (ARD 패턴 답습 — ARD도 sub-spec 본문에 역링크 안 박힘). Tech Spec → sub-spec 단방향만 유지해 sub-spec 톤을 보존한다.

다만 `skip-permanently` 결정의 흔적을 남겨야 할 때만 sub-spec 헤더에 `**Tech Spec:** skipped` 한 줄 박제 (이 케이스는 phase -1에서만 발생, `/tech-spec` 자체는 건드리지 않음).

### 7. 마무리

- 작성·갱신된 파일 경로 + 닫힌 Open Tech Decisions / 남은 Open Tech Decisions 짧게 요약.
- **남은 Open Tech Decisions가 있으면**: 후속 `/spec-build` 또는 ARD 작성에서 시드된다는 점 안내.
- **commit 권고 (수동 모드만)** — `/spec-resolve`와 동일 패턴. 사용자에게 "지금 `git-workflow` skill로 commit할까요?"를 한 번 묻는다. 동의하면 그대로 진행, 거절하면 변경 파일 목록만 다시 표시하고 종료. 본 명령은 직접 commit하지 않는다 — 사용자 동의 후 git-workflow skill에 위임만 한다.
- 다음 권장 액션 안내:
  ```
  다음: /spec-build docs/specs/<feature>/_index.md --apply
  ```
  Tech Spec이 phase 0(ARD)·phase 1(plan-drafter) 입력으로 자동 전달된다.

## /spec-build 통합 설계 게이트와의 관계

`/spec-build`가 per-sub-spec 루프 안에서 ARD 단계 직전에 본 워크플로우를 inline 답습한다. 그때는 `--auto` 모드로 동작하며, 메인 세션이 직접 step 1·2·4·5-0·5를 inline 실행한다 (Task로 본 command를 재호출하지 않음 — 컨텍스트 중첩 회피). step 3 양성 신호 점검은 spec-design-extractor sub-agent가 통합 게이트 진입 시점에 1회 수행하며, ARD 후보 추출과 한 호출로 처리된다.

inline 답습 시에도 "5-0. 박제 직전 단일 게이트"는 메인 세션이 그대로 답습한다. spec-design-extractor 초안만으로 박제하지 않는다.

수동 호출은 통합 게이트를 우회해 사용자가 직접 sub-spec 1개에 Tech Spec을 박을 때 사용한다.

## 출력 형식

진행 메시지는 한국어, 짧게. 질문은 `AskUserQuestion` 또는 자유 텍스트 둘 다 허용. 어느 쪽이든 한 번에 너무 많은 질문은 피한다.
