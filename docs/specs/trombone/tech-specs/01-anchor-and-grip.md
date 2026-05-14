# Trombone — 앵커·그립 Tech Spec

**Sub-Spec:** [`01-anchor-and-grip.md`](../specs/01-anchor-and-grip.md)
**Status:** `Draft`
**Date:** 2026-05-14

## Components

- **TromboneGripController** (신규) — 트럼본 앵커 진입·이탈을 감지해 Mouth 추적·양손 grip override·양손 PhysicsHand deactivate·테이블 복귀를 일괄 관장.
- **MouthAnchor Transform** (신규) — VR Player Camera의 자식. 트럼본이 매 프레임 정렬할 입 위치 기준점.
- **MouthpieceAnchor Transform** (신규) — 트럼본 prefab의 자식. 매 프레임 MouthAnchor와 1:1 일치하도록 트럼본 root가 역계산된다.
- **LeftGripRoot / LeftGripWristRoot Transform** (신규) — 트럼본 바디 자식. 왼손 PlayHand source override 대상.
- **RightGripRoot / RightGripWristRoot Transform** (신규) — 슬라이드 GameObject 자식. 오른손 PlayHand source override 대상 (슬라이드 X 이동에 따라 함께 이동).
- **TeleportInstrumentProvider** (기존) — `ActiveInstrumentChanged` 이벤트로 활성 악기 전환 알림.
- **InstrumentTeleportLink** (기존) — 트럼본 앵커에 부착, 텔레포트 → 위 provider 체인 트리거.
- **PlayHandPoseDriver** (기존) — `PushSourceOverride/PopSourceOverride`로 양손 PlayHand 본 source를 grip root로 전환.
- **PhysicsHand GameObject (양손, 기존)** — 트럼본 활성 동안 SetActive(false), 이탈 시 SetActive(true).

## Data / Control Flow

- 텔레포트 → `InstrumentTeleportLink.AnyAnchorTeleported` → `TeleportInstrumentProvider.ActiveInstrumentChanged(current)` → TromboneGripController가 "이전이 자기 트럼본인가 / 새 값이 자기 트럼본인가" 비교로 진입·이탈 분기.
- 진입 시: TromboneGripController가 (a) 양손 PhysicsHand SetActive(false), (b) `PlayHandPoseDriver.PushSourceOverride(LeftGripRoot, LeftGripWristRoot)` 및 오른손 동등 호출, (c) Mouth 추적 활성 플래그 on.
- 매 프레임(추적 활성 시): MouthAnchor world pose에 MouthpieceAnchor가 일치하도록 트럼본 root를 역계산해 `SetPositionAndRotation`.
- 이탈 시: TromboneGripController가 (a) 트럼본 root를 Start 시점 캡처한 baseline world position/rotation으로 복귀, (b) `PlayHandPoseDriver.PopSourceOverride` 양손 호출, (c) 양손 PhysicsHand SetActive(true), (d) Mouth 추적 플래그 off.
- Start: TromboneGripController가 트럼본 root.world position/rotation을 복귀 baseline으로 캡처.

## Boundaries

- **건드린다**: TromboneGripController 신규 컴포넌트.
- **건드린다**: 트럼본 prefab — MouthpieceAnchor·LeftGripRoot/WristRoot·RightGripRoot/WristRoot child Transform 추가.
- **건드린다**: VR Player Camera child — MouthAnchor Transform 추가.
- **건드린다**: 트럼본 활성 동안 양손 PhysicsHand GameObject active 상태.
- **건드리지 않는다**: PlayHandPoseDriver 본체 시그니처·source priority 로직.
- **건드리지 않는다**: 드럼/피아노 grip 흐름(`GripPoseProvider` 경유 `XRGrabInteractable.selectEntered` 체인).
- **건드리지 않는다**: 슬라이드 X 이동 매핑 및 MIDI 발음(→ 02-slide-midi).
- **건드리지 않는다**: HidePhysicsHandInPlayMode의 SkinnedMeshRenderer 토글 로직.

## Invariants

- 트럼본 root.world transform은 Start 시점 캡처된 baseline으로만 복귀하고, 이탈 시 그 baseline과 정확히 일치한다.
- 트럼본 활성 동안 매 프레임 `MouthpieceAnchor.world transform == MouthAnchor.world transform`.
- 트럼본 활성 동안 양손 `PhysicsHand.activeInHierarchy == false`. 이탈 직후 `true`로 복원.
- 트럼본 활성 ↔ 비활성 전이는 `TeleportInstrumentProvider.ActiveInstrumentChanged` 단일 경로로만 발생.
- PlayHandPoseDriver override push와 pop이 양손 각각 짝 맞춰 호출된다 (활성 동안에만 push, 이탈 시 즉시 pop).

## Assumptions

- VR Player Camera에 child Transform을 추가해 입 위치 anchor로 사용 가능하다. — 출처: `Read Assets/SessionPanel/Scripts/SessionPanelController.cs (2026-05-14)` (Camera.main 참조 사례 확인).
- `InstrumentTeleportLink` → `TeleportInstrumentProvider` 이벤트 체인이 동작하고, 비-악기 구역 텔레포트 시 current = null이 된다. — 출처: `Read Assets/Instruments/_Core/Scripts/TeleportInstrumentProvider.cs (2026-05-14)`.
- `PlayHandPoseDriver.PushSourceOverride/PopSourceOverride`는 XRGrabInteractable select 이벤트와 독립적으로 직접 호출 가능하다. — 출처: `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-14)` lines 52–65.
- 트럼본 GameObject는 씬 셋업 시점에 테이블 위치(복귀 baseline)에 미리 배치된다 (TestSceneSanyo 셋업 책임).
- `HidePhysicsHandInPlayMode`는 SkinnedMeshRenderer.enabled만 토글하고 GameObject deactivate는 하지 않는다. 따라서 트럼본 측 SetActive(false)와 의미 충돌하지 않는다. — 출처: `Read Assets/Hands/Scripts/HidePhysicsHandInPlayMode.cs (2026-05-14)`.

## Open Tech Decisions

- [ ] PhysicsHand deactivate를 PlayHandPoseDriver 본체 책임으로 끌어올려 모든 grip override 사용자(드럼 스틱 grab 포함)에 일관 적용할지. 본 sub-spec은 TromboneGripController 한정 구현으로 default 진행. 드럼 스틱 grab도 PhysicsHand deactivate가 원 의도였다는 신호가 있어 cross-cutting 후보 — 별도 ARD에서 결정.
