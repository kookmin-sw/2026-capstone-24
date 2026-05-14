<!--
Tech Spec 템플릿. 이 파일은 docs/specs/<feature>/tech-specs/<NN>-<title>.md로
복사해서 사용한다 (NN: 대응하는 sub-spec과 동일).

Tech Spec = 한 sub-spec의 시스템 설계 윤곽 박제. 결정 자체는 하지 않는다 —
결정해야 할 분기 지점만 골라내 ARD(decisions/<NN>-*.md)로 넘긴다.

작성 규칙:
- sub-spec 1개 ↔ Tech Spec 1개 1:1. 같은 sub-spec에 Tech Spec 2개 이상 만들지 않는다
  (그 신호가 보이면 sub-spec을 쪼갠다).
- 본문은 아래 6 섹션으로 한정한다. 다른 헤더 추가 금지. 각 섹션이 비더라도 헤더는 둔다
  (`_해당 없음_` 한 줄로 채움).
- **anti-pattern**: 본문에 옵션 비교/선택 문장 금지. "A vs B 중 A를 채택" 같은 분기는
  ARD에서 다룬다. Tech Spec은 *서술*만, ARD는 *분기 결정*.
- Components·Data/Control Flow에는 알고리즘이나 구현 디테일을 적지 않는다 (그건 plan으로).
- Assumptions에는 외부에서 받아오는 사실 + 출처 표기를 함께 둔다
  (`Read <경로> (YYYY-MM-DD)` 또는 `unity-scene-reader 보고 (YYYY-MM-DD)`).
- Open Tech Decisions의 각 항목은 후속 ARD 1건으로 1:1 매핑되어야 한다. ARD가 작성되면
  해당 항목 끝에 `→ decisions/<NN>-*.md` 한 줄로 닫는다.
-->

# <Tech Spec Title>

**Sub-Spec:** [`<NN>-<sub>.md`](../specs/<NN>-<sub>.md)
**Status:** `Draft`
**Date:** <YYYY-MM-DD>

<!-- Status 값: Draft / Accepted -->

## Components

<새 또는 기존 어떤 컴포넌트·클래스·시스템이 등장하는가. 이름과 한 줄 역할만 적는다 — 알고리즘·필드·메서드 시그니처 금지.>

- **<컴포넌트 1 이름>** (신규 | 기존) — <한 줄 역할>
- **<컴포넌트 2 이름>** (신규 | 기존) — <한 줄 역할>

## Data / Control Flow

<누가 누구에게 무엇을 frame/event 단위로 보내는가. 시퀀스를 글머리 또는 화살표 리스트로.>

- <트리거> → <컴포넌트 A> → <전달되는 데이터/이벤트> → <컴포넌트 B> → <결과 상태>
- <보조 시퀀스>

## Boundaries

<이 설계가 "건드리는 영역" / "건드리지 않는 영역". sub-spec의 Out of Scope보다 더 기술적으로.>

- **건드린다**: <영역/컴포넌트/자산 1>
- **건드리지 않는다**: <인접해 보이지만 손대지 않는 영역 1>

## Invariants

<항상 참이어야 하는 사실. plan-drafter가 이걸 깨면 안 됨.>

- <불변식 1 — 예: "PlayHand source는 동시에 1개만 점유">
- <불변식 2>

## Assumptions

<외부에서 받아오는 사실 + 출처. 이 가정이 깨지면 본 Tech Spec이 무효화된다.>

- <가정 1> — 출처: `Read <경로> (YYYY-MM-DD)` 또는 `unity-scene-reader 보고 (YYYY-MM-DD)`
- <가정 2> — 출처: ...

## Open Tech Decisions

<닫지 않은 분기 지점. 각 항목은 후속 ARD 1건으로 1:1 매핑된다. ARD 작성 후 `→ decisions/<NN>-*.md` 한 줄로 닫는다.>

- [ ] <분기 1 — 한 줄로 무엇을 결정해야 하는지>
- [ ] <분기 2>
