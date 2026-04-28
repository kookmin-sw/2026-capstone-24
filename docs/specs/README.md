# Specs

이 폴더는 **Spec-Driven Development** 워크플로우의 진입점이다.
What/Why를 담는 얇은 **spec**과 How를 담는 실행 가능한 **plan**을 분리해서 관리한다.

## 폴더 구조

```
docs/specs/
├── README.md                       # 이 파일 — 인덱스 + 상태 보드
├── _templates/                     # 새 spec/plan 작성 시 베이스
│   ├── root-spec.md                # 큰 피처의 _index.md 템플릿
│   ├── sub-spec.md                 # 하위 spec 템플릿
│   └── plan.md                     # 구현 plan 템플릿
├── _archive/                       # 완료된 spec·plan 보관
│   └── <feature-kebab>/
│       ├── specs/
│       └── plans/
└── <feature-kebab>/                # 큰 피처 = 폴더 하나
    ├── _index.md                   # 루트 spec (What/Why + 하위 spec 링크)
    ├── specs/
    │   └── <NN>-<sub-name>.md      # 하위 spec (NN: 구현 순서 zero-pad 2자리)
    └── plans/
        └── <YYYY-MM-DD>-<author>-<slug>.md  # 구현 plan (날짜·작성자·slug 기반 파일명)
```

## 워크플로우

| 작업 | 명령 |
|---|---|
| 새 피처의 spec 시작 | `/spec-new` |
| 기존 spec의 Open Questions 닫기 | `/spec-resolve <spec-path>` |
| 기존 spec에 plan 추가 | `/plan-new <spec-path>` |
| 작성된 plan대로 구현 | plan 파일 경로를 Claude에게 전달 (Linked Spec → parent `_index.md` 순으로 자동 컨텍스트 적재) |
| 구현 완료 후 상태 갱신·아카이브 | `plan-complete` skill (자동 트리거) |

## 파일명 규칙

### Sub-Spec 파일명

- 형식: `docs/specs/<feature>/specs/<NN>-<sub-name>.md`
- `NN`은 **구현 순서**를 나타내는 zero-pad 2자리 숫자.
- 새 sub-spec 추가 시 같은 피처에 등록된 가장 큰 번호 + 1을 부여한다.
- 사이 삽입이 필요하면 영향받는 sub-spec 전체를 재번호하고, `_index.md`의 Sub-Specs 표 및 sub-spec 본문의 상호 참조 링크를 함께 갱신한다.

### Plan 파일명

- 형식: `docs/specs/<feature>/plans/<YYYY-MM-DD>-<author>-<slug>.md`
- 날짜: 시스템 로컬 기준 `YYYY-MM-DD`.
- 작성자 슬러그: `git config user.name` → 소문자화 + 비-영숫자(`-` 치환) + 양끝 `-` 제거.
- slug: plan 제목을 kebab-case로. 한국어 제목이면 영문 slug 후보를 사용자에게 확인받는다.
- 새 plan은 `/plan-new`로 작성. 충돌 시 slug 끝에 `-2`, `-3` 접미사로 디스앰비.

## 작성 규칙 요약

- **Spec은 얇게.** What/Why만. 함수명·파일 경로·자료구조 같은 구현 디테일 금지.
- **방대하면 쪼갠다.** 한 spec에 무관한 피처를 섞지 않는다.
- **Plan은 self-contained.** 다른 plan이나 이전 세션 컨텍스트를 가정하지 않는다. 필요한 배경은 `Context` 섹션에 모두 담는다.
- **링크 양방향 유지.** sub-spec ↔ plan은 서로 링크되어야 한다.
- **Plan 작성 전 Open Questions 정리.** 핵심 질문(예: 포맷 결정)이 미결이면 plan을 다시 써야 할 가능성이 높으므로 `/spec-resolve`로 먼저 닫는 것을 권장.

## Plan 실행 시 읽기 순서

사용자가 plan 경로를 주고 "구현해" 라고 하면 다음 순서로 컨텍스트를 적재한다.

1. **Plan 파일** — Goal / Context / Approach / Acceptance Criteria 파악.
2. **Linked Spec** — 해당 sub-spec 또는 root-spec.
3. **parent `_index.md`** — 피처 전체의 Why / What / 다른 sub-spec들과의 관계.
4. 그 후 구현 시작.

## 작성 anti-pattern (금지)

- Spec 본문에 함수명, 클래스명, 파일 경로, 알고리즘 같은 **구현 디테일** 넣기.
- 한 spec 폴더에 무관한 피처 섞기.
- Plan을 다른 plan/세션 컨텍스트에 의존하도록 작성하기.
- `/spec-new`, `/spec-resolve`, `/plan-new` 실행 중 `docs/specs/` 외부 파일 수정.
- `docs/plans/PLAN.md` 같은 ad-hoc 임시 plan 노트 운영. 1회성 메모는 대화 안에서 처리한다.

## 상태 보드

| Feature | Status | Sub-Specs | Plans (Done/Total) | 비고 |
|---|---|---|---|---|
| [rhythm-game](rhythm-game/_index.md) | Active | 6 | 5/5 | |
| [hands](hands/_index.md) | Active | 2 | 1/1 | |

> Status 값: `Draft` / `Active` / `Done` / `Abandoned`
> 새 피처 추가 시 이 표에 행을 직접 갱신한다.
