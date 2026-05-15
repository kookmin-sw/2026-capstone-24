# Trombone Grip Controller — 앵커 진입·이탈 시 Mouth 추적·양손 grip override·PhysicsHand 토글

**Linked Spec:** [`01-anchor-and-grip.md`](../specs/01-anchor-and-grip.md)
**Status:** `Done`

## Goal

`TromboneGripController` 단일 신규 컴포넌트로 트럼본 앵커 진입·이탈을 감지하고, 진입 시 매 프레임 Mouth Transform을 추적하며 양손 `PhysicsHand`를 SetActive(false) + `PlayHandPoseDriver.PushSourceOverride`로 grip 모델 교체를 수행하고, 이탈 시 모든 상태를 시작 시점 baseline으로 정확히 되돌린다. TestSceneSanyo에서 헤드셋으로 텔레포트 → 트럼본이 입 앞에 정렬·추적되고 양손이 grip pose로 교체되는 동작을 확인할 수 있도록 한다.

## Context

Trombone 피처(`docs/specs/trombone/_index.md`)의 첫 sub-spec인 [`01-anchor-and-grip.md`](../specs/01-anchor-and-grip.md)는 트럼본 연주의 *전제 조건*을 박제한다 — 슬라이드 X 이동·MIDI 발음은 02-slide-midi가 후속으로 처리하므로 본 plan은 다루지 않는다. Tech Spec(`../tech-specs/01-anchor-and-grip.md`)이 **단일 컴포넌트(TromboneGripController) + 보조 Transform들**이라는 결합도 높은 시스템으로 설계를 박제했고, ARD 01(`../decisions/01-physicshand-deactivate-scope.md`)이 PhysicsHand SetActive 토글을 본 컴포넌트 단독 책임으로 결정했다. 따라서 본 plan은 spec What 4건 + Tech Spec §Data Flow 5단계(enter / per-frame / exit / Start baseline / 4-step exit)를 하나의 self-contained plan으로 묶어 처리한다.

선례로 `Assets/Instruments/Drum/Scripts/DrumKitStickAnchor.cs`가 거의 동일한 패턴(텔레포트 이벤트 구독 + `PushSourceOverride` 양손 호출 + `PhysicsHand.SetActive` 토글 + Detach 시 pop → destroy 순서)을 갖는다. 차이점은 (a) 드럼은 스틱 prefab을 `Instantiate/Destroy`하는 데 반해 트럼본은 *씬에 미리 배치된 트럼본 GameObject 자체*를 Mouth Transform에 매 프레임 정렬하는 모델이고, (b) 드럼은 `TeleportationAnchor.selectExited` + `LocomotionProvider.locomotionStarted` pending 윈도우를 직접 본다면 트럼본은 이미 존재하는 `TeleportInstrumentProvider.ActiveInstrumentChanged` (= `InstrumentTeleportLink.AnyAnchorTeleported` 한 단계 위 추상) 단일 경로를 구독한다는 점이다. Tech Spec §Invariants에 "전이는 `ActiveInstrumentChanged` 단일 경로로만 발생"이 못 박혀 있으므로 본 plan은 그 단일 진실원만 구독한다.

본 plan 적용 후 sub-spec의 What 4건이 모두 만족되어야 하고, 후속 02-slide-midi sub-spec이 본 plan이 셋업한 RightGripRoot Transform(슬라이드 GameObject 자식)을 슬라이드 X 이동 매핑의 grip 앵커로 그대로 사용할 수 있어야 한다.

## Verified Structural Assumptions

- **`PlayHandPoseDriver.PushSourceOverride(newRoot, newWristRoot)` / `PopSourceOverride()` API 동작 박제 (full file Read)**: (a) `m_OverrideSourceRoot/WristRoot` 필드에 즉시 설정 후 `RefreshActiveSource()` 호출로 *같은 프레임* 안에 source 전환 (one-frame jump 없음). (b) Override는 source priority 최상위 — `(grip override > physics activeInHierarchy > ghost fallback > null)`. (c) `OnEnable`에서 `Application.onBeforeRender += OnBeforeRender` 구독, `LateUpdate`·`OnBeforeRender` 양쪽에서 `SyncDesiredSourceIfChanged()` 자가교정 호출. 즉 외부에서 source GameObject `activeInHierarchy`가 바뀌어도 다음 frame 자동 따라간다. (d) `syncRootTransform == true`(default)이면 매 frame `transform.SetPositionAndRotation(activeSourceRoot.position, activeSourceRoot.rotation)`로 PlayHand root를 source root에 끌어붙임 — 즉 trombone GripRoot가 트럼본·슬라이드의 자식이면 PlayHand는 그 world pose를 자동 추종. (e) push 직후 `RefreshActiveSource → ResetInitialization → TryEnsureInitialized`로 source/target joint map 재구축(이름 매칭, `__Collider_` prefix 무시) — 따라서 GripRoot는 `targetWristRoot`(=PlayHand wrist)와 *동일한 joint 이름 hierarchy*를 가져야 한다. (f) `PopSourceOverride`는 단순히 override 필드를 null로 풀고 priority 재평가 — 즉 양손 `PhysicsHand.SetActive(true)`로 physics source가 다시 깨어나면 자동으로 그 쪽으로 fallback. — 출처: `Read Assets/Hands/Scripts/PlayHandPoseDriver.cs (2026-05-14)`
- **`TeleportInstrumentProvider.ActiveInstrumentChanged` 이벤트 동작 박제**: (a) `OnEnable`에서 `InstrumentTeleportLink.AnyAnchorTeleported` 정적 이벤트 구독, `OnDisable`에서 해지. (b) 텔레포트 발생 시 인수 `InstrumentBase`(= 트럼본 등의 `IActiveInstrument` 구현, null이면 비악기 구역). (c) `ReferenceEquals(_current, next)` 비교로 *값이 실제로 바뀐 경우에만* 이벤트 발화 — 같은 트럼본에 재텔레포트하면 `ActiveInstrumentChanged`가 fire되지 않으므로 본 plan이 그 케이스를 별도 처리할 필요 없음. (d) `_current = next; ActiveInstrumentChanged?.Invoke(_current);` 순서이므로 핸들러 안에서 `Current`를 읽으면 새 값이 보인다. — 출처: `Read Assets/Instruments/_Core/Scripts/TeleportInstrumentProvider.cs (2026-05-14)`
- **`HidePhysicsHandInPlayMode`는 SkinnedMeshRenderer.enabled만 토글, GameObject deactivate는 하지 않음**: `[ExecuteAlways]` + `OnEnable/ApplyVisibility`에서 `GetComponent<SkinnedMeshRenderer>().enabled = !Application.isPlaying`. 따라서 본 plan의 `PhysicsHand.SetActive(false)`와 의미 충돌 없음 — Play 모드에서는 SkinnedMeshRenderer가 이미 disable인 상태로 GameObject 자체를 추가 비활성화. SetActive(true) 복귀 시에도 SkinnedMeshRenderer는 그대로 disabled로 남아 시각적 변화 0. — 출처: `Read Assets/Hands/Scripts/HidePhysicsHandInPlayMode.cs (2026-05-14)`
- **`InstrumentBase`는 abstract — 트럼본 전용 구체 클래스 신규 필요 (`Trombone : InstrumentBase`)**: `InstrumentBase`가 `IPlayable`/`IActiveInstrument` 구현 + abstract `TryResolveNoteOn`을 강제. 본 sub-spec 스코프(앵커·그립)에서는 사운드 발음이 없지만, `InstrumentTeleportLink.linkedInstrument` 필드 타입이 `InstrumentBase`이므로 *씬에 트럼본을 등록하려면 InstrumentBase 상속 컴포넌트가 반드시 부착되어 있어야 한다*. 본 plan은 발음 없는 최소 stub(`TryResolveNoteOn`이 false 반환 + `instrumentId="trombone"` 정도)을 한 파일로 두고, 02-slide-midi가 그 안에 실제 발음 로직을 채우는 분담을 채택한다. — 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-14)` / `Read Assets/Instruments/_Core/Scripts/InstrumentTeleportLink.cs (2026-05-14)`
- **`Instruments` asmdef는 `Hands` asmdef를 이미 references함**: `Assets/Instruments/Instruments.asmdef`의 `references = ["Unity.InputSystem", "Hands", "Unity.XR.Interaction.Toolkit"]`. 본 plan이 추가하는 신규 `.cs` 파일을 `Assets/Instruments/Trombone/Scripts/` 폴더에 두면 자동으로 `Instruments` asmdef에 속하므로 (a) `PlayHandPoseDriver`(global namespace, `Hands` asmdef), (b) `Instruments` namespace 내부 타입(`InstrumentBase` / `IActiveInstrument` / `TeleportInstrumentProvider` 등) 모두 import 가능. 신규 asmdef 추가·기존 asmdef 수정 불요. — 출처: `Read Assets/Instruments/Instruments.asmdef (2026-05-14)` / `Read Assets/Hands/Hands.asmdef (2026-05-14)`
- **씬·트럼본 자산 현재 상태**: `Assets/Instruments/Trombone/`에는 `Models/trombone-sanyo.fbx`만 존재(meta guid `b023dacfa4a39fc478baee199b490c2c`). prefab/Scripts 폴더 없음. TestSceneSanyo(`Assets/Scenes/TestSceneSanyo.unity`)에 `VR Player`(instanceID 52604), `TeleportInstrumentProvider` 부착 오브젝트(52660), `InstrumentTeleportLink` 부착 3개(52606 포함, 드럼/피아노 등 기존 앵커들), `PlayHandPoseDriver` 부착 2개(양손, instanceIDs -2340/-3004), `HidePhysicsHandInPlayMode` 부착 2개(양손 PhysicsHand, -2332/-2996)가 이미 셋업돼 있다. `Main Camera`는 1개(-1762, `VR Player` 하위로 추정). 트럼본 GameObject·트럼본 앵커는 *씬에 아직 없음* — 본 plan이 신규 배치한다. — 출처: `mcp UnityMCP find_gameobjects (2026-05-14)` + `Glob Assets/Instruments/Trombone/** (2026-05-14)`
- **ARD 01 결정 채택**: 트럼본 활성 동안 양손 `PhysicsHand.SetActive(false)` / 이탈 시 `SetActive(true)`는 `TromboneGripController`가 *단독으로 직접 수행*한다. `PlayHandPoseDriver` 본체에 deactivate 책임을 끌어올리지 않으며, 드럼/피아노 grip 흐름(`GripPoseProvider` 경유 `XRGrabInteractable.selectEntered` 체인)도 본 plan 수정 범위 밖. — 출처: `Read docs/specs/trombone/decisions/01-physicshand-deactivate-scope.md (2026-05-14)`

## Approach

1. **신규 컴포넌트 `Trombone : InstrumentBase` (`Assets/Instruments/Trombone/Scripts/Trombone.cs`)** — `Instruments` namespace. `TryResolveNoteOn`은 일단 `playback = default; return false;` stub. 02-slide-midi가 본 클래스에 발음 로직을 채울 자리. `instrumentId="trombone"` default, panel anchor 필요시 직접 셋업.
2. **신규 컴포넌트 `TromboneGripController` (`Assets/Instruments/Trombone/Scripts/TromboneGripController.cs`)** — `Instruments` namespace, `[DisallowMultipleComponent]` + `[DefaultExecutionOrder(10005)]`(DrumKitStickAnchor와 동일). 인스펙터 필드:
   - `Trombone trombone` — 자기 트럼본 (활성 비교 대상).
   - `Transform tromboneRoot` — Mouth 추적 시 `SetPositionAndRotation` 대상이 되는 트럼본 root (일반적으로 `trombone.transform`이지만 별도 root 사용 가능하도록 분리).
   - `Transform mouthAnchor` — VR Player Camera child.
   - `Transform mouthpieceAnchor` — 트럼본 자식. 매 frame `mouthpieceAnchor.world == mouthAnchor.world` 보장.
   - `Transform leftGripRoot` / `leftGripWristRoot` — 트럼본 바디 자식.
   - `Transform rightGripRoot` / `rightGripWristRoot` — 슬라이드 GameObject 자식 (슬라이드 X 이동 시 함께 이동, 02-slide-midi가 그 의존을 그대로 사용).
   - `PlayHandPoseDriver leftPlayHandDriver` / `rightPlayHandDriver` — `PushSourceOverride/PopSourceOverride` 대상.
   - `GameObject leftPhysicsHand` / `rightPhysicsHand` — `SetActive` 토글 대상.
   - `TeleportInstrumentProvider activeInstrumentProvider` — 이벤트 구독원.
3. **Start baseline 캡처** — `Start()`에서 `tromboneRoot.GetPositionAndRotation(out m_BaselinePos, out m_BaselineRot)` 1회. 캡처 후에는 baseline을 수정하지 않는다 (이탈 시 그 정확한 baseline으로만 복귀 = Tech Spec §Invariants 1).
4. **이벤트 구독** — `OnEnable`에서 `activeInstrumentProvider.ActiveInstrumentChanged += OnActiveInstrumentChanged`, `OnDisable`에서 해지. 컴포넌트 비활성/씬 언로드 안전.
5. **enter/exit 분기** — `OnActiveInstrumentChanged(IActiveInstrument next)` 안에서 `bool nextIsMine = ReferenceEquals(next, (IActiveInstrument)trombone);` + 내부 상태 `m_IsActive`와 비교해 4가지 케이스 처리:
   - 이전 inactive → next mine: **Enter 4-step**.
   - 이전 active(mine) → next not mine: **Exit 4-step**.
   - 이전 inactive → next not mine, 이전 active → next mine: no-op.
   - 동일 트럼본 재텔레포트는 `TeleportInstrumentProvider`가 `ReferenceEquals(_current, next)`로 이미 막아 이벤트 자체가 fire되지 않음 (Verified Assumptions 2번).
6. **Enter 4-step** (Tech Spec §Data Flow):
   1. `leftPhysicsHand.SetActive(false); rightPhysicsHand.SetActive(false);` (null guard 포함).
   2. `leftPlayHandDriver.PushSourceOverride(leftGripRoot, leftGripWristRoot);` 동일하게 right. push 직후 같은 frame 안에 source 전환됨.
   3. `m_IsActive = true;` — 추적 활성 플래그.
   4. 첫 frame 정렬은 다음 `LateUpdate`에 자동 적용됨 (Step 7).
7. **매 프레임 Mouth 추적 (`LateUpdate`, `m_IsActive == true`일 때만)** — Tech Spec §Invariants 2: `mouthpieceAnchor.world == mouthAnchor.world` 유지를 위해 트럼본 root world pose를 역계산:
   - 목표: `mouthpieceAnchor.localToWorld := mouthAnchor.localToWorld` (양변이 4x4 same).
   - `mouthpieceAnchor = tromboneRoot * mouthpieceLocal` 이므로 `tromboneRoot.world = mouthAnchor.world * mouthpieceLocal⁻¹`.
   - 즉 `mouthpieceLocal = tromboneRoot.worldToLocalMatrix * mouthpieceAnchor.localToWorldMatrix`를 (선택적으로) Start에서 1회 캐시 (mouthpieceAnchor가 트럼본 root의 *직접* 또는 *간접* 자식이고 그 사이 transform이 정적이라는 가정 하). 안전을 위해 매 frame 재계산해도 비용 미미.
   - 구현: `var target = mouthAnchor.localToWorldMatrix * mouthpieceLocalToTrombone.inverse; tromboneRoot.SetPositionAndRotation(target.GetPosition(), target.rotation);` 형태. `AnchoredStickGhostFollower.SyncToGhost`의 ghost-wrist 역산 패턴(`stickWorld = ghostWorld * wristLocalToRoot.inverse`)을 그대로 차용.
   - `LateUpdate`로 두는 이유: VR Camera/`TrackedPoseDriver` 위치가 갱신된 *이후*에 추적해야 입과 트럼본 사이 frame lag 0. `PlayHandPoseDriver`도 `[DefaultExecutionOrder(10010)]` + `LateUpdate` + `OnBeforeRender`로 양쪽 hook에서 sync — 본 컨트롤러를 `10005`로 두면 PlayHandPoseDriver보다 *먼저* tromboneRoot가 갱신되고, PlayHandPoseDriver가 GripRoot(=트럼본 자식)을 source로 따라가므로 PlayHand world가 같은 frame 안에 일치한다.
8. **Exit 4-step** (Tech Spec §Data Flow):
   1. `tromboneRoot.SetPositionAndRotation(m_BaselinePos, m_BaselineRot);` 원위치 복귀.
   2. `leftPlayHandDriver.PopSourceOverride(); rightPlayHandDriver.PopSourceOverride();` (pop → SetActive 순서는 DrumKitStickAnchor.Detach 패턴과 동일).
   3. `leftPhysicsHand.SetActive(true); rightPhysicsHand.SetActive(true);` PhysicsHand 복원으로 PlayHandPoseDriver source priority가 자동으로 physics source로 fallback.
   4. `m_IsActive = false;` — 추적 플래그 off.
9. **OnDisable 안전망** — 컴포넌트가 활성 상태에서 비활성/언로드되면 `m_IsActive`가 true인 채로 PhysicsHand가 영구 비활성 잔류할 수 있다. `OnDisable`에서 `if (m_IsActive)` Exit 4-step을 한 번 호출해 invariant 보전 (DrumKitStickAnchor의 `OnDisable` PhysicsHand 복원 패턴 차용).
10. **씬 셋업 (TestSceneSanyo)** — Tech Spec §Boundaries에 박제된 신규 자산 추가:
    - **VR Player Camera 자식 `MouthAnchor`** — `Main Camera` 자식에 빈 GameObject. 카메라 local position 기준 입 위치 오프셋(권장: `(0, -0.05, 0.05)` 정도 — 헤드셋 착용 시 카메라 origin이 양 눈 사이이므로 약간 아래·앞). 정확한 값은 manual-hard 검증에서 튜닝.
    - **트럼본 GameObject 배치** — `trombone-sanyo.fbx`를 씬에 인스턴스화해 적절한 테이블 위치로 배치 (baseline). `Trombone` 컴포넌트 부착, `instrumentId="trombone"`. fbx 하위에 다음 child Transform 신규 추가:
      - `MouthpieceAnchor` — 트럼본 마우스피스 끝(입에 닿는 지점) 위치/회전. 트럼본 root 또는 적절한 body 자식 밑에.
      - `LeftGripRoot` + `LeftGripRoot/LeftGripWristRoot` — 트럼본 바디 자식. WristRoot 이름과 자식 joint 이름은 `PlayHandPoseDriver.targetWristRoot`(왼손 PlayHand wrist) 하위 joint 이름들과 *일치*해야 source/target joint map 매칭 (Verified Assumptions 1번 (e)).
      - `RightGripRoot` + `RightGripRoot/RightGripWristRoot` — 슬라이드 GameObject(트럼본 자식 중 슬라이드 부분) 자식. 02-slide-midi가 슬라이드 X 이동 시 그 자식인 RightGrip 전체를 함께 이동시키도록 셋업.
    - **트럼본 앵커** — `TeleportationAnchor` 컴포넌트가 부착된 빈 GameObject + `InstrumentTeleportLink` 컴포넌트 부착, `linkedInstrument`에 위 `Trombone` 참조 할당. 텔레포트 ray가 트럼본 *앞* 위치를 가리키도록 anchor pose 설정.
    - **TromboneGripController 부착** — 트럼본 root 또는 별도 manager GameObject. 모든 인스펙터 참조를 위 자산들로 wiring.
11. **asmdef 확인** — `Assets/Instruments/Trombone/Scripts/`는 `Assets/Instruments/Instruments.asmdef` 범위 안. 신규 asmdef 추가/수정 불필요 (Verified Assumptions 5번). `Trombone.cs` / `TromboneGripController.cs`가 import할 namespace는 (a) `UnityEngine`, (b) `Instruments` 내부 동일 namespace, (c) `PlayHandPoseDriver` (global namespace, `Hands` asmdef는 이미 references됨). 모두 추가 작업 0.

> Tech Spec §Boundaries에 박제된 "건드리지 않는다" 4건은 본 plan Approach에서 모두 회피됨: PlayHandPoseDriver 본체 시그니처 무수정, 드럼/피아노 grip 흐름 무수정, 슬라이드 X 이동·MIDI는 02 미루기, HidePhysicsHandInPlayMode 토글 로직 무수정.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/Trombone.cs` — `InstrumentBase` 상속, 본 sub-spec에서는 발음 stub. 02-slide-midi가 내용 채움.
- `Assets/Instruments/Trombone/Scripts/TromboneGripController.cs` — 앵커 진입·이탈 감지 + Mouth 추적 + 양손 grip override + PhysicsHand 토글 통합 컴포넌트.
- `Assets/Scenes/TestSceneSanyo.unity` — VR Player Camera 하위 `MouthAnchor` Transform 신규 추가, 트럼본 GameObject(`trombone-sanyo.fbx` 인스턴스 + 신규 child Transforms `MouthpieceAnchor`/`LeftGripRoot`+WristRoot/`RightGripRoot`+WristRoot) 배치·`Trombone` 컴포넌트 부착, 트럼본 앵커 GameObject(`TeleportationAnchor` + `InstrumentTeleportLink`) 추가, `TromboneGripController` 부착 + 모든 참조 wiring.

> SampleScene 변경 없음 (sub-spec Out of Scope).

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Instruments/Trombone/Scripts/Trombone.cs`와 `Assets/Instruments/Trombone/Scripts/TromboneGripController.cs`가 존재하고, 두 파일을 추가·수정한 직후 `read_console(types=["error"])`가 0건이다 (컴파일 통과).
- [ ] `[auto-hard]` TestSceneSanyo에서 `find_gameobjects search_method=by_component search_term=TromboneGripController include_inactive=true`가 단일 instance를 반환하고, 그 컴포넌트의 모든 인스펙터 참조 필드(`trombone`/`tromboneRoot`/`mouthAnchor`/`mouthpieceAnchor`/`leftGripRoot`/`leftGripWristRoot`/`rightGripRoot`/`rightGripWristRoot`/`leftPlayHandDriver`/`rightPlayHandDriver`/`leftPhysicsHand`/`rightPhysicsHand`/`activeInstrumentProvider`)가 null이 아니다 (씬 인스턴스 직렬화 정합).
- [ ] `[auto-hard]` TestSceneSanyo에서 `find_gameobjects search_method=by_name search_term=MouthAnchor include_inactive=true`가 단일 instance를 반환하고, 그 parent chain이 VR Player Camera(=Main Camera) 자식이다.
- [ ] `[auto-hard]` TestSceneSanyo에서 `find_gameobjects search_method=by_name search_term=LeftGripRoot include_inactive=true`가 단일 instance를 반환하고, 그 parent chain이 트럼본 바디(`trombone-sanyo.fbx` 인스턴스 root 또는 body 자식) 하위에 있다 (Tech Spec §Components: LeftGripRoot는 트럼본 바디 자식 — 슬라이드 X 이동의 영향을 받지 않아야 함).
- [ ] `[auto-hard]` TestSceneSanyo에서 `find_gameobjects search_method=by_name search_term=RightGripRoot include_inactive=true`가 단일 instance를 반환하고, 그 parent chain이 슬라이드 GameObject(트럼본 자식 중 슬라이드 부분) 하위에 있다 (Tech Spec §Components: RightGripRoot는 슬라이드 자식 — 02-slide-midi가 슬라이드 X 이동 시 RightGrip + PlayHand 전체가 함께 이동하도록 보장하는 의존 박제).
- [ ] `[auto-hard]` TestSceneSanyo에서 `find_gameobjects search_method=by_component search_term=InstrumentTeleportLink include_inactive=true` 결과 중, 트럼본 앵커 GameObject의 `linkedInstrument` 필드가 본 plan이 추가한 `Trombone` 인스턴스를 가리킨다 (드럼/피아노 등 기존 3개 link는 변경 없음).
- [ ] `[manual-hard]` Editor Play 모드 진입 → 헤드셋에서 트럼본 앵커로 텔레포트 → 트럼본이 *시각적으로* 입 앞에 정렬되고 머리를 좌/우/위/아래로 돌려도 마우스피스가 입에 붙어 따라온다 (Tech Spec §Invariants 2 "매 프레임 `mouthpieceAnchor.world == mouthAnchor.world`" 검증). 정렬 위치가 어색하면 `MouthAnchor` local offset을 튜닝한다.
- [ ] `[manual-hard]` 위 진입 상태에서 양손이 *각각 GripPose 모델로 교체*되어 보이고(`PhysicsHand` SkinnedMeshRenderer가 사라지고 GripRoot 위치에 grip 자세 손이 보임), 왼손은 트럼본 바디 고정 위치, 오른손은 슬라이드 위치에 자리잡는다. 두 PhysicsHand GameObject의 `activeInHierarchy`가 Inspector에서 false다 (Tech Spec §Invariants 3).
- [ ] `[manual-hard]` 트럼본 앵커에서 *다른* 텔레포트 앵커(예: 드럼/피아노/비-악기 구역)로 이동 → 트럼본이 *시각적으로* 시작 시점 테이블 위치/회전으로 정확히 복귀하고, 양손이 grip 모델에서 PhysicsHand 모델로 즉시 전환되며 PhysicsHand GameObject가 다시 activeInHierarchy == true가 된다. 트럼본 root world position/rotation이 Start 시점 baseline과 일치한다 (Tech Spec §Invariants 1).
- [ ] `[manual-hard]` 트럼본 앵커 → 트럼본 앵커 자기 자신으로 재텔레포트하는 케이스에서 `TeleportInstrumentProvider`가 ReferenceEquals 가드로 이벤트를 막아 본 컨트롤러의 enter/exit 핸들러가 한 번도 추가 호출되지 않고 상태가 active 그대로 유지된다 (Verified Assumptions 2번 (c)). Console에 enter 로그 추가 없음으로 확인 (디버그 로그 추가하지 않는다 — Inspector의 `PhysicsHand` activeInHierarchy 값이 false 유지로 검증).
- [ ] `[manual-hard]` 02-slide-midi 후속 sub-spec이 `RightGripRoot`를 슬라이드 X 이동에 사용할 수 있도록, 슬라이드 GameObject를 *수동으로* X 방향으로 끌어보면 RightGripRoot/WristRoot와 오른손 PlayHand 자세 전체가 슬라이드와 함께 이동한다 (`PlayHandPoseDriver.syncRootTransform == true`로 source root world를 추종 — Verified Assumptions 1번 (d) 검증).

## Out of Scope

- 슬라이드 X 이동 매핑·MIDI 노트 발음 (→ 02-slide-midi). `Trombone.cs`의 `TryResolveNoteOn`은 본 plan에서 stub으로만 둔다.
- 버튼 트리거 기반 발음 (후속 피처, sub-spec 미할당).
- SampleScene 트럼본 셋업 (sub-spec Out of Scope — TestSceneSanyo에서만 개발).
- 손 포즈 세부 블렌딩·skinning 튜닝 (sub-spec Out of Scope).
- 드럼 스틱 grab의 PhysicsHand deactivate 일관화 (ARD 01 결과: 본 sub-spec 스코프 밖, 별도 cross-cutting ARD 후보).
- `PlayHandPoseDriver` 본체 시그니처·source priority 로직 수정 (Tech Spec §Boundaries).
- `HidePhysicsHandInPlayMode`의 SkinnedMeshRenderer 토글 로직 수정 (Tech Spec §Boundaries).

## Notes

- 본 plan은 `TromboneGripController`를 *단일 컴포넌트*로 묶었지만, 향후 02-slide-midi에서 슬라이드 X 이동 매핑이 들어가면 `TromboneSlideController` 같은 분리 컴포넌트가 별도 추가될 수 있다 — 본 컨트롤러는 grip/추적까지만 책임진다.
- `MouthAnchor`의 local position/rotation은 헤드셋 착용자별 미세 차이가 있으므로, 본 plan은 초기 권장값을 정하고 manual-hard 검증에서 튜닝하는 방식을 채택한다. 후속 사용자 캘리브레이션 기능은 sub-spec 추가 후보.
- `Trombone : InstrumentBase` stub의 `audioOutput`은 본 plan에서 셋업하지 않아도 되지만, `InstrumentBase.Initialize`가 null이면 `enabled = false` 처리하므로 향후 02-slide-midi 적용 시점에 셋업이 필요하다. 본 plan에서는 컴파일·앵커 동작에만 영향 없도록 `audioOutput` 인스펙터 미할당으로 두거나 비어 있는 `InstrumentAudioOutput` 자식을 미리 두고 채움. 후자가 02 작업 부담 줄임.

## Handoff

본 plan 완료 후 02-slide-midi에 넘길 공개 인터페이스·셋업 사실:

- `Trombone` 컴포넌트(`Assets/Instruments/Trombone/Scripts/Trombone.cs`)가 TestSceneSanyo의 트럼본 GameObject에 부착돼 있고, `instrumentId="trombone"`. `TryResolveNoteOn`은 false 반환 stub — 02-slide-midi가 슬라이드 구간별 MIDI 노트 해석으로 채운다.
- `TromboneGripController.rightGripRoot/rightGripWristRoot`는 슬라이드 GameObject의 자식 Transforms. 02-slide-midi가 슬라이드 X 이동 로직을 슬라이드 GameObject에 적용하면 RightGrip + PlayHand 전체가 자동으로 따라온다 (`PlayHandPoseDriver.syncRootTransform == true` 동작).
- 슬라이드 GameObject(트럼본 자식 중 슬라이드 부분)의 정확한 hierarchy 경로는 본 plan의 prefab 셋업 결과로 박제됨. 02 시작 시 `find_gameobjects search_method=by_name`으로 확정 경로 재확인.
- `MouthAnchor` 캘리브레이션 값(VR Player Camera local 기준)이 manual-hard 검증에서 확정됐으면 그 값을 02에 그대로 전달 — 02는 이 anchor를 변경하지 않는다.
- `TeleportInstrumentProvider.ActiveInstrumentChanged` 이벤트 단일 진실원은 본 plan 이후에도 변경 없음 — 02는 추가 이벤트 구독자를 두지 않고 본 컨트롤러 안에서 enter/exit 핸들러를 확장할지, 별도 컴포넌트로 분리할지 02 plan-drafter가 결정.
