# Wire three-hand visual & tracking-source fallback for validation

**Linked Spec:** [`01-three-hand-architecture-validation.md`](../specs/01-three-hand-architecture-validation.md)
**Status:** `Done`

## Goal

3-hand 구조(Ghost / Physics / Play)의 신호 흐름을 시각과 로직 양쪽에서 검증 가능하게 만든다. Ghost는 투명 파랑, Physics는 평소 비표시(디버그 fallback 투명 빨강)로 분리하고, Play는 Physics GameObject의 활성 상태에 따라 Physics ↔ Ghost로 추적 소스를 전환한다.

## Context

- 부모 spec(`hands/_index.md`)은 입력 추적 / 물리 충돌 / 시각 표현을 Ghost / Physics / Play 세 핸드에 분리한 구조를 명시한다. 이 구조는 코드·프리팹에 이미 존재하지만, 시각이 분리돼 있지 않고 Play의 추적 소스가 Physics에 정적으로 와이어링돼 "Physics 비활성 시 Play가 Ghost로 폴백"이라는 sub-spec의 핵심 동작이 검증된 적이 없다.
- 후속 sub-spec [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)이 본격 비통과 물리(non-kinematic 전환)와 locomotion 시 Physics 일시 중단을 도입하기 전에, 이 신호 흐름이 의도대로 동작함을 시각적으로 확인할 수 있어야 한다. 본 plan은 02의 전제 조건이다.
- 머터리얼 결정 — Linked Spec 본문에 따라 본 검증 단계에서 정의한 색·투명도 머터리얼은 그대로 유지하며 출시용으로 다시 다듬지 않는다.
- "Physics 비활성"의 정의 — Linked Spec은 Physics 핸드 GameObject `SetActive(false)`로 정의한다. 본 plan에서는 Play 입장에서 안전하게 판단하기 위해 `physicsSourceRoot.gameObject.activeInHierarchy`를 사용한다(부모 컨테이너가 꺼졌을 때도 일관).
- **현재 코드 사실**
  - `Assets/Hands/Scripts/PlayHandPoseDriver.cs` — 단일 `sourceRoot`/`sourceWristRoot`를 받아 본 transform을 미러링. grip pose용 `PushSourceOverride`/`PopSourceOverride`로 런타임 소스 교체가 가능하며 내부적으로 `RefreshActiveSource()` → `ResetInitialization()` → `TryEnsureInitialized()` 경로로 한 프레임 점프 없이 전환한다. SetActive 기반 폴백 로직은 없다.
  - `Assets/Hands/Editor/PlayHandPoseDriverSceneWiring.cs` — `Tools/Hands/Wire Play Hand Pose Drivers` 메뉴가 Play를 PhysicsHand로 정적 와이어링한다. Ghost 본은 와이어링하지 않는다.
  - `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — Controller Ghost / HandTracking Ghost 두 루트를 받아 `activeInHierarchy`로 활성 쪽을 자동 선택. 본 plan의 Play 폴백은 동일 패턴을 차용한다.
  - `Assets/Hands/Prefabs/Play/{Left,Right}PlayHand.prefab` — SkinnedMeshRenderer 머터리얼 guid `31321ba15b8f8eb4c954353edc038b1d`. (Play는 사용자에게 보이는 시각이고 본 sub-spec은 Play 머터리얼을 다시 다듬지 않으므로 그대로 유지.)
  - `Assets/Hands/Prefabs/Physics/{Left,Right}PhysicsHand.prefab` — 같은 머터리얼 guid 공유, SkinnedMeshRenderer `m_Enabled: 1`.
  - `Assets/Hands/Prefabs/Ghost/{Left,Right}{Controller,HandTracking}GhostHand.prefab` — 머터리얼 guid `613690cd962241049a0ec289a6ff835e`.
  - `Assets/Hands/Materials/`는 비어 있다(`.gitkeep`만).

## Approach

### 1. 디버그·검증용 머터리얼 자산 생성

`Assets/Hands/Materials/` 아래 두 머터리얼을 만든다.

- `GhostHand.mat` — URP/Lit, Surface = Transparent, Base Color ≈ RGBA `(0.0, 0.5, 1.0, 0.4)`. Linked Spec의 "투명 파랑".
- `PhysicsHandDebug.mat` — URP/Lit, Surface = Transparent, Base Color ≈ RGBA `(1.0, 0.0, 0.0, 0.4)`. Linked Spec의 "디버그용 fallback 투명 빨강".

생성은 Unity Editor에서 직접(또는 MCP `manage_material`) 수행하고, 생성된 `.mat`의 guid를 다음 단계에서 사용한다.

### 2. 프리팹 머터리얼·Renderer 갱신

Linked Spec의 시각 분리 요구 사항에 따라:

- `Assets/Hands/Prefabs/Ghost/LeftControllerGhostHand.prefab`
- `Assets/Hands/Prefabs/Ghost/LeftHandTrackingGhostHand.prefab`
- `Assets/Hands/Prefabs/Ghost/RightControllerGhostHand.prefab`
- `Assets/Hands/Prefabs/Ghost/RightHandTrackingGhostHand.prefab`

위 4개의 SkinnedMeshRenderer.`m_Materials`를 `GhostHand.mat`로 교체한다. Renderer는 활성 유지(`m_Enabled: 1`).

- `Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab`
- `Assets/Hands/Prefabs/Physics/RightPhysicsHand.prefab`

위 2개의 SkinnedMeshRenderer.`m_Materials`를 `PhysicsHandDebug.mat`로 교체하고 `m_Enabled: 0`으로 둔다. (정상 상태에서 보이지 않음 → 누군가 디버그로 켰을 때만 투명 빨강으로 즉시 식별.)

Play 프리팹은 손대지 않는다.

직렬화 자산을 직접 편집하기 전에 `unity-asset-edit` 스킬의 절차(범위 고정, prefab vs scene instance 차이 확인, 헤더 보존)를 따른다.

### 3. `PlayHandPoseDriver`에 SetActive 기반 폴백 추가

(`Assets/Hands/Scripts/PlayHandPoseDriver.cs`)

- 기존 `sourceRoot` / `sourceWristRoot`는 **Physics 소스**로 의미 보존.
- 직렬화 필드 추가: `fallbackSourceRoot`, `fallbackSourceWristRoot`. Linked Spec이 단일 "Ghost"를 말하지만 실제 Ghost는 controller/handtracking 두 종류가 존재하므로, 한 쌍의 fallback root로 받되 그 root 자체가 `PhysicsHandGhostFollower`처럼 두 자식 중 활성 쪽이 살아 있는 컨테이너 또는 직접 활성 Ghost root를 가리키게 한다 (Editor 와이어링 단계에서 결정).
- 활성 소스 결정 우선순위: `override (grip pose) > physics(when active) > fallback(ghost)`.
  - 새 private 메서드 `ResolveDesiredSource()`를 두고 매 `LateUpdate` / `OnBeforeRender`에서 호출.
  - 결정 로직:
    1. `m_OverrideSourceRoot != null` → override 사용.
    2. else if `sourceRoot != null && sourceRoot.gameObject.activeInHierarchy` → Physics 사용.
    3. else if `fallbackSourceRoot != null && fallbackSourceRoot.gameObject.activeInHierarchy` → Ghost 사용.
    4. else → null (현재 본 유지, 즉 미동작).
- 결정된 root가 `m_ActiveSourceRoot`와 다르면 기존 `RefreshActiveSource()`와 동일하게 `ResetInitialization()` + `TryEnsureInitialized()` 경로를 타게 한다. 한 프레임 점프 방지 패턴은 이미 검증되어 있으므로 재사용.
- `RefreshActiveSource()`는 grip pose override만 다루는 기존 시그니처를 유지하고, 본 SetActive 기반 전환은 `SyncPose` 진입부에서 자동으로 일어나도록 매 프레임 비교한다(`m_ActiveSourceRoot != desiredRoot` 시 ResetInitialization).

### 4. Editor 와이어링 갱신

(`Assets/Hands/Editor/PlayHandPoseDriverSceneWiring.cs`)

- 기존 메뉴 `Tools/Hands/Wire Play Hand Pose Drivers`가 Physics 소스에 더해 fallback Ghost 소스도 함께 와이어링하도록 확장한다.
- Ghost 소스 경로는 `PhysicsHandGhostFollower`가 이미 사용하는 패턴을 그대로 차용:
  - 씬 계층 `VR Player/Camera Offset/Hands/{Left|Right}/` 아래의 `{HandTracking|Controller}GhostHand` 둘 중 한 쪽을 fallback으로 와이어링.
  - 우선 `HandTrackingGhostHand`를 fallback으로 와이어링하고, 양쪽 모두를 자연스럽게 따르려면 `PhysicsHandGhostFollower`처럼 두 ghost를 함께 받도록 확장하는 것은 후속 plan으로 미룬다(Out of Scope 참조).
- 와이어링 후 `EditorSceneManager.MarkSceneDirty` + `EditorSceneManager.SaveOpenScenes`로 SampleScene의 PrefabInstance override를 저장한다.

### 5. Play 모드 검증

1. SampleScene을 연 뒤 `Tools/Hands/Wire Play Hand Pose Drivers` 메뉴를 재실행.
2. Unity MCP `read_console`로 컴파일·런타임 에러가 없는지 확인.
3. Play 모드 진입 후 다음 시나리오를 직접 확인:
   - **시나리오 A** — Physics 활성: Ghost(투명 파랑)가 입력 raw를 따라가고, Physics는 보이지 않으며, Play의 본이 Physics와 일치(같은 위치).
   - **시나리오 B** — Hierarchy에서 `LeftPhysicsHand` 또는 `RightPhysicsHand`를 SetActive(false)로 토글: Play 본이 활성 Ghost의 본과 일치하도록 다음 프레임에 폴백.
   - **시나리오 C** — Physics 핸드의 SkinnedMeshRenderer를 임의로 enabled=true로 켜기: 투명 빨강으로 즉시 식별됨.
   - **시나리오 D** — grip pose override(`PushSourceOverride`) 호출 동안에는 Physics 활성/비활성과 무관하게 override 소스를 따라간다(기존 grip pose 동작 회귀 없음).

## Deliverables

- 신규: `Assets/Hands/Materials/GhostHand.mat`
- 신규: `Assets/Hands/Materials/PhysicsHandDebug.mat`
- 수정: `Assets/Hands/Prefabs/Ghost/LeftControllerGhostHand.prefab` (머터리얼 교체)
- 수정: `Assets/Hands/Prefabs/Ghost/LeftHandTrackingGhostHand.prefab` (머터리얼 교체)
- 수정: `Assets/Hands/Prefabs/Ghost/RightControllerGhostHand.prefab` (머터리얼 교체)
- 수정: `Assets/Hands/Prefabs/Ghost/RightHandTrackingGhostHand.prefab` (머터리얼 교체)
- 수정: `Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab` (머터리얼 교체 + SkinnedMeshRenderer `m_Enabled: 0`)
- 수정: `Assets/Hands/Prefabs/Physics/RightPhysicsHand.prefab` (머터리얼 교체 + SkinnedMeshRenderer `m_Enabled: 0`)
- 수정: `Assets/Hands/Scripts/PlayHandPoseDriver.cs` (fallback 소스 + SetActive 기반 폴백 로직)
- 수정: `Assets/Hands/Editor/PlayHandPoseDriverSceneWiring.cs` (Ghost fallback 본까지 함께 와이어링)
- 수정: `Assets/Scenes/SampleScene.unity` (와이어링 메뉴 재실행으로 갱신되는 PrefabInstance override)

## Acceptance Criteria

- [ ] Play 모드에서 Physics 핸드가 활성일 때 좌·우 Play 핸드의 손목/손가락 본 월드 transform이 같은 쪽 Physics 핸드의 대응 본과 일치한다.
- [ ] Play 모드에서 Physics 핸드 GameObject를 `SetActive(false)`로 토글하면 다음 프레임부터 같은 쪽 Play 핸드의 본 월드 transform이 활성 Ghost(HandTracking 또는 Controller) 본과 일치한다.
- [ ] Ghost 핸드 SkinnedMeshRenderer가 활성 상태에서 항상 보이고, 머터리얼은 `Assets/Hands/Materials/GhostHand.mat`(투명 파랑)이다.
- [ ] Physics 핸드 SkinnedMeshRenderer는 기본 `m_Enabled: 0`이며, 임의로 활성화하면 `Assets/Hands/Materials/PhysicsHandDebug.mat`(투명 빨강)으로 식별된다.
- [ ] grip pose `PushSourceOverride`가 적용된 동안에는 Physics 핸드 활성/비활성 여부와 무관하게 Play가 override 소스를 따라간다 (기존 grip pose 동작 회귀 없음).
- [ ] `Tools/Hands/Wire Play Hand Pose Drivers` 메뉴가 Editor 콘솔 에러/경고 없이 성공하고, 좌·우 `PlayHandPoseDriver`의 `sourceRoot` / `sourceWristRoot` / `fallbackSourceRoot` / `fallbackSourceWristRoot`가 모두 채워져 SampleScene에 저장된다.
- [ ] Unity 콘솔(`read_console`)에 본 변경으로 인한 신규 컴파일 에러·경고가 없다.

## Out of Scope

- Physics 핸드를 non-kinematic으로 전환하는 본격 비통과 물리 — [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)에서 다룬다.
- Physics 핸드를 언제 `SetActive(false)`로 토글할지에 대한 트리거 정책(locomotion 이벤트, 속도 임계 등). 본 plan은 "비활성화된 상태에서 Play가 Ghost로 폴백한다"는 동작만 보장한다.
- 손 메쉬 형태·머터리얼의 출시용 마감(투명도/색 외 디테일).
- `PhysicsHandGhostFollower`와 동일한 "두 Ghost(controller + handtracking) 동시 인지" 패턴까지 `PlayHandPoseDriver`로 일반화하는 작업. 본 plan에서는 fallback 한 쌍(우선 HandTracking Ghost)만 와이어링하며, 추가 일반화는 필요 시 후속 plan으로 분리.
- Play 프리팹 머터리얼 변경.

## Notes

- `PhysicsHandGhostFollower`와 `PlayHandPoseDriver`가 모두 `OnBeforeRender` + `LateUpdate` 양쪽에서 Sync를 도는 점, `__Collider_*` 본을 jointMap에서 제외하는 규약을 그대로 따른다(본 plan의 신규 fallback 경로도 같은 jointMap 빌드를 거치게 됨).
- `m_ActiveSourceRoot != desiredRoot` 전환 시 jointMap을 재빌드하는 비용이 있지만, 전환 빈도(SetActive 토글)는 낮으므로 측정·최적화는 후속 과제.
- Linked Spec의 Behavior 3개 케이스가 본 plan의 Acceptance Criteria 1~4번에 1:1 대응한다.
