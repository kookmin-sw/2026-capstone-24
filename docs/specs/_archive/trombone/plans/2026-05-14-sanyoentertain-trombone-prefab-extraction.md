# Trombone Prefab 추출 및 자식 Transform 재정착

**Linked Spec:** [`../specs/01-anchor-and-grip.md`](../specs/01-anchor-and-grip.md)
**Caused By:** [`2026-05-14-sanyoentertain-trombone-grip-controller.md`](2026-05-14-sanyoentertain-trombone-grip-controller.md)
**Status:** `Done`

## Goal

`Assets/Instruments/Trombone/Prefabs/Trombone.prefab` wrapper prefab을 신규 생성하고, `trombone-sanyo.fbx` 인스턴스를 **unpack**해 fbx 내부 `Body`·`Slide` mesh GameObject가 wrapper의 *직속* 자식이 되도록 박제한다. `LeftGripRoot/L_Wrist`는 `Body` 자식, `RightGripRoot/R_Wrist`는 *fbx 본연의* `Slide` 자식(별도 빈 Slide 컨테이너 만들지 않음). 씬에는 그 prefab의 PrefabInstance 1건만 남기고 내부 7개 참조는 자동 보존·외부 6개 참조는 PrefabInstance override로 wiring한다. 드럼 `DrumKit.prefab` 선례를 1:1 답습 — DrumKit이 fbx의 mesh(BassDrum/Snare 등)를 wrapper의 직속 자식 또는 nested prefab으로 풀어 둔 패턴 그대로.

## Context

이전 plan(`...-grip-controller.md`)이 sub-spec What 4건과 Tech Spec §Data Flow를 만족하는 `Trombone` / `TromboneGripController` 컴포넌트와 씬 wiring을 완료했지만, **Tech Spec §Boundaries line 30 "건드린다: 트럼본 prefab — child Transform 추가" 박제를 누락**해 `trombone-sanyo.fbx` 인스턴스와 자식 Transform들을 모두 `Assets/Scenes/TestSceneSanyo.unity`의 *씬 단*에 직접 박았다. 결과: 트럼본 단위 자산이 prefab으로 묶이지 않아 재사용·내부 참조 박제·`Prefabs/` 폴더 일관성 모두 깨짐.

사용자 지적 (2026-05-14 1차): "trombone-sanyo model만 있고 prefab이 없어. Prefab을 만들어서 거기에 Mouth Anchor랑 그런것들을 넣어야지." — manual-hard 검증 직전에 인지되어 이전 plan은 사용자 stop 결정으로 `In Progress` 유지(commit 미생성).

사용자 지적 (2026-05-14 2차, 본 plan 1차 실행 후): "DrumKit을 봐봐. 이거는 unpack한 후, Prefab으로 만들어서, 바로 Trombone 아래에 Slide, Body가 있어야지. 그리고 왜 지금 Slide가 이미 Trombone model에 있는데도 새로 만들었는지 의문이야." — 본 plan 1차 실행은 (a) fbx instance를 nested PrefabInstance로 그대로 두어 Body/Slide가 wrapper의 *직속 자식이 아님*, (b) fbx 내부에 이미 Slide GameObject가 있음에도 *별도 빈 Slide 컨테이너*를 추가로 만들어 hierarchy 중복을 발생시켰음. 또한 fileID 100100000 계열을 raw YAML로 쓰는 바람에 Unity가 prefab 인스턴스를 씬에서 로드하지 못하는 PPtr cast 에러까지 동반. 본 Approach 재설계는 그 2차 지적을 반영한다 — fbx를 **unpack**해 Body/Slide를 wrapper 직속 자식으로 풀어내고, fbx 본연의 Slide GameObject를 RightGrip의 부모로 사용한다.

본 plan Done 시점에 두 plan을 함께 `_archive/`로 이동한다. 이전 plan의 코드 산출물(`Scripts/Trombone.cs` / `TromboneGripController.cs`)은 본 plan에서 *재구축 없이* 그대로 사용된다. 이전 plan의 씬 작업은 1차 본 plan 실행 직후 사용자 stop 결정으로 working tree에서 `git restore`로 되돌려졌으며, 본 2차 Approach가 plan 1·plan 2를 **씬·prefab 산출물 단일 패스로 재구축**한다. MouthAnchor는 VR Player Camera (`Main Camera`) 자식이므로 prefab 외부로 명시 유지.

## Verified Structural Assumptions

- **drum prefab 선례 — fbx unpack 후 mesh 직속 패턴 박제:** `Assets/Instruments/Drum/Prefabs/DrumKit.prefab` get_hierarchy 결과 44개 GameObject 중, 각 드럼 부분(BassDrum/Snare/HighTom 등)이 *별도 nested prefab*으로 분리되어 wrapper 직속 자식. nested prefab 내부에는 fbx에서 추출된 mesh GameObject(bass_drum/snare/snare_stand 등)가 wrapper 자식의 *직속*에 풀려 있음. 즉 DrumKit은 fbx를 *unpack한 후* mesh를 자식으로 풀어 wrapper에 직접 매다는 패턴. nested fbx PrefabInstance를 그대로 두지 않는다. — 출처: `mcp UnityMCP manage_prefabs get_hierarchy Assets/Instruments/Drum/Prefabs/DrumKit.prefab (2026-05-14)`.
- **fbx 자체 (`trombone-sanyo.fbx`):** Unity가 fbx instance를 unpack하면 fbx 내부 root GameObject(`trombone-sanyo`) 아래 *직접 자식*들(`Body`, `Slide`)이 unpack 부모의 직속 자식으로 풀린다. 본 plan 1차 실행에서 확인된 fbx 내부 hierarchy는 `trombone-sanyo/{Body, Slide}` 2개 mesh GameObject(각 Transform + MeshFilter + MeshRenderer). Slide는 fbx에 *이미 존재*하므로 별도 빈 Slide 컨테이너 생성 금지. — 출처: `mcp UnityMCP manage_prefabs get_hierarchy Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-14, 1차 실행 결과)`.
- **fbx unpack 절차:** `manage_gameobject action=create prefab_path=Assets/Instruments/Trombone/Models/trombone-sanyo.fbx`로 씬에 instance 1건 생성 → `PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction)` 호출하면 fbx 내부 GameObject들이 unpack root의 직속 자식이 된다. execute_code 미사용 시 `manage_prefabs action=open_prefab_stage`로 prefab 편집 stage에서 unpack은 불가하므로, 본 plan은 *씬에서* unpack 수행 후 prefab으로 다시 저장하는 흐름을 채택. — 출처: `Unity ScriptReference PrefabUtility.UnpackPrefabInstance (2026-05-14)` + drum prefab 선례 답습.
- **prefab 저장 시 fileID 발급 — raw YAML 작성 금지 박제:** Unity prefab의 `m_SourcePrefab: {fileID: 100100000, ...}` reference는 sentinel 값. prefab 내부 fileID 정의에 `100100000` 계열을 사용하면 PPtr cast 실패. **반드시** `manage_prefabs action=modify_contents` 또는 `manage_gameobject save_as_prefab=true`로 Unity API를 통해 prefab을 저장해 valid random 큰 fileID(예: DrumKit의 `618647493018728085`) 자동 발급을 받는다. raw YAML 직접 작성 금지. — 출처: 본 plan 1차 실행에서 발생한 `PPtr cast failed: GameObject to Prefab at FileID 100100000` 에러 5+건 + DrumKit.prefab의 valid fileID 패턴 확인.
- **이전 plan 코드 무수정:** `Assets/Instruments/Trombone/Scripts/Trombone.cs` (Instruments namespace, `InstrumentBase` stub) + `TromboneGripController.cs` (13개 인스펙터 필드 — 내부 7 + 외부 6). 본 plan은 C# 코드 변경 0. — 출처: `Read Assets/Instruments/Trombone/Scripts/*.cs (2026-05-14)`.
- **XRIT TeleportationAnchor select 메커니즘 박제:** `BaseTeleportationInteractable`은 `IXRSelectInteractable`. XRRayInteractor가 `Physics.Raycast`로 collider hit → `XRInteractionManager`가 `collider.instanceID → interactable` 매핑 lookup → interactable의 `interactionLayers`와 ray interactor의 `interactionLayers`가 AND 비트마스크 매칭 (`(a & b) != 0`) 통과해야 select. anchor의 `colliders` 리스트는 `XRBaseInteractable.RegisterInteractable` 시점에 매핑 셋업. 본 plan 2차 manual-hard 검증 실패는 layer mismatch가 원인 — Trombone anchor `interactionLayers=1` vs Teleport Interactor `-2147483648`. — 출처: 본 세션 dump `mcpforunity://scene/gameobject/-3844/component/XRRayInteractor` + `XRIT 3.3.1` 내부 동작 박제.
- **InstrumentTeleportColliderBinder 보조 동작:** `Start()`에서 `transform.parent`(없으면 self)의 자식 collider를 모두 `TeleportationAnchor.colliders`에 추가. `excludeTriggers=true`/`excludeRigidbodies=true` default. 이건 anchor의 hit area를 *본체 collider까지 확장*하는 부수적 helper일 뿐, anchor select 자체의 필수조건은 anchor 자신의 collider. Piano/DrumKit이 본체 키·헤드 collider도 등록되는 건 연주용 collider의 부수효과. — 출처: `Read Assets/Instruments/_Core/Scripts/InstrumentTeleportColliderBinder.cs (2026-05-14)`.
- **씬 출발점:** `Assets/Scenes/TestSceneSanyo.unity`는 본 plan 1차 실행 후 사용자 stop 결정으로 `git restore`되어 plan 1 적용 *이전* 상태로 복귀. rootCount=18, Unity 컴파일·콘솔 에러 0. trombone 관련 GameObject 일체 없음. — 출처: `mcp UnityMCP manage_scene get_active (2026-05-14)` + `mcp UnityMCP read_console (2026-05-14)`.

## Approach

> 본 Approach는 plan 1·plan 2를 *씬·prefab 산출물 단일 패스*로 재구축한다. plan 1의 C# 코드(`Scripts/Trombone.cs`, `TromboneGripController.cs`)는 변경 0. 모든 작업은 Unity MCP `manage_gameobject` / `manage_components` / `manage_prefabs` / `manage_scene`으로 수행하고, raw YAML 작성·`execute_code` 사용은 금지(1차 실행에서 PPtr cast 에러를 일으킨 원인이라 박제).

1. **사전 점검.** `read_console types=["error"]` 0건 확인. `Glob Assets/Instruments/Trombone/Prefabs/*.prefab` 미존재 확인. `manage_scene get_active`로 TestSceneSanyo 활성 + rootCount 기록. 씬에 `Trombone`/`MouthAnchor`/`TromboneAnchor` 부류 GameObject 일체 부재 확인(`find_gameobjects`).

2. **fbx 인스턴스 + unpack.**
   - `manage_gameobject action=create prefab_path=Assets/Instruments/Trombone/Models/trombone-sanyo.fbx position=[<table-pose>] rotation=[0,0,0,1]` — 씬 root-level에 fbx PrefabInstance 1건. world position은 *테이블 위치 baseline*으로 직접 셋업(권장 시작값 `(1.669, 0.817, 1.603)` — 이전 plan 1차 실행 박제값; manual-hard에서 미세 조정 가능).
   - fbx instance를 **unpack**: `manage_gameobject action=modify`의 unpack 옵션 또는 `manage_prefabs unlink_if_instance=true` 또는 `manage_components`의 unpack instruction을 사용. 도구 레벨에서 unpack 단일 액션이 없으면 *후속 reparent에서 worldPositionStays=true로 자식만 끌어옴*으로써 effectively unpack 효과(fbx instance container는 자식이 비워진 채 남고 삭제). 어느 경로든 fbx 내부 `Body` / `Slide` GameObject(Transform + MeshFilter + MeshRenderer)가 unpack 부모 또는 wrapper의 *직속 자식*으로 풀려야 한다.

3. **wrapper GameObject `Trombone` 생성.** `manage_gameobject action=create name=Trombone parent=<scene root> position=[<table-pose>]` — 빈 GameObject. world pose는 fbx 인스턴스와 같은 baseline. fbx의 자식 `Body`/`Slide`를 `manage_gameobject action=modify parent=Trombone worldPositionStays=true`로 wrapper의 직속 자식으로 reparent. fbx 빈 container GameObject(`trombone-sanyo` 또는 unpack 흔적)는 삭제.

4. **wrapper에 컴포넌트 부착.** `manage_components action=add component_type=Instruments.Trombone target=Trombone` + `component_type=Instruments.TromboneGripController`. Trombone의 `instrumentId="trombone"` set. TromboneGripController 13개 필드는 다음 단계에서 wiring.

5. **자식 Transform 추가 (anchor·grip).**
   - `manage_gameobject action=create name=MouthpieceAnchor parent=Trombone/Body position=<마우스피스 끝>` — Body의 자식. 위치는 *트럼본 마우스피스 끝 = 입에 닿는 지점*. manual-hard에서 튜닝.
   - `manage_gameobject action=create name=LeftGripRoot parent=Trombone/Body position=<바디 고정 그립>` → 자식 `name=L_Wrist parent=Trombone/Body/LeftGripRoot`. L_Wrist 하위 joint 이름은 PlayHandPoseDriver.targetWristRoot 매칭 위해 *Hands/L_Wrist prefab과 동일*해야 하지만, 본 plan은 빈 Transform 1개로 시작 — 02-slide-midi 또는 별도 후속 plan에서 PlayHand joint hierarchy 복제.
   - `manage_gameobject action=create name=RightGripRoot parent=Trombone/Slide position=<슬라이드 그립>` → 자식 `name=R_Wrist parent=Trombone/Slide/RightGripRoot`. **RightGripRoot의 부모는 fbx 본연의 Slide GameObject — 별도 빈 Slide 컨테이너 생성 금지.** 02-slide-midi가 `Trombone/Slide.transform.localPosition.x`를 조작하면 RightGripRoot + 오른손 PlayHand가 자동 추종.

6. **TromboneAnchor 추가.** `manage_gameobject action=create name=TromboneAnchor parent=Trombone position=<텔레포트 위치>` → `manage_components action=add`로 `UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor` + `UnityEngine.BoxCollider` + `Instruments.InstrumentTeleportLink` + `Instruments.InstrumentTeleportColliderBinder`. **필수 셋업** (Piano/DrumKit 답습):
   - `BoxCollider.center = (0, 0.025, 0)`, `BoxCollider.size = (1, 0.05, 1)` — ray hit area 표식, ground 50mm 두께.
   - `TeleportationAnchor.interactionLayers = -1` (Everything). **이게 핵심** — 기본값 `1` (Default layer)이면 XRRayInteractor.interactionLayers=`-2147483648` (Teleport bit 31)와 mismatch해 select 불가 (`1 & -2147483648 = 0`). 1차 manual-hard 검증 실패 원인 박제.
   - `TeleportationAnchor.teleportAnchorTransform = <자기 Transform>` — player가 텔레포트할 위치.
   - `TeleportationAnchor.matchOrientation = 2` (WorldSpaceUp), `teleportTrigger = 0` (OnSelectExited).
   - InstrumentTeleportLink.linkedInstrument는 다음 단계에서 prefab 내부 Trombone 컴포넌트로 wiring.

7. **AudioOutput 빈 자식 추가.** `manage_gameobject action=create name=AudioOutput parent=Trombone` — 컴포넌트 미부착. 02-slide-midi가 InstrumentAudioOutput 부착 + 라우팅 셋업 예정.

8. **TromboneGripController 내부 7개 + InstrumentTeleportLink 1개 wiring (live scene).** `manage_components action=set_property target=Trombone`:
   - `trombone` → `Trombone` 컴포넌트 자기 참조 (instanceID)
   - `tromboneRoot` → Trombone wrapper Transform
   - `mouthpieceAnchor` → `Trombone/Body/MouthpieceAnchor` Transform
   - `leftGripRoot` → `Trombone/Body/LeftGripRoot` Transform
   - `leftGripWristRoot` → `Trombone/Body/LeftGripRoot/L_Wrist` Transform
   - `rightGripRoot` → `Trombone/Slide/RightGripRoot` Transform
   - `rightGripWristRoot` → `Trombone/Slide/RightGripRoot/R_Wrist` Transform
   - `manage_components action=set_property target=TromboneAnchor component_type=InstrumentTeleportLink property=linkedInstrument value=<Trombone instanceID>` — prefab 내부 self-reference.

9. **MouthAnchor 신규 추가.** `manage_gameobject action=create name=MouthAnchor parent=Main Camera localPosition=[0,-0.05,0.05]` — VR Player Main Camera 자식. world offset은 *헤드셋 카메라 origin 기준 입 위치*; manual-hard에서 튜닝.

10. **prefab 저장.** `manage_gameobject action=modify save_as_prefab=true prefab_path=Assets/Instruments/Trombone/Prefabs/Trombone.prefab` (또는 동등 `manage_prefabs` 액션). Unity API 경로로 저장 → valid random fileID 자동 발급. 저장 직후 wrapper는 자동으로 새 prefab의 PrefabInstance로 치환된다.

11. **씬 외부 6개 참조 wiring (PrefabInstance override).** `manage_components action=set_property target=Trombone component_type=TromboneGripController`:
   - `mouthAnchor` → `Main Camera/MouthAnchor` Transform
   - `leftPlayHandDriver` → `VR Player/Camera Offset/Hands/Left/LeftPlayHand` PlayHandPoseDriver
   - `rightPlayHandDriver` → `VR Player/Camera Offset/Hands/Right/RightPlayHand` PlayHandPoseDriver
   - `leftPhysicsHand` → `VR Player/.../Left/LeftPhysicsHand` GameObject
   - `rightPhysicsHand` → `VR Player/.../Right/RightPhysicsHand` GameObject
   - `activeInstrumentProvider` → 씬의 `InstrumentSystem`(`TeleportInstrumentProvider` 부착) GameObject
   - 모든 set은 PrefabInstance override로 자동 등록.

12. **씬 저장 + refresh.** `manage_scene action=save` → `refresh_unity force` → `read_console types=[error,warning] count=50` 0건 확인.

13. **자동 AC 직접 실행.** §Acceptance Criteria의 §1~§7을 메인 세션이 evidence 박제해 통과 처리.

> Tech Spec §Boundaries "건드린다: 트럼본 prefab — child Transform 추가" 박제 충족. PlayHandPoseDriver / 드럼·피아노 grip 흐름 / HidePhysicsHandInPlayMode / 슬라이드 X 이동·MIDI 모두 무수정.

## Deliverables

- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` **신규** (+ `.meta`). 내부 hierarchy:
  ```
  Trombone (Trombone + TromboneGripController)
  ├── Body (mesh — fbx unpack 산출)
  │   ├── MouthpieceAnchor
  │   └── LeftGripRoot
  │       └── L_Wrist
  ├── Slide (mesh — fbx unpack 산출, **fbx 본연 GameObject**)
  │   └── RightGripRoot
  │       └── R_Wrist
  ├── TromboneAnchor (TeleportationAnchor + BoxCollider + InstrumentTeleportLink → 자기 Trombone 컴포넌트)
  └── AudioOutput (빈, 02-slide-midi가 채움)
  ```
- `Assets/Scenes/TestSceneSanyo.unity` **수정**: `Trombone.prefab` PrefabInstance 1건 추가 (table baseline pose) + 외부 6개 참조 PrefabInstance override 등록 + VR Player Main Camera 자식으로 `MouthAnchor` 빈 GameObject 추가(AddedGameObject).

> 이전 plan의 `Assets/Instruments/Trombone/Scripts/{Trombone.cs, TromboneGripController.cs}`는 **무변경**. 본 plan은 자산 재구조화·씬 셋업만.

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 파일이 존재한다. **검증:** `Glob Assets/Instruments/Trombone/Prefabs/*.prefab` 결과 1건 이상 + 항목명 `Trombone.prefab`.
- [ ] `[auto-hard]` 위 prefab의 `.meta` guid 필드가 비어 있지 않다. **검증:** `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab.meta` 헤드 guid 줄 정합.
- [ ] `[auto-hard]` `Assets/Scenes/TestSceneSanyo.unity`에 Trombone.prefab의 PrefabInstance가 정확히 1건. **검증:** `Grep "m_SourcePrefab.*<Trombone.prefab guid>" TestSceneSanyo.unity` 결과 1건.
- [ ] `[auto-hard]` 그 PrefabInstance의 `m_AddedGameObjects` 리스트가 비어 있다. **검증:** PrefabInstance 블록 grep `m_AddedGameObjects: \[\]` 또는 빈 리스트.
- [ ] `[auto-hard]` **prefab fileID 정합**: `Trombone.prefab` 본문의 `--- !u!` 정의가 모두 random 큰 fileID(자릿수 ≥10)이며, `100100000`/`200100000` 계열 sentinel ID를 사용하지 않는다. **검증:** `Grep "^--- !u!" Trombone.prefab` 결과의 모든 `&XXX`가 `^&[1-9][0-9]{10,}$` 패턴.
- [ ] `[auto-hard]` **재구조 hierarchy**: prefab 내부에 `Trombone/Body`(mesh) / `Trombone/Slide`(mesh) / `Trombone/Body/MouthpieceAnchor` / `Trombone/Body/LeftGripRoot/L_Wrist` / `Trombone/Slide/RightGripRoot/R_Wrist` / `Trombone/TromboneAnchor` / `Trombone/AudioOutput`이 존재하고, **별도 빈 `Slide` 컨테이너가 추가로 존재하지 않는다** (fbx 본연 Slide만 1건). **검증:** `manage_prefabs get_hierarchy Trombone.prefab`의 `path` 필드 모음에서 `Trombone/Slide`는 1건만 + `Trombone/trombone-sanyo` 부재 + `LeftGripRoot` parent path는 `Trombone/Body` + `RightGripRoot` parent path는 `Trombone/Slide`.
- [ ] `[auto-hard]` `find_gameobjects by_component=TromboneGripController include_inactive=true` 단일 instance + 13개 필드 모두 non-null + 내부 7개 필드(`trombone`/`tromboneRoot`/`mouthpieceAnchor`/`leftGripRoot`/`leftGripWristRoot`/`rightGripRoot`/`rightGripWristRoot`)의 parent chain이 wrapper PrefabInstance root에 도달. **검증:** `mcpforunity://scene/gameobject/<id>/component/TromboneGripController` resource dump.
- [ ] `[auto-hard]` `mouthAnchor` 필드 Transform의 parent chain이 `Main Camera`에 도달하고 wrapper PrefabInstance에는 *도달하지 않는다*. **검증:** scene YAML `m_Father` chain trace 2건 비교.
- [ ] `[auto-hard]` `refresh_unity force` → `manage_scene load` → `read_console types=["error"] count=50` 0건. **검증:** console output 그렙. 1차 실행에서 발생한 `PPtr cast failed: GameObject to Prefab at FileID 100100000`이 재현되지 않음.
- [ ] `[manual-hard]` 선행 plan(`2026-05-14-sanyoentertain-trombone-grip-controller.md`)의 manual-hard 5건이 이 plan 적용 후 재검증에서 통과한다: (a) 트럼본 앵커 텔레포트 → 마우스피스 입에 붙어 추적, (b) 양손 grip 모델 교체 + PhysicsHand activeInHierarchy=false, (c) 다른 앵커로 이동 → baseline 복귀 + PhysicsHand 복원, (d) 동일 앵커 재텔레포트 → no-op, (e) 슬라이드 GameObject 수동 X 이동 → RightGripRoot + 오른손 PlayHand 동기 이동. **검증:** Editor Play 모드 헤드셋 1회 시퀀스.
- [ ] `[manual-hard]` Trombone.prefab을 *별도 위치*에 추가 인스턴스해도 (a) 자식 hierarchy 그대로 유지, (b) prefab 내부 `InstrumentTeleportLink.linkedInstrument`가 *새 instance의 Trombone 컴포넌트*를 가리킴 (cross-instance 오염 없음), (c) 새 instance의 외부 6개 참조 필드는 null/missing (씬마다 wiring 필요 노출). **검증:** prefab을 빈 GameObject 자식으로 drag-drop 후 Inspector 1회 확인.

## Risks

- **fbx unpack 단일 액션 부재.** Unity MCP 도구에 unpack 액션이 직접 노출돼 있지 않을 가능성 — `manage_gameobject action=modify worldPositionStays=true`로 fbx 자식만 wrapper로 끌어 옮기는 *effectively unpack* 우회를 채택. fbx instance container는 자식이 비워진 채 남아 삭제. 도중 prefab override 충돌이 발생하면 사용자에게 멈춤·중간 보고.
- **Joint name 매칭.** L_Wrist / R_Wrist 하위 joint 이름은 PlayHandPoseDriver.targetWristRoot 매칭 필수 — 본 plan은 빈 Transform 1개로만 시작하므로 plan 1 manual-hard (a)(b)에서 양손 grip 모델이 *Wrist에 정렬은 되지만 손가락 joint가 비어* 모델 자세가 손가락까지 정확히 잡지 않을 수 있음. 후속 plan에서 joint hierarchy 복제 처리.
- **execute_code 사용 금지.** 1차 실행에서 raw YAML 작성으로 fileID sentinel(100100000) 사용해 PPtr cast 실패가 발생했고, 또한 본 세션에서 `execute_code` 자체가 "filename or extension is too long" 환경 에러로 사용 불능 상태. 본 plan은 *모든* prefab/씬 변경을 `manage_*` MCP로만 수행하고 `execute_code`를 사용하지 않는다.
- **YAML 가드 hook (`scripts/guard-unity-yaml.ps1`).** `.unity`/`.prefab` 등 Unity 직렬화 자산의 직접 텍스트 Edit은 가드로 차단. `UNITY_YAML_OVERRIDE=1`은 메인 세션 환경에서 set해도 새 PowerShell 프로세스로 전파되지 않으므로 사실상 사용 불가. 모든 자산 수정은 MCP `manage_*`로 수행한다.
- **baseline pose 보존.** wrapper world pose가 prefab 저장 도중 일시 reset될 가능성 — 저장 직전 wrapper Transform world pose snapshot 후 저장 직후 동일 값 set으로 보강. 1차 실행에선 0,0,0으로 리셋되지 않았으므로 risk 낮음.
- **audioOutput null.** Trombone.cs Initialize() 동작은 본 plan에서 무변경 — 02-slide-midi 적용 전까지 audioOutput null 허용. AudioOutput GameObject만 빈 컨테이너로 미리 둠.

## Out of Scope

- 슬라이드 X 이동 매핑 / MIDI 노트 발음 / AudioOutput(InstrumentAudioOutput) 부착 — 02-slide-midi.
- MouthAnchor local offset 캘리브레이션 — 이전 plan manual-hard에서 결정된 값 그대로 유지.
- 손 포즈 세부 블렌딩 / skinning 튜닝 — sub-spec Out of Scope.
- SampleScene 트럼본 셋업 — sub-spec Out of Scope (TestSceneSanyo만).
- Trombone_Sanyo.prefab variant — 사용자별 차이 명확해질 때까지 보류.
- TromboneGripController 외부 6개 참조 자동 wiring helper — 별도 sub-spec 후보.

## Notes

- 이전 plan(`...-grip-controller.md`)은 `In Progress`(commit 미생성) 상태. 본 plan Done 시 두 plan을 *동반 archive* 처리 — plan-complete 호출 시 두 plan 모두 `_archive/`로 이동.
- atomic commit 1건에 다음을 묶음: 이전 plan 산출물(Scripts/.cs 2건 신규) + 본 plan 산출물(Trombone.prefab + .meta + TestSceneSanyo.unity diff) + spec-build phase 산출물(ARD 01·tech-spec edit·plan 2건) + plan-complete 라이프사이클 docs(sub-spec/_index/README 보드 갱신, 두 plan archive 이동).
- **2차 Approach (본 버전)**: 1차 Approach는 fbx instance를 nested PrefabInstance로 그대로 두고 별도 빈 Slide 컨테이너를 만들어 hierarchy 중복 + raw YAML로 fileID sentinel을 사용해 PPtr cast 실패를 일으켰다. 2차 Approach는 fbx unpack + 별도 Slide 제거 + Unity API 경로 prefab 저장으로 모두 해결. 1차 결과물은 working tree에서 `git restore` + `rm`으로 제거됐다 (씬·prefab·부산물 Editor/scripts 일체).

## Handoff

- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 신설. wrapper root에 `Trombone` + `TromboneGripController` 컴포넌트. 자식 hierarchy: `Body` (fbx unpack mesh) → `MouthpieceAnchor` / `LeftGripRoot` → `L_Wrist`; `Slide` (fbx 본연 mesh) → `RightGripRoot` → `R_Wrist`; `TromboneAnchor` (TeleportationAnchor + InstrumentTeleportLink → self); `AudioOutput` (빈, 02가 채움).
- Slide GameObject path: `Trombone/Slide` (PrefabInstance 기준). fbx 본연 mesh이며 *별도 빈 컨테이너가 아님*. 02-slide-midi가 `Trombone/Slide.transform.localPosition.x`를 조작하면 RightGripRoot + 오른손 PlayHand가 자동 추종.
- AudioOutput 자식 GameObject는 빈 상태로 미리 두었음. 02가 InstrumentAudioOutput 컴포넌트 부착 + routing 셋업 + Trombone.cs.audioOutput 참조 wiring 담당.
- 외부 6개 참조(`mouthAnchor`, `leftPlayHandDriver`, `rightPlayHandDriver`, `leftPhysicsHand`, `rightPhysicsHand`, `activeInstrumentProvider`)는 씬마다 PrefabInstance override로 wiring 필요. SampleScene 도입 시 동일 패턴 반복.
- `TromboneAnchor.InstrumentTeleportLink.linkedInstrument`는 prefab asset 안에서 이미 박혀 있음 — 씬 wiring 불요.
- prefab fileID는 모두 Unity 자동 발급 random 큰 ID. 향후 prefab 편집 시 raw YAML로 새 fileID를 추가하지 말 것 (반드시 Unity API 경로 사용).
- L_Wrist / R_Wrist 하위 joint hierarchy는 본 plan에서 빈 Transform 1개로만 시작했음. 손가락 자세 정합성이 필요해지면 PlayHand wrist hierarchy를 복제해 채우는 후속 plan 후보.
