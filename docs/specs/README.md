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

자동 파이프라인(권장):

| 작업 | 명령 |
|---|---|
| 새 피처를 인터뷰로 박제 | `/spec-interview [러프한 아이디어]` |
| 박제된 root-spec 한 개 자동 구현 | `/spec-build <root-spec-path>` (기본 dry-run, `--apply`로 실제 실행) |

수동 단계별 진입점(자동 파이프라인이 막히거나 한 단계만 손으로 진행하고 싶을 때):

| 작업 | 명령 |
|---|---|
| 새 피처의 spec 시작 | `/spec-new` |
| 기존 spec의 Open Questions 닫기 | `/spec-resolve <spec-path>` |
| 기존 spec에 plan 추가 | `/plan-new <spec-path>` |
| 검증 실패에서 후속 plan 시드 | `/plan-new --from-failure <failed-plan-path>` (또는 `/spec-implement`가 자동 분기) |
| 작성된 plan대로 구현 | `/spec-implement <plan-path>` (기본 dry-run, `--apply`로 실제 실행) |
| 구현 완료 후 상태 갱신·아카이브 | `plan-complete` skill (자동 트리거) |

### Command 책임 분리

| Command | 입력 | 책임 | 사용자 게이트 |
|---|---|---|---|
| `/spec-interview` | 자유 텍스트(아이디어) | 인터뷰 → spec(루트+서브) 박제. Open Q 0건 강제. | Q&A 라운드 N회 + 초안 확인 1회 |
| `/spec-build` | root-spec `_index.md` | sub-spec 큐 자동 실행: plan-drafter → plan-quality-reviewer → spec-implement 워크플로우 inline 답습 | manual-hard 검증·destructive 가드만 |
| `/spec-implement` | plan 또는 sub-spec 경로 | 미완료 plan들 순차 실행 + manual-hard 4택 분기 + Caused By max-cascade 제한 | manual-hard 검증, handoff 승인 |
| `/plan-new` | spec 경로 또는 `--from-failure` | plan 1~N개 인터랙티브 작성. plan-drafter sub-agent가 `--auto` 모드로도 호출. | 분할 결정·slug 확인 (인터랙티브 모드만) |
| `/spec-new`·`/spec-resolve` | spec 경로 | 단계별 spec 작성·Open Q 닫기 (`/spec-interview`로 흡수됨, 수동 호출용) | 라운드별 사용자 결정 |

## 검증 실패 시 후속 plan 시드

`/spec-implement --apply` 도중 plan의 acceptance criteria가 fail로 판정되면(특히 `manual-hard`에서 자주 발생한다) `/spec-implement`는 사용자에게 **4택 결정**을 묻는다. 단일 진실원: [`.claude/commands/spec-implement.md`](../../.claude/commands/spec-implement.md) "manual-hard fail 4택 분기" 박스.

| 옵션 | 동작 |
|---|---|
| `pass` | 통과 처리. 항목별 evidence 박제. |
| `stop` | plan Status `In Progress` 유지 + 큐 중단. 후속 plan 시드 묻지 않음. |
| `stop-and-seed` | 큐 중단 + `/plan-new --from-failure <current-plan>` 자동 위임. 새 plan은 아래 자동 부여 항목들을 받는다. (단 `cascade_depth >= max-cascade`면 거부.) |
| `skip-and-continue` | 이번 항목을 `skipped-deferred`로 박제 + plan은 In Progress 유지 + working tree 변경분 그대로 두고 다음 plan 진행. 큐 종료 시 deferred 목록을 모아 일괄 시드 옵션 제공. |

**Caused By max-cascade 제한:** `/spec-implement --max-cascade N` (default 2). `Caused By` 자동 시드가 N단을 초과하려 하면 거부 + 사용자 호출. 무한 루프 차단.

`stop-and-seed` 또는 `/plan-new --from-failure`를 직접 호출했을 때, 새 plan은 다음을 자동으로 부여받는다.

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
- **컴포넌트 enum/Flags 필드를 신규 셋업하는 plan은 의도 값 검증 AC 1건 필수.** 부착 사실만 검증하는 AC는 MCP의 enum 인덱스 매핑 함정(인스펙터 표기와 직렬화 인덱스가 어긋나는 케이스)을 잡지 못해 동작이 정반대가 되는 사고를 그대로 통과시킨다. AC는 `## Verified Structural Assumptions`에 박제된 enum 정의의 의도 값을 직렬화 grep 단일 매치로 검증하는 형태로 둔다 — 예: "`Plane TeleportationArea` 부착 + `m_TeleportTrigger == 0`(OnSelectExited)을 grep으로 단일 매치." 권장 라벨 `[auto-hard]`. 단일 propertyPath 스칼라 변경이 필요할 때는 [`unity-asset-edit`](.claude/skills/unity-asset-edit/SKILL.md) skill의 직접 텍스트 Edit 예외 경로로 우회한다.
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
| [rhythm-game](rhythm-game/_index.md) | Active | 11 | 11/13 | |
| [hands](hands/_index.md) | Active | 2 | 1/1 | |
| [teleport-locomotion](_archive/teleport-locomotion/_index.md) | Done | 3 | 4/4 | |

> Status 값: `Draft` / `Active` / `Done` / `Abandoned`
> 새 피처 추가 시 이 표에 행을 직접 갱신한다.
