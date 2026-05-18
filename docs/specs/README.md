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
│       ├── _index.md
│       ├── specs/
│       ├── tech-specs/
│       ├── decisions/
│       └── plans/
└── <feature-kebab>/                # 큰 피처 = 폴더 하나
    ├── _index.md                   # 루트 spec (What/Why + 하위 spec 링크)
    ├── specs/
    │   └── <NN>-<sub-name>.md      # 하위 spec (NN: 구현 순서 zero-pad 2자리)
    ├── tech-specs/
    │   └── <NN>-<title>.md         # Tech Spec — /spec-interview에서 작성, sub-spec 1:1
    ├── decisions/
    │   └── <NN>-<title>.md         # ARD — /spec-interview에서 작성
    └── plans/
        └── <YYYY-MM-DD>-<author>-<slug>.md  # 구현 plan — /spec-build가 planner sub-agent로 작성
```

### Archive 정책

- **plan 단위**: `Status: Done` 직후 `_archive/<feature>/plans/`로 자동 이동 (doc-updater가 처리).
- **sub-spec 단위**: Done 되어도 feature 전체 Done 시점까지 `specs/` 안에 보류.
- **feature 단위**: 모든 sub-spec Done + Open Q 0건 + 검증 pass + working tree clean 조건 충족 시 `_archive/<feature>/`로 이동. 사용자 승인 1회 (destructive 가드).
- **archive 행 표기**: `| [<feature>](_archive/<feature>/_index.md) | Done | ... |`

## 워크플로우

진입점은 **2개**.

| 작업 | 명령 |
|---|---|
| 새 피처 박제 (spec + Tech Spec + ARD 한 번에) | `/spec-interview [러프한 아이디어]` |
| 박제된 root-spec 자동 구현 | `/spec-build <root-spec-path> --apply` |

`/spec-interview`는 자유 Q&A로 root-spec + sub-spec + (양성 신호 발화 시) Tech Spec + ARD를 단일 사용자 확인 게이트로 박제한다. Tech Spec 양성 신호 점검·Prefab 구조 박제·ARD 후보 추출을 메인 세션이 직접 수행한다.

`/spec-build`는 root-spec을 받아 sub-spec 큐를 순회하며 plan을 1개씩 만들고 구현한다. 사용자 게이트는 3종(plan 검토 / manual-hard 테스트 / destructive 가드).

### Sub-agent 책임 분리

| Sub-agent | 호출 주체 | 책임 |
|---|---|---|
| `planner` | `/spec-build` | sub-spec 한 개에 대해 plan 1개 작성 + self-check 진단 |
| `orchestrator` | `/spec-build` | plan 1개 라이프사이클(implementer → reviewer → unity-test-runner → 자동 AC 검증) 격리 sandbox |
| `implementer` | `orchestrator` | plan의 코드/자산 변경 적용 (Unity MCP write 권한) |
| `reviewer` | `orchestrator` | 구현 후 git diff vs plan 의도 검증 |
| `unity-test-runner` | `orchestrator` | EditMode·PlayMode 회귀 테스트 |
| `doc-updater` | `/spec-build`, 메인 세션 | plan/sub-spec/feature 라이프사이클 종료 시 Status·표·링크·archive 이동 |
| `unity-scene-reader` | `planner`, `/spec-interview` | Unity 자산 read-only 사실 추출 (Prefab Hierarchy 박제 등) |
| `unity-scene-writer` | 메인 세션 보조 | 확정된 Unity 자산 변경 적용 |

## 검증 실패 시 후속 plan 시드 (3택)

`/spec-build --apply` 도중 plan의 manual-hard AC가 fail 판정되면 메인 세션이 사용자에게 **3택**을 묻는다.

| 옵션 | 동작 |
|---|---|
| `pass` | 통과 처리. 항목별 evidence 박제. |
| `retry-via-new-plan` | 후속 plan을 planner에 발주 (Caused By 자동 부여). `cascade_depth >= max-cascade`이면 거부. |
| `stop` | 큐 중단. 상태 파일 `pending_user_action` 기록 후 멈춤. |

**Caused By max-cascade 제한:** `/spec-build --max-cascade N` (default 2). 무한 루프 차단.

`retry-via-new-plan` 시 새 plan은 다음을 자동 부여받는다.

- **`Linked Spec`** — 선행 plan과 동일한 spec.
- **`Caused By` 헤더** — `**Caused By:** [<선행 plan>](./<선행 plan>)` 한 줄.
- **Context 표준 인용 블록** — 선행 plan 파일명 + 실패 AC 원문 + evidence.
- **마지막 manual-hard AC** — "선행 plan `<파일>` 의 실패 AC '<원문 일부>' 가 이 plan 적용 후 재검증에서 통과한다." 한 줄 자동 추가.
- **선행 plan의 `## Notes` append** — "<YYYY-MM-DD>: 후속 plan `<파일>` 추가. 완료 후 본 plan 재검증 필요." 한 줄.

후속 plan이 통과되면 `/spec-build`가 선행 plan의 manual-hard 재검증 항목을 자동 reflect로 pass 갱신한다.

> **재검증 AC 매칭 키 (단일 진실원).**
> 자동 reflect 로직이 후속 plan의 "재검증" AC 항목을 인식할 때 사용하는 substring 키:
> ` 가 이 plan 적용 후 재검증에서 통과한다`
>
> planner가 후속 plan에 자동 부여하는 재검증 AC 문구와 `/spec-build`의 자동 reflect 매칭 로직이 본 substring을 동일하게 참조한다.

## 파일명 규칙

### Sub-Spec 파일명

- 형식: `docs/specs/<feature>/specs/<NN>-<sub-name>.md`
- `NN`은 **구현 순서**를 나타내는 zero-pad 2자리 숫자.
- 새 sub-spec 추가 시 같은 피처에 등록된 가장 큰 번호 + 1.
- 사이 삽입이 필요하면 영향받는 sub-spec 전체를 재번호하고, `_index.md` Sub-Specs 표와 본문 상호 참조 링크를 함께 갱신한다.

### Tech Spec 파일명 + 1:1 룰

- 형식: `docs/specs/<feature>/tech-specs/<NN>-<title>.md`
- `<NN>`: 대응하는 sub-spec과 **동일한** zero-pad 2자리.
- **sub-spec 1개 ↔ Tech Spec 1개 1:1 강제.** 같은 sub-spec에 Tech Spec 2개 이상 만들지 않는다 — 신호가 보이면 sub-spec을 쪼갠다.
- 새 Tech Spec은 `/spec-interview` 안의 양성 신호 4종 점검을 통과한 sub-spec에 한해 작성.

### Tech Spec 양성 신호 4종

`/spec-interview`가 다음 신호 1건이라도 발화하면 사용자에게 Tech Spec 작성 여부 3택(yes/no/skip-permanently)을 묻는다.

1. **신규 클래스/컴포넌트 2개 이상 + 그들 사이 통신·의존**이 있는 sub-spec.
2. **기존 클래스의 public API 접속 또는 frame loop·event 구독에 끼어들기**가 필요한 sub-spec.
3. **데이터/제어 흐름이 한 컴포넌트 안에서 닫히지 않는** sub-spec.
4. **Comparable Siblings 누락** — `Assets/` 또는 `docs/specs/` 산하에 동급 자산 1+개가 있는데 sub-spec/기존 tech-specs 둘 다에 비교 박제가 없는 sub-spec.

양성 신호 0건이면 Tech Spec skip. `skip-permanently` 선택 시 sub-spec 헤더에 `**Tech Spec:** skipped` 박제 → 다음 호출에 안 묻힘.

### Plan 파일명

- 형식: `docs/specs/<feature>/plans/<YYYY-MM-DD>-<author>-<slug>.md`
- 날짜: 시스템 로컬 기준 `YYYY-MM-DD`.
- 작성자 슬러그: `git config user.name` → 소문자화 + 비-영숫자(`-` 치환) + 양끝 `-` 제거.
- slug: plan 제목을 kebab-case로.
- planner sub-agent가 자동 생성. 충돌 시 `-2`, `-3` 접미사.

## 작성 규칙 요약

- **Tech Spec → ARD → plan 우선.** planner는 같은 sub-spec의 `tech-specs/<NN>-*.md`(있으면)와 `decisions/<NN>-*.md`(있으면)를 모두 읽고 plan의 Approach·Verified Structural Assumptions에 반영. **Tech Spec Boundaries의 "건드리지 않는다" 영역은 plan Deliverables에 포함 금지**, **Tech Spec Invariants를 plan Approach가 깨지 않도록 설계**, **ARD와 충돌하는 plan 본문 작성 금지.** Tech Spec ↔ ARD 짝 검증: ARD가 Tech Spec의 `Open Tech Decisions`에서 도출됐으면 ARD 헤더에 `**From Tech Spec:** <path> §Open Tech Decisions #N` 박제 + Tech Spec 대응 항목 끝에 `→ decisions/<NN>-*.md` 추가.
- **Spec은 얇게.** What/Why만. 함수명·파일 경로·자료구조 같은 구현 디테일 금지. (Tech Spec/ARD에는 허용.)
- **방대하면 쪼갠다.** 한 spec에 무관한 피처를 섞지 않는다.
- **Plan은 self-contained + 1개 단위.** planner는 매 호출당 plan 1개만 작성한다. sub-spec이 N개 plan을 요구하면 `/spec-build`가 N회 호출. 필요한 배경은 `Context` 섹션에 모두 담는다.
- **링크 양방향 유지.** sub-spec ↔ plan은 서로 링크되어야 한다.
- **Plan 작성 전 Open Questions 정리.** spec의 모든 Open Q는 `/spec-interview` 박제 시점에 0건이어야 한다.
- **Acceptance Criteria 라벨 부여.** plan의 각 AC 항목은 `[auto-hard]`(자동 검증·실패시 plan 중단) / `[auto-soft]`(자동 검증·실패시 노트 기록 후 진행) / `[manual-hard]`(사용자 직접 검증·실패시 plan 중단) 중 하나. 라벨 미부여 항목이 있으면 `/spec-build`가 실행을 거부한다.
- **AC evidence 라인 의무화.** 각 AC 본문 뒤에 `**검증:**` 라인을 1줄 부착. `[auto-hard]`/`[auto-soft]`는 Grep 패턴·Bash 명령·MCP 도구 호출 같은 자동 실행 evidence, `[manual-hard]`는 시각/시뮬레이션 시나리오 1줄. evidence가 단일 사실에만 머무르면 **런타임 / 씬 로드 / 직렬화 정합** evidence 1건 이상을 같은 plan 안 다른 AC에 동반시킨다.
- **Unity 직렬화 자산 의존 plan은 직렬화 정합성/인스턴스화 sanity AC 최소 1건 필수.** `## Verified Structural Assumptions`에 못 박은 prefab 계층/nested override/씬 인스턴스 가정을 plan 적용 후 깨뜨리지 않는지 확인. 권장 라벨 `[auto-hard]`(MCP `find_gameobjects`/`manage_prefabs`). 자동화 어려우면 `[manual-hard]`. `[auto-soft]`는 직렬화 사고에 부적합.
- **컴포넌트 enum/Flags 필드 신규 셋업 plan은 의도 값 검증 AC 1건 필수.** `## Verified Structural Assumptions`에 박제된 enum 정의의 의도 값을 직렬화 grep 단일 매치로 검증. 권장 라벨 `[auto-hard]`.
- **검증 실패에서 파생된 plan은 헤더에 `**Caused By:**` 라인.** planner가 자동 부여. 정책 단일 진실원: 위 "검증 실패 시 후속 plan 시드" 섹션.
- **호출 API side effect 박제 강제.** Unity 자산 의존 plan이 외부 컴포넌트 public API를 호출하면, 그 API가 호출 컴포넌트의 transform·frame loop·event 구독에 미치는 모든 side effect를 `## Verified Structural Assumptions`에 박제. 부분 라인 박제 금지.
- **(권고) 스크립트 변경 plan은 컴파일 0건 검증 AC 1건.** `.cs` 신규/수정이 포함된 plan은 `read_console(types=["error"])`가 0건임을 plan 적용 후 확인하는 AC 1건 권장. 라벨 `[auto-hard]`. 절차는 [`unity-mcp-workflow`](../../.claude/skills/unity-mcp-workflow/SKILL.md) §2.
- **(권고) 시각적 변화가 핵심인 plan은 screenshot 검증 AC 1건.** GameObject 배치/카메라 lens/머티리얼/UI 같이 시각으로 의도 일치를 확인해야 하는 plan은 screenshot 1장 첨부를 `[manual-hard]` AC로 1건 권장.

### ARD Spec What Coverage 룰

`/spec-interview` ARD 후보 추출에서 모든 `options[]` 항목은 sub-spec `## What` 항목 1:1 매핑 `spec_what_coverage`를 박제. "만족 못 함" 옵션은 라벨 끝 ⚠️. `recommended`는 모두 "만족"인 옵션 우선.

작성된 `decisions/<NN>-<title>.md` 본문에는 `## Spec What Coverage` 섹션이 권장된다. 모든 옵션이 ⚠️인 경우 사용자에게 sub-spec `## What` 재검토 여부를 확인.

## Plan 실행 시 읽기 순서

사용자가 plan 경로를 주고 "구현해" 라고 하면 다음 순서로 컨텍스트를 적재한다.

1. **Plan 파일** — Goal / Context / Approach / Acceptance Criteria.
2. **Linked Spec** — 해당 sub-spec 또는 root-spec.
3. **parent `_index.md`** — 피처 전체의 Why / What / 다른 sub-spec들과의 관계.
4. **(있으면) Tech Spec / ARD** — 같은 sub-spec의 `tech-specs/<NN>-*.md` / `decisions/<NN>-*.md`.
5. 그 후 구현 시작.

> 정식 진입점은 `/spec-build <root-spec> --apply`다. plan 1개를 단독 실행하려면 메인 세션이 `orchestrator` sub-agent를 직접 호출한다.

## 작성 anti-pattern (금지)

- Spec 본문에 함수명, 클래스명, 파일 경로, 알고리즘 같은 **구현 디테일** 넣기. (Tech Spec/ARD에는 허용.)
- 한 spec 폴더에 무관한 피처 섞기.
- Plan을 다른 plan/세션 컨텍스트에 의존하도록 작성하기.
- `/spec-interview`/`/spec-build` 실행 중 `docs/specs/` 외부 파일 수정.
- `docs/plans/PLAN.md` 같은 ad-hoc 임시 plan 노트 운영. 1회성 메모는 대화 안에서 처리.

## 상태 보드

| Feature | Status | Sub-Specs | Plans (Done/Total) | 비고 |
|---|---|---|---|---|
| [rhythm-game](rhythm-game/_index.md) | Active | 14 | 21/21 | |
| [hands](hands/_index.md) | Active | 3 | 1/2 | |
| [drum-stick](drum-stick/_index.md) | Active | 2 | 3/3 | |
| [session-panel](session-panel/_index.md) | Active | 4 | 5/6 | |
| [multiplayer-network](multiplayer-network/_index.md) | Active | 6 | 13/19 | |
| [teleport-locomotion](_archive/teleport-locomotion/_index.md) | Done | 3 | 4/4 | |

> Status 값: `Draft` / `Active` / `Done` / `Abandoned`. doc-updater가 sub-spec/feature 종료 시점에 자동 갱신.
