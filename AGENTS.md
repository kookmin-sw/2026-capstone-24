# VirtualMusicStudio Unity 에이전트 가이드

## 프로젝트
- 주요 씬: `Assets/Scenes/SampleScene.unity`
- Unity 6000.3.10f1 / URP / Unity MCP: `com.coplaydev.unity-mcp`

## 상시 규칙
- 기본적으로 한국어로 답한다.
- 정합 대상이 씬/프리팹 같은 Unity 직렬화 자산이면 `docs/agent-rules/serialized-assets.md`를 읽고 작업한다.

## Plan 파일 규칙
- 공식 plan은 `docs/specs/<feature>/plans/<YYYY-MM-DD>-<author>-<slug>.md` 형식으로 관리한다. 새 plan 작성은 `/plan-new`를 사용한다.
- Plan 작성 전 대상 spec의 `Open Questions`가 많이 남아 있으면 `/spec-resolve`로 먼저 닫기를 권장한다 (포맷 결정 같은 핵심 질문이 미결이면 plan을 다시 써야 할 가능성이 큼).
- 사용자가 **저장된 plan대로 구현해달라고 요청**하면, 먼저 해당 plan 파일을 읽고 → `Linked Spec` → parent `_index.md` 순으로 컨텍스트를 적재한 뒤 작업을 진행한다.
- `docs/plans/PLAN.md` 같은 ad-hoc 임시 plan 노트는 더 이상 권장하지 않는다. 1회성 메모가 필요하면 대화 안에서 처리한다.

## 작업 유형별 규칙
작업 시작 전 필요한 파일을 읽을 것.

| 작업 유형 | 규칙 파일 |
|---|---|
| C# 스크립트 작성/수정 | @docs/agent-rules/coding.md |
| 씬/프리팹/직렬화 자산 수정 | @docs/agent-rules/serialized-assets.md |
| Git 관련 작업(브랜치명 추천/생성, 커밋, PR) | @docs/agent-rules/git.md |
| Spec/Plan 문서 작성·수정 | @docs/specs/README.md, `.claude/commands/spec-new.md`, `.claude/commands/spec-resolve.md`, `.claude/commands/plan-new.md` |
| Plan 구현 완료 후 상태 갱신 | @docs/agent-rules/plan-lifecycle.md |

## Spec 시스템

What/Why를 담는 얇은 **spec**과 How를 담는 실행 가능한 **plan**을 분리해서 관리한다. 진입점은 [`docs/specs/README.md`](docs/specs/README.md).

### 폴더 구조

```
docs/specs/
├── README.md                       # 인덱스 + 상태 보드
├── _templates/                     # 새 spec/plan 작성 시 베이스
│   ├── root-spec.md
│   ├── sub-spec.md
│   └── plan.md
└── <feature-kebab>/
    ├── _index.md                   # 루트 spec
    ├── specs/<sub-name>.md         # 하위 spec
    └── plans/<YYYY-MM-DD>-<author>-<slug>.md  # 구현 plan (날짜·작성자·slug 기반 파일명)
```

### Plan 실행 시 읽기 순서

사용자가 plan 경로를 주고 "구현해" 라고 하면 다음 순서로 컨텍스트를 적재한다.

1. **Plan 파일** — Goal / Context / Approach / Acceptance Criteria 파악.
2. **Linked Spec** — 해당 sub-spec 또는 root-spec.
3. **parent `_index.md`** — 피처 전체의 Why / What / 다른 sub-spec들과의 관계.
4. 그 후 구현 시작.

### 작성 anti-pattern (금지)

- Spec 본문에 함수명, 클래스명, 파일 경로, 알고리즘 같은 **구현 디테일** 넣기.
- 한 spec 폴더에 무관한 피처 섞기.
- Plan을 다른 plan/세션 컨텍스트에 의존하도록 작성하기 (plan은 self-contained여야 한다).
- `/spec-new`, `/spec-resolve`, `/plan-new` 실행 중 `docs/specs/` 외부 파일 수정.

### 워크플로우 명령

- 새 피처의 spec 시작: `/spec-new`
- Spec의 Open Questions 닫기: `/spec-resolve <spec-path>`
- 기존 spec에 plan 추가: `/plan-new <spec-path>`
