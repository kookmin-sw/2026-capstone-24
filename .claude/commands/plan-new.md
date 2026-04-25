---
description: 기존 spec을 받아 self-contained한 구현 plan을 작성한다. 전역 일련번호를 자동 부여하고 spec과 양방향 링크를 맺는다.
argument-hint: "<spec 파일 경로>"
allowed-tools: Read, Glob, Grep, Write, Edit, AskUserQuestion
---

# /plan-new — Plan 작성 워크플로우

목적: 주어진 spec(보통 sub-spec)을 읽어 **How를 담은 self-contained plan**을 작성한다. 한 plan = 한 세션 분량. 코드는 읽기만 하고 수정하지 않는다.

## 절대 규칙

1. **수정 허용 경로는 `docs/specs/**`뿐.** 그 외는 읽기 전용.
2. **Plan은 self-contained.** 다른 plan이나 이전 세션 컨텍스트를 가정하지 않는다. 필요한 배경은 `Context` 섹션에 적어 넣는다.
3. **사용자 승인 없이 plan 파일을 만들지 않는다.** 분할안 제안 → 승인 → 작성 순서를 지킨다.
4. **번호 충돌 시 사용자에게 보고하고 멈춘다.** 임의 재발급 금지.

## 입력

- `$ARGUMENTS` — 대상 spec 파일 경로 (예: `docs/specs/rhythm-game/specs/scoring.md`). 비어 있으면 사용자에게 묻는다.

## 워크플로우

### 1. Spec 컨텍스트 적재

순서대로 읽는다:

1. 대상 spec 파일.
2. 같은 feature의 `_index.md` (parent root-spec).
3. 같은 sub-spec의 `Implementation Plans` 표에 이미 등록된 plan들 (있으면 모두 읽어 중복/연속성 파악).
4. `_templates/plan.md` (작성 형식 확인).

필요하면 관련 코드/문서를 *읽기만* 한다.

### 2. 분할 제안

다음을 사용자에게 제시하고 **명시 승인을 받는다**:

- 이 spec을 **plan 1개**로 충분히 다룰 수 있는지, 아니면 **여러 plan으로 쪼개야 하는지**.
- 여러 개라면 각 plan의 제목, 한 줄 책임, 권장 실행 순서.
- 각 plan의 대략적 분량(파일 수/난이도) 추정.

판단 기준:
- 한 plan은 "한 세션에 끝낼 수 있는" 크기.
- 자연스러운 의존성(예: 데이터 → 로직 → UI)이 있으면 그 경계로 자른다.
- 검증 가능한 Acceptance Criteria가 plan 단위로 나오는지 확인.

### 3. 번호 발급

승인 후, 각 plan에 전역 일련번호를 부여:

1. `Glob "docs/specs/**/plans/*.md"`로 기존 plan 전부 수집.
2. 파일명 prefix(예: `042-foo.md`)에서 숫자만 추출. 추출 실패하는 파일이 있으면 사용자에게 보고하고 멈춘다.
3. 최댓값 + 1부터 순서대로 부여. 없으면 `001`부터.
4. 한 번의 `/plan-new` 실행 안에서 여러 plan을 만들 때는 **연속 번호**를 쓴다 (예: 042, 043, 044).
5. 충돌(같은 번호 파일이 이미 있음)이 감지되면 즉시 멈추고 사용자에게 보고.

### 4. 파일 작성

승인 후에만 진행:

- 경로: `docs/specs/<feature>/plans/NNN-<kebab>.md`.
- `_templates/plan.md`를 베이스로 채운다.
- `Linked Spec`은 대상 spec 파일을 상대경로로 정확히 가리킨다 (`../specs/<sub>.md` 형태).
- `Status`는 `Ready`로 시작.
- `Context` 섹션은 다른 세션에서 이 파일만 읽고도 작업을 시작할 수 있을 만큼 충분한 배경을 담는다 (Linked Spec 핵심 요약, 현재 코드 상태, 제약, 의사결정 근거).
- `Approach`는 단계별로. `Deliverables`는 생성/수정될 파일 경로 목록.
- `Acceptance Criteria`는 검증 가능한 항목만 적는다 (모호한 표현 금지).

### 5. 역링크 갱신

- 대상 sub-spec 파일의 `Implementation Plans` 표에 새 plan 행을 **Edit**로 추가한다.
  - 행 형식: `| NNN | <Plan Title> | Ready | [NNN-<kebab>.md](../plans/NNN-<kebab>.md) |`
  - 기존 표가 `_아직 없음_` 행만 갖고 있다면 그 행을 새 행으로 대체한다.
- 여러 plan을 한 번에 만든 경우 모두 추가.

### 6. 마무리

- 작성된 plan 파일 경로 목록과 권장 실행 순서를 사용자에게 짧게 안내한다.
- "각 plan을 실행하려면 plan 파일 경로를 새 세션의 Claude에게 전달하면 된다 (Linked Spec과 parent `_index.md`까지 자동으로 읽도록 AGENTS.md에 규칙이 적혀 있음)"는 안내를 한 번 추가한다.

## 출력 형식

진행 메시지는 한국어, 짧게. 질문은 `AskUserQuestion`으로만 묶는다.
