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

- `$ARGUMENTS` — 사용자의 초기 아이디어 한두 줄. 비어 있으면 먼저 "어떤 피처를 spec으로 만들고 싶은지" 물어본다.

## 워크플로우

### 1. 컨텍스트 파악 (read-only)

- `docs/specs/`를 글롭해 기존 피처 폴더와 상태 보드를 파악한다.
- `_templates/root-spec.md`, `_templates/sub-spec.md`를 읽어 작성 형식을 확인한다.
- 사용자 아이디어가 기존 피처의 sub-spec인지, 아예 새 피처인지 분기한다. 모호하면 사용자에게 묻는다.
- 필요하면 관련 코드/문서를 *읽기만* 한다 (예: `Assets/`의 기존 구현 확인). 수정 금지.

### 2. Clarifying 질문 라운드 (최대 3회)

각 라운드마다 **3~5개**의 질문을 `AskUserQuestion`으로 한 번에 묶어 던진다. 라운드 사이에는 받은 답을 짧게 요약해 사용자가 보강·수정할 여지를 준다.

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
- Sub-spec이 여러 개라면 각자의 이름과 책임 한 줄.
- 작성될 파일 경로 목록.

승인 전에는 어떤 파일도 만들지 않는다.

### 4. 파일 작성

승인 후에만 진행:

- 새 root-spec: `docs/specs/<feature-kebab>/_index.md` — `_templates/root-spec.md`를 베이스로.
- Sub-spec: `docs/specs/<feature-kebab>/specs/<sub-name>.md` — `_templates/sub-spec.md`를 베이스로. 헤더의 `Parent` 링크를 정확히 채운다.
- Root-spec의 `Sub-Specs` 표에 신규 sub-spec 행을 추가한다 (둘 다 만든 경우).
- Plan은 만들지 않는다. (`plans/` 디렉토리는 비어 있어도 된다. 필요해지면 `/plan-new` 호출.)

### 5. 마무리

- `docs/specs/README.md`의 상태 보드에 행 추가가 필요한지 사용자에게 보고한다 (자동 갱신은 하지 않는다 — 사용자가 직접 결정).
- 작성된 파일 경로 목록과 다음 권장 액션(`/plan-new <spec-path>`)을 짧게 안내한다.

## 출력 형식

각 단계 진행 시 사용자에게 보일 메시지는 한국어, 짧게. 질문은 `AskUserQuestion`으로만 묶어 던지고 자유 텍스트 질문 남발 금지.
