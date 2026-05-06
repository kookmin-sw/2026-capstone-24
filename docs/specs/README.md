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
│   ├── tech-spec.md                # Tech Spec 템플릿 (시스템 설계 윤곽)
│   ├── decision.md                 # Architecture Decision Record (ARD) 템플릿
│   └── plan.md                     # 구현 plan 템플릿
├── _archive/                       # 완료된 feature 전체 보관 (feature 단위 이동)
│   └── <feature-kebab>/
│       ├── _index.md               # 루트 spec (이동됨)
│       ├── specs/                  # 하위 spec 전체 (이동됨)
│       ├── tech-specs/             # Tech Spec 전체 (이동됨, sub-spec과 1:1 대응되는 항목만 존재)
│       ├── decisions/              # ARD 전체 (이동됨, drum-stick부터 포함)
│       └── plans/                  # 구현 plan 전체 (plan Done 시점에 먼저 이동됨)
└── <feature-kebab>/                # 큰 피처 = 폴더 하나
    ├── _index.md                   # 루트 spec (What/Why + 하위 spec 링크)
    ├── specs/
    │   └── <NN>-<sub-name>.md      # 하위 spec (NN: 구현 순서 zero-pad 2자리)
    ├── tech-specs/
    │   └── <NN>-<title>.md         # Tech Spec — /spec-build phase -1 (Tech Spec gate) 또는 /tech-spec 수동 작성. sub-spec 1:1
    ├── decisions/
    │   └── <NN>-<title>.md         # Architecture Decision Record (ARD) — /spec-build phase 0 자동 생성
    └── plans/
        └── <YYYY-MM-DD>-<author>-<slug>.md  # 구현 plan (날짜·작성자·slug 기반 파일명)
```

### Archive 정책

- **plan 단위**: `Status: Done` 직후 `_archive/<feature>/plans/`로 자동 이동.
- **sub-spec 단위**: Done 되어도 feature 전체 Done 시점까지 `specs/` 안에 보류. 중간 이동 없음.
- **feature 단위**: 모든 sub-spec Done + Open Q 0건 + 검증 pass + working tree clean 조건 모두 충족 시, 다음 항목을 `_archive/<feature>/`로 이동. 사용자 승인 1회.
  - `_index.md` / `specs/` / `tech-specs/` / `decisions/` / `plans/`(이미 `_archive`에 있으면 병합)
  - `.feature-build-state.json`은 삭제(`.gitignore`라 git 영향 없음)
- **archive 포맷 사례**: `teleport-locomotion`은 decisions/ 없는 구형식, `drum-stick`은 decisions/ 포함 중간형식, Tech Spec 도입 이후 신규 feature는 tech-specs/ 까지 포함하는 신형식. 기존 feature에 회고적으로 tech-specs/를 채우지 않는다.
- **archive 행 표기**: `| [<feature>](_archive/<feature>/_index.md) | Done | ... |`

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
| sub-spec의 Tech Spec(시스템 설계 윤곽) 작성 | `/tech-spec <sub-spec-path>` |
| 기존 spec에 plan 추가 | `/plan-new <spec-path>` |
| 검증 실패에서 후속 plan 시드 | `/plan-new --from-failure <failed-plan-path>` (또는 `/spec-implement`가 자동 분기) |
| 작성된 plan대로 구현 | `/spec-implement <plan-path>` (기본 dry-run, `--apply`로 실제 실행) |
| 구현 완료 후 상태 갱신·아카이브 | `plan-complete` skill (자동 트리거) |

### Command 책임 분리

| Command | 입력 | 책임 | 사용자 게이트 |
|---|---|---|---|
| `/spec-interview` | 자유 텍스트(아이디어) | 인터뷰 → spec(루트+서브) 박제. Open Q 0건 강제. | Q&A 라운드 N회 + 초안 확인 1회 |
| `/spec-build` | root-spec `_index.md` | sub-spec 큐 자동 실행: **phase -1 Tech Spec gate** → **phase 0 Architecture Decision** → plan-drafter → plan-quality-reviewer → spec-implement 워크플로우 inline 답습 | phase -1 Tech Spec yes/no/skip-permanently 3택·phase -1 인터뷰 라운드(yes 시)·phase 0 설계 결정 Q&A·manual-hard 검증·destructive 가드 |
| `/spec-implement` | plan 또는 sub-spec 경로 | 미완료 plan들 순차 실행 + manual-hard 4택 분기 + Caused By max-cascade 제한 | manual-hard 검증, handoff 승인 |
| `/tech-spec` | sub-spec 경로 | sub-spec 1개의 시스템 설계 윤곽(Tech Spec)을 인터뷰로 박제. 결정은 하지 않고 분기만 골라 후속 ARD에 시드. `/spec-build` phase -1이 `--auto` 모드로도 inline 답습. | 인터뷰 라운드(최대 3회) + 1:1·skip 가드 |
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

### Tech Spec 파일명 + 1:1 룰

- 형식: `docs/specs/<feature>/tech-specs/<NN>-<title>.md`
- `<NN>`: 대응하는 sub-spec과 **동일한** zero-pad 2자리 (NN 미부여 sub-spec이면 sub-spec 파일명 베이스 사용).
- **sub-spec 1개 ↔ Tech Spec 1개 1:1 강제.** 같은 sub-spec에 Tech Spec 2개 이상 만들지 않는다 — 그 신호가 보이면 sub-spec을 쪼갠다.
- 새 Tech Spec은 `/spec-build` phase -1 게이트 또는 `/tech-spec` 수동 호출로 작성.

### Tech Spec 트리거 (단일 진실원)

`/spec-build` phase -1이 `tech-spec-extractor`를 호출해 다음 양성 신호 3종 중 1건이라도 발화하는지 점검한다. 1건 이상이면 사용자에게 Tech Spec 작성 여부를 1회 묻는다(yes / no / skip-permanently).

1. **신규 클래스/컴포넌트 2개 이상 + 그들 사이 통신·의존**이 있는 sub-spec.
2. **기존 클래스의 public API 접속 또는 frame loop·event 구독에 끼어들기**가 필요한 sub-spec.
3. **데이터/제어 흐름이 한 컴포넌트 안에서 닫히지 않는** sub-spec.

양성 신호 0건이면 phase -1을 통째로 skip한다. `skip-permanently` 선택 시 sub-spec 헤더에 `**Tech Spec:** skipped` 한 줄 박제 → 다음 호출에 안 묻힘.

### Plan 파일명

- 형식: `docs/specs/<feature>/plans/<YYYY-MM-DD>-<author>-<slug>.md`
- 날짜: 시스템 로컬 기준 `YYYY-MM-DD`.
- 작성자 슬러그: `git config user.name` → 소문자화 + 비-영숫자(`-` 치환) + 양끝 `-` 제거.
- slug: plan 제목을 kebab-case로. 한국어 제목이면 영문 slug 후보를 사용자에게 확인받는다.
- 새 plan은 `/plan-new`로 작성. 충돌 시 slug 끝에 `-2`, `-3` 접미사로 디스앰비.

## 작성 규칙 요약

- **Tech Spec → ARD → plan 우선.** plan-drafter는 plan 작성 전에 같은 sub-spec의 `tech-specs/<NN>-*.md`(있으면)와 `decisions/<NN>-*.md`(있으면)를 모두 읽고, 그 내용을 plan의 Approach·Verified Structural Assumptions에 그대로 반영한다. **Tech Spec의 Boundaries에서 "건드리지 않는다"고 박제된 영역은 plan Deliverables에 포함 금지**, **Tech Spec의 Invariants는 plan Approach가 깨지 않는 형태로 설계**, **ARD와 충돌하는 plan 본문 작성 금지.** Tech Spec ↔ ARD 짝 검증: ARD가 Tech Spec의 `Open Tech Decisions`에서 도출됐으면 ARD 헤더에 `**From Tech Spec:** <path> §Open Tech Decisions #N`이 박제돼야 하고, 동시에 Tech Spec의 대응 항목 끝에 `→ decisions/<NN>-*.md`가 추가돼야 한다.
- **Spec은 얇게.** What/Why만. 함수명·파일 경로·자료구조 같은 구현 디테일 금지.
- **방대하면 쪼갠다.** 한 spec에 무관한 피처를 섞지 않는다.
- **Plan은 self-contained.** 다른 plan이나 이전 세션 컨텍스트를 가정하지 않는다. 필요한 배경은 `Context` 섹션에 모두 담는다.
- **링크 양방향 유지.** sub-spec ↔ plan은 서로 링크되어야 한다.
- **Plan 작성 전 Open Questions 정리.** 핵심 질문(예: 포맷 결정)이 미결이면 plan을 다시 써야 할 가능성이 높으므로 `/spec-resolve`로 먼저 닫는 것을 권장.
- **Acceptance Criteria 라벨 부여.** plan의 각 Acceptance Criteria 항목은 `[auto-hard]`(자동 검증·실패시 plan 중단) / `[auto-soft]`(자동 검증·실패시 노트 기록 후 진행) / `[manual-hard]`(사용자 직접 검증·실패시 plan 중단) 중 하나를 인라인 코드로 붙인다. 라벨 미부여 항목이 있으면 `/spec-implement`가 실행을 거부한다. 라벨은 이 3종으로 한정 — 사람이 직접 검증하는 항목은 항상 중단 사유로 처리한다.
- **Unity 직렬화 자산 의존 plan은 직렬화 정합성 또는 인스턴스화 sanity AC 최소 1건 필수.** `## Verified Structural Assumptions`에 못 박은 prefab 계층/nested override/씬 인스턴스 가정을 plan 적용 후 실제로 깨뜨리지 않았는지 확인하는 항목을 둔다 — 예: "VR Player prefab 인스턴스화 후 `<자식 경로>` 자식이 빠짐없이 존재한다", "PrefabUtility로 인스턴스화 시 콘솔 에러 0". 권장 라벨은 `[auto-hard]`(MCP `find_gameobjects`/`manage_prefabs`로 자동 검증 가능). 자동화가 어려우면 `[manual-hard]`로 떨어뜨린다 — `[auto-soft]`는 직렬화 사고에서 부적합(soft fail은 catch에 실패하므로 사고 패턴 그대로 재현된다).
- **컴포넌트 enum/Flags 필드를 신규 셋업하는 plan은 의도 값 검증 AC 1건 필수.** 부착 사실만 검증하는 AC는 MCP의 enum 인덱스 매핑 함정(인스펙터 표기와 직렬화 인덱스가 어긋나는 케이스)을 잡지 못해 동작이 정반대가 되는 사고를 그대로 통과시킨다. AC는 `## Verified Structural Assumptions`에 박제된 enum 정의의 의도 값을 직렬화 grep 단일 매치로 검증하는 형태로 둔다 — 예: "`Plane TeleportationArea` 부착 + `m_TeleportTrigger == 0`(OnSelectExited)을 grep으로 단일 매치." 권장 라벨 `[auto-hard]`. 단일 propertyPath 스칼라 변경이 필요할 때는 [`unity-asset-edit`](.claude/skills/unity-asset-edit/SKILL.md) skill의 직접 텍스트 Edit 예외 경로로 우회한다.
- **검증 실패에서 파생된 plan은 헤더에 `**Caused By:** [<선행 plan>](./<선행 plan>)` 라인을 둔다.** 옵셔널 메타필드. `/plan-new --from-failure`가 자동 부여한다. 정책 단일 진실원: 위 "검증 실패 시 후속 plan 시드" 섹션.
- **호출 API side effect 박제 강제.** Unity 자산 의존 plan이 외부 컴포넌트 public API를 호출하면, 그 API가 호출 컴포넌트의 transform·frame loop·event 구독에 미치는 모든 side effect를 `## Verified Structural Assumptions`에 박제한다. 부분 라인 박제 금지.

### ARD Spec What Coverage 룰

phase 0(arch-decision-extractor)이 추출하는 결정 후보의 모든 `options[]` 항목은 sub-spec의 `## What` 섹션에 박제된 모든 What을 1:1 매핑해 `spec_what_coverage`를 박제해야 한다. "만족 못 함" 옵션은 라벨 끝에 ⚠️ 마커. `recommended`는 `spec_what_coverage` 전부 "만족"인 옵션 우선.

phase 0가 작성하는 `decisions/<NN>-<title>.md` 본문에는 `## Spec What Coverage` 섹션이 옵션이 아니라 *권장*된다. 결정의 근거가 What 만족도라면 그 매트릭스를 본문에 박제해 후속 plan-drafter·plan-implementer가 결정 의도를 정확히 받을 수 있게 한다.

모든 옵션이 ⚠️인 경우(어떤 옵션도 spec What을 fully 만족하지 못함) arch-decision-extractor는 경고 한 줄을 해당 결정 항목에 추가하고, 메인 세션이 사용자에게 sub-spec의 What 재검토 여부를 확인한다.

### plan-quality-reviewer 점검 항목 #4 (Spec What 정합)

plan-quality-reviewer는 plan 작성 직후 Linked Spec의 `## What` 항목을 enumerate해 각 What이 plan의 Approach·Deliverables 적용으로 만족되는지를 pass/partial/fail 3분류로 판정한다.

| 판정 | 의미 | verdict 영향 |
|---|---|---|
| pass | 만족 메커니즘 박제 + AC 검증 항목 존재 | — |
| partial | 메커니즘 박제됐으나 AC 누락, 또는 AC는 있으나 메커니즘 모호 | `fix-and-retry` |
| fail | Approach 적용 후에도 해당 What이 만족된다고 추론 불가 | `stop` |

`fail` 1건 이상 → verdict `stop` (ARD 또는 spec 수정 필요, plan-drafter 재호출로 해결 불가).  
`partial` 1건 이상(fail 없음) → verdict `fix-and-retry` (plan-drafter 재호출로 수정 가능).

plan-drafter는 plan 작성 시 위 기준으로 self-check해, Approach가 spec의 모든 What을 만족시키는지 확인한 후 제출한다.

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
| [drum-stick](drum-stick/_index.md) | Active | 2 | 3/3 | |
| [teleport-locomotion](_archive/teleport-locomotion/_index.md) | Done | 3 | 4/4 | |

> Status 값: `Draft` / `Active` / `Done` / `Abandoned`
> 새 피처 추가 시 이 표에 행을 직접 갱신한다.
