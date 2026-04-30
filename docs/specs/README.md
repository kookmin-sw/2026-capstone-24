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
| 검증 실패에서 후속 plan 시드 | `/plan-new --from-failure <failed-plan-path>` (또는 `/spec-implement`가 자동 분기) |
| 작성된 plan대로 구현 | `/spec-implement <plan-path>` (기본 dry-run, `--apply`로 실제 실행) |
| 구현 완료 후 상태 갱신·아카이브 | `plan-complete` skill (자동 트리거) |

## 검증 실패 시 후속 plan 시드

`/spec-implement --apply` 도중 plan의 acceptance criteria가 fail로 판정되면(특히 `manual-hard`에서 자주 발생한다) 그 plan은 Status `In Progress` 유지 + 큐 중단으로 멈춘다. 이때 사용자가 선택할 수 있는 분기는 셋이다.

1. **원 plan 본문 수정 후 재호출** — 실패 원인이 plan의 매개변수·순서 조정으로 풀리는 경우.
2. **후속 plan 시드 후 재호출** — 실패 원인이 새 자산·시스템·튜닝 작업을 요구해 원 plan의 Approach 안에서 풀기 어려운 경우. 권장 시점: 원 plan의 Out of Scope에 적힌 영역에 가깝거나, 결정 근거 자체를 바꿔야 할 때.
3. **중단** — abandoned 처리 또는 보류.

`/spec-implement`는 manual-hard fail 직후 사용자에게 "후속 plan을 지금 시드할까요?"를 묻는다. **yes**를 선택하면 자동으로 `/plan-new --from-failure <current-plan>`을 위임 호출하며, 새 plan은 다음을 자동으로 부여받는다.

- **`Linked Spec`** — 원 plan과 동일한 spec을 자동 상속.
- **`Caused By` 헤더** — `**Caused By:** [<선행 plan 파일>](./<선행 plan 파일>)` 한 줄. grep으로 의존 그래프를 추적하기 위한 옵셔널 메타필드.
- **Context 표준 인용 블록** — 선행 plan 파일명 + 실패한 acceptance criteria 원문 + evidence를 자동 인용.
- **마지막 manual-hard AC** — "선행 plan `<파일>` 의 실패 AC '<원문 일부>' 가 이 plan 적용 후 재검증에서 통과한다." 한 줄 자동 추가.
- **선행 plan의 `## Notes` append** — "<YYYY-MM-DD>: 검증 실패에서 파생된 후속 plan `<파일>` 추가. 완료 후 본 plan의 `[manual-hard]` '<원문 일부>' 항목 재검증 필요." 한 줄.

후속 plan이 `--apply`로 완료(=AC 모두 pass)되면 `/spec-implement`가 선행 plan의 `.orchestrator-state.json` `per_plan_history[].acceptance_results[]`의 fail 항목을 자동으로 pass로 갱신한다(reflect). 사용자가 `/spec-implement <sub-spec> --apply`를 다시 호출하면 선행 plan이 재검증 단계로 진입하고, 이미 pass로 기록된 manual-hard 항목은 재질문 없이(또는 한 번의 확인만으로) Done 처리될 수 있다. 자동 reflect가 매칭에 실패하면 noop + Notes 경고가 남고, 사용자가 manual로 재검증·통과 처리한다.

`spec` 모드 큐는 매 plan 시작 직전 sub-spec 표를 다시 읽어 재생성된다. 재생성 시 정렬 키는 **`Caused By` 체인 + 작성일 위상정렬**이다. **선행 plan이 `In Progress`(=blocked)이면 후속 plan을 먼저 실행**한다 — 선행이 막혀 있으니 후속을 풀고 와야 재검증이 가능하기 때문이다.

> **재검증 AC 매칭 키 (단일 진실원).**
> 자동 reflect 로직이 후속 plan의 "재검증" AC 항목을 인식할 때 사용하는 substring 키:
> ` 가 이 plan 적용 후 재검증에서 통과한다`
>
> `/plan-new --from-failure`가 후속 plan에 자동 부여하는 재검증 AC 문구와 `/spec-implement` 3-8.5 자동 reflect의 매칭 로직이 본 substring을 동일하게 참조한다. 본 문구가 후속 plan의 AC 항목 끝에 그대로 포함되어야 자동 reflect가 동작한다 — plan 본문에서 임의로 바꾸지 않는다. 누군가 이 키를 변경하려면 본 박스, plan-new, spec-implement 세 곳을 한 commit에 묶어 수정한다.

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
- **Acceptance Criteria 라벨 부여.** plan의 각 Acceptance Criteria 항목은 `[auto-hard]`(자동 검증·실패시 plan 중단) / `[auto-soft]`(자동 검증·실패시 노트 기록 후 진행) / `[manual-hard]`(사용자 직접 검증·실패시 plan 중단) 중 하나를 인라인 코드로 붙인다. 라벨 미부여 항목이 있으면 `/spec-implement`가 실행을 거부한다. 라벨은 이 3종으로 한정 — 사람이 직접 검증하는 항목은 항상 중단 사유로 처리한다.
- **Unity 직렬화 자산 의존 plan은 직렬화 정합성 또는 인스턴스화 sanity AC 최소 1건 필수.** `## Verified Structural Assumptions`에 못 박은 prefab 계층/nested override/씬 인스턴스 가정을 plan 적용 후 실제로 깨뜨리지 않았는지 확인하는 항목을 둔다 — 예: "VR Player prefab 인스턴스화 후 `<자식 경로>` 자식이 빠짐없이 존재한다", "PrefabUtility로 인스턴스화 시 콘솔 에러 0". 권장 라벨은 `[auto-hard]`(MCP `find_gameobjects`/`manage_prefabs`로 자동 검증 가능). 자동화가 어려우면 `[manual-hard]`로 떨어뜨린다 — `[auto-soft]`는 직렬화 사고에서 부적합(soft fail은 catch에 실패하므로 사고 패턴 그대로 재현된다).
- **검증 실패에서 파생된 plan은 헤더에 `**Caused By:** [<선행 plan>](./<선행 plan>)` 라인을 둔다.** 옵셔널 메타필드. `/plan-new --from-failure`가 자동 부여한다. 정책 단일 진실원: 위 "검증 실패 시 후속 plan 시드" 섹션.

## Plan 실행 시 읽기 순서

사용자가 plan 경로를 주고 "구현해" 라고 하면 다음 순서로 컨텍스트를 적재한다.

1. **Plan 파일** — Goal / Context / Approach / Acceptance Criteria 파악.
2. **Linked Spec** — 해당 sub-spec 또는 root-spec.
3. **parent `_index.md`** — 피처 전체의 Why / What / 다른 sub-spec들과의 관계.
4. 그 후 구현 시작.

> 정식 진입점은 `/spec-implement <plan-path>` slash command다. 사람이 plan을 직접 실행하더라도 위 순서를 따른다.

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
| [teleport-locomotion](teleport-locomotion/_index.md) | Active | 3 | 1/2 | |

> Status 값: `Draft` / `Active` / `Done` / `Abandoned`
> 새 피처 추가 시 이 표에 행을 직접 갱신한다.
