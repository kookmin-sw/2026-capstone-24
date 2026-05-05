---
description: sub-spec 한 개를 받아 그 sub-spec의 시스템 설계 윤곽(Tech Spec)을 인터뷰 라운드로 박제한다. /spec-build phase -1이 자동 호출하거나 사용자가 수동 호출한다. ARD 작성 직전 단계로, 결정 자체는 하지 않고 결정해야 할 분기 지점만 골라 ARD로 넘긴다. docs/specs/ 외부는 절대 수정하지 않는다.
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
6. **무한 라운드 금지.** 한 번의 호출에서 최대 3라운드. 남은 항목은 Open Tech Decisions로 유지한다.
7. **commit은 직접 하지 않는다.** 마무리에서 사용자에게 한 번 묻고 동의 시 `git-workflow` skill에 위임만.

## 입력

- `$ARGUMENTS` — 대상 sub-spec 파일 경로 (예: `docs/specs/rhythm-game/specs/02-chart-import.md`). 비어 있으면 사용자에게 묻는다.

## 모드

- **수동 호출 (default)** — 사용자가 직접 호출. 인터뷰 라운드 진행, 마무리 commit 권유.
- **`--auto`** — `/spec-build` phase -1이 inline 답습할 때만 사용. 다음 차이를 갖는다:
  - tech-spec-extractor가 미리 작성해 둔 6 섹션 초안을 입력으로 받는다 (메인 세션이 prompt로 전달).
  - 인터뷰 라운드 0~2회로 단축 (초안이 충분하면 0회로 통과).
  - 마무리 commit 권유 생략 (atomic commit은 spec-build가 처리).

## 워크플로우

### 1. 컨텍스트 적재 (read-only)

순서대로 읽는다.

1. 대상 sub-spec 파일.
2. 같은 feature의 `_index.md` (parent root-spec).
3. 같은 feature의 sibling sub-specs 전부 (cross-cutting 컴포넌트 이름 일관성 확인용).
4. 같은 feature의 기존 `tech-specs/<NN>-*.md` 전부 (있으면).
5. 필요시 관련 코드 read-only Read. Assumptions 박제 시 출처로 인용 (`Read <경로> (YYYY-MM-DD)` 표기).

### 2. 1:1 가드 + skip 가드 점검

- 대상 sub-spec과 1:1 대응되는 `tech-specs/<NN>-*.md`가 이미 존재하면 → 사용자에게 "이미 작성됨. 갱신 모드로 진입할까요?"를 한 번 묻는다. yes면 기존 파일을 갱신 모드로, no면 종료.
- sub-spec 헤더에 `**Tech Spec:** skipped` 박제됐으면 → "이 sub-spec은 영구 skip으로 표시됨. 그래도 작성할까요?"를 한 번 묻는다. yes면 진행, no면 종료.

### 3. 양성 신호 점검 (수동 모드만)

`/spec-build` phase -1이 호출한 `--auto` 모드는 이 단계를 건너뛴다 (이미 tech-spec-extractor가 점검 완료).

수동 호출 모드는 [`tech-spec-extractor`](../agents/tech-spec-extractor.md)의 양성 신호 3종을 사용자에게 짧게 보여주고 "이 sub-spec에 Tech Spec이 정말 필요한가"를 한 번 확인한다. 사용자가 no면 종료.

### 4. 인터뷰 라운드 (최대 3회)

각 라운드마다 `AskUserQuestion`으로 항목 1~4개를 묶어 묻는다. 6 섹션을 라운드별로 분배:

- **라운드 1**: Components + Data/Control Flow.
- **라운드 2**: Boundaries + Invariants.
- **라운드 3**: Assumptions + Open Tech Decisions.

질문 작성 가이드:

- 한 항목 = 한 질문. 옵션 형태가 자연스러우면 옵션화, 자유 텍스트가 자연스러우면 자유 텍스트.
- 사용자가 답하기 어려운 추상적 항목은 default 후보 2~3개로 쪼개 제시 (`/spec-interview` 패턴).
- Open Tech Decisions 후보가 도출되면 그 항목이 ARD로 넘어갈 분기인지 확인 — 즉, "Tech Spec에서 닫을 수 있는 사실"인지 "ARD에서 결정해야 할 분기"인지.

`--auto` 모드는 tech-spec-extractor의 초안이 충분하면 라운드 0~2회로 단축한다. 초안이 모든 섹션을 채우고 Open Tech Decisions 후보가 1+개면 라운드 0회 (5단계 직진).

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

## /spec-build phase -1과의 관계

`/spec-build`가 per-sub-spec 루프 3-2(ARD/phase 0) 직전에 phase -1으로 본 워크플로우를 inline 답습한다. 그때는 `--auto` 모드로 동작하며, 메인 세션이 직접 step 1·2·4·5를 inline 실행한다 (Task로 본 command를 재호출하지 않음 — 컨텍스트 중첩 회피). step 3 양성 신호 점검은 tech-spec-extractor sub-agent가 phase -1 진입 직전에 수행.

수동 호출은 phase -1 게이트를 우회해 사용자가 직접 sub-spec 1개에 Tech Spec을 박을 때 사용한다.

## 출력 형식

진행 메시지는 한국어, 짧게. 질문은 `AskUserQuestion` 또는 자유 텍스트 둘 다 허용. 어느 쪽이든 한 번에 너무 많은 질문은 피한다.
