# DrumKitAnchor Forward Rotation 보정

**Linked Spec:** [`01-anchor-auto-attach-detach.md`](../specs/01-anchor-auto-attach-detach.md)
**Status:** `Done`

## Goal

`SampleScene.unity` 의 `DrumKitAnchor` GameObject 의 Y rotation 을 180° 회전 (180→0) 시켜, `TeleportationAnchor.m_MatchOrientation=2` (TargetUpAndForward) 의 forward 가 드럼킷 정면을 향하도록 보정한다. 도착 시 사용자가 드럼 등 뒤가 아니라 드럼 정면을 보고 있게 된다.

## Context

### 선행 plan에서 박제된 결손

선행 plan [`2026-05-02-sanyoentertain-anchor-auto-attach-detach.md`](../../_archive/drum-stick/plans/2026-05-02-sanyoentertain-anchor-auto-attach-detach.md) 의 Handoff "DrumKitAnchor forward 방향 결손" 에서 다음이 박제됐다.

> `DrumKitAnchor` GameObject 의 Y rotation 이 의도와 반대(현재 forward = 드럼킷 등 뒤). `TeleportationAnchor.m_MatchOrientation=2` (TargetUpAndForward) 이므로 도착 시 사용자 forward 가 drum 의 등 뒤로 향한다. 단순 1-line transform 변경 (SampleScene `DrumKitAnchor` Y rotation +180°). 본 plan 자체의 lifecycle 동작에는 영향 없음.

선행 plan 의 manual-hard 4건은 모두 pass (=lifecycle 이 동작) 이지만 anchor 도착 시 사용자 forward 가 정반대다. 본 plan 은 그 단일 결손을 닫는다 (검증 실패 분기가 아니므로 `Caused By` 헤더는 부착하지 않는다).

### 좌표 박제 (현재 SampleScene)

- `DrumKit` (root, scale 1.5): position `(-0.857, 0, -1.625)`, rotation `(0, 0, 0)`. forward = `+Z`. 즉 드럼 정면이 `+Z` 방향.
- `DrumKitAnchor` (root): position `(-0.857, 0, -2.551)`, rotation `(0, 180, 0)`. anchor 의 forward = `-Z`. 사용자가 anchor 에 도착하면 forward 가 `-Z` (= 드럼 등 뒤) 로 향한다.
- 사용자가 anchor 에 도착해 drum 정면을 보려면 anchor 의 forward 가 drum 위치 방향 (= `-2.551 → -1.625` = `+Z`) 이어야 한다 → anchor 의 Y rotation 은 `0` 이어야 옳다.
- 따라서 본 plan 의 변경 = `DrumKitAnchor.transform.localEulerAngles.y` 를 `180` → `0`. (= 의도된 +180° 회전 적용; 180+180=360≡0.)

### 박제된 ARD 결정

- **ARD 04 — `manage_prefabs` / `manage_components` MCP 1차.** scene 직렬화 transform 변경은 `manage_gameobject` (또는 `manage_components action=set_property component_type=Transform`) MCP 호출로 처리. 텍스트 직접 Edit 은 사용자 승인 선행 시에만 — 본 plan 은 MCP 경로로 완결.
- **선행 plan ARD 01/02/03** — 본 plan 은 anchor lifecycle / stick↔손 결합 / Instantiate-Destroy 모델에 영향 주지 않는다. transform 한 축 변경만.

### 호출 외부 API side effect 박제

- `BaseTeleportationInteractable.MatchOrientation` enum 정의 (출처: `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/BaseTeleportationInteractable.cs (2026-05-03)`):
  - `None=0`, `WorldSpaceUp=1`, `TargetUp=2`, `TargetUpAndForward=3` 또는 동등한 정의 (선행 plan 박제 값 `m_MatchOrientation=2` 와 자체 코멘트 "TargetUpAndForward" 가 일치하는 정의 — 즉 anchor 자신의 transform.forward 와 transform.up 양쪽으로 사용자를 정렬). 본 plan 은 이 enum 의 의도 값을 변경하지 않고, anchor transform 의 Y rotation 만 변경한다.
  - `TargetUpAndForward` 의 동작: anchor 의 `transform.forward` (= world space 의 anchor forward 방향) 가 사용자 시선 forward 가 된다. `transform.up` 도 함께 정렬. 즉 anchor 의 Y rotation 이 final 사용자 yaw 결정.
- `Transform.localEulerAngles` set 의 side effect: 자식 (없음 — `DrumKitAnchor` childCount=0) 의 world pose 도 함께 회전. `BoxCollider` 의 size/center 는 local 그대로 유지되지만 world rotation 이 따라 변한다 — anchor 의 BoxCollider 가 텔레포트 라인 hit 영역이므로 회전 후에도 사용자가 anchor 영역을 가리킬 수 있는지가 변할 수 있다. 그러나 BoxCollider 는 회전축 중심 box 이므로 Y rotation 180°→0° 는 평면 box 면적 분포가 동일 (대칭). 즉 텔레포트 가능 영역이 깨지지 않는다.
- `TeleportationAnchor.selectExited` (선행 plan 의 attach trigger source) 는 anchor 의 transform 변경에 영향받지 않는다 — 사용자가 anchor 영역을 가리키고 release 하는 동작 자체는 anchor 의 회전과 독립. 즉 본 plan 의 변경이 선행 plan 의 lifecycle (attach/detach) 을 깨지 않는다.

## Verified Structural Assumptions

- `DrumKitAnchor` 는 SampleScene root GameObject (instanceID 51948 at 2026-05-03 query, path `DrumKitAnchor`, childCount 0). 컴포넌트 = `Transform` + `BoxCollider` + `TeleportationAnchor` + `DrumKitStickAnchor`. 현재 transform = position `(-0.857, 0, -2.551)`, rotation `(0, 180, 0)`, scale `(1, 1, 1)`. — `unity-scene-reader 보고 (2026-05-03, manage_scene action=get_hierarchy include_transform=true cursor=5)`
- `DrumKit` (root) 의 transform = position `(-0.8574094, ~0, -1.62513065)`, rotation `(0, 0, 0)`, scale `(1.5, 1.5, 1.5)`. forward = `+Z`. 즉 drum 정면이 `+Z` 방향이고, anchor 가 drum 보다 z 가 작은 쪽 (`-2.551 < -1.625`) 에 있으므로 anchor 의 forward 가 `+Z` 일 때 사용자가 drum 정면을 본다. — `unity-scene-reader 보고 (2026-05-03, 위 동일 호출 cursor=5 결과)`
- `BaseTeleportationInteractable.MatchOrientation` enum 박제 (선행 plan 에서 m_MatchOrientation=2 = TargetUpAndForward 로 박제). 본 plan 은 enum 인덱스 / 의도 값을 변경하지 않고 anchor 의 world transform 만 변경한다. — `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/BaseTeleportationInteractable.cs (2026-05-03)` (선행 plan VSA 와 동일 출처).
- `BaseTeleportationInteractable.TeleportTrigger` enum: `OnSelectExited=0`, `OnSelectEntered=1`, `OnActivated=2`, `OnDeactivated=3`. 선행 plan 의 의도 값 = `0`. 본 plan 은 이 직렬화 값을 변경하지 않는다 (가드 AC 1건). — `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/BaseTeleportationInteractable.cs (2026-05-03)` (선행 plan VSA 와 동일).
- `DrumKitAnchor` 의 BoxCollider 는 anchor 영역 hit. Y 180° 회전 시 BoxCollider 의 world AABB 가 변하더라도 OBB 중심 (= anchor position) 은 불변, 회전 대칭이므로 사용자가 가리킬 수 있는 영역이 사실상 동일. 본 plan 은 BoxCollider size/center 를 변경하지 않는다. — `unity-scene-reader 보고 (2026-05-03, 위 동일)`.

## Approach

### 단계 1 — DrumKitAnchor Y rotation 180° 회전

`SampleScene.unity` 의 `DrumKitAnchor` (path `DrumKitAnchor`, root) 의 Transform 을 다음과 같이 변경:

- `localEulerAngles.y` : `180` → `0`. (= 의도된 +180° 적용; 회전축 modular 결과)
- 다른 transform 축 (position x/y/z, rotation x/z, scale) 은 변경 금지.

MCP 경로 (decision 04 의 1차):

```
manage_components
  action=set_property
  target=DrumKitAnchor          # path 또는 instanceID; instanceID 는 재load 시 변할 수 있어 path 권장
  component_type=Transform
  property=localEulerAngles
  value=[0, 0, 0]
```

또는 `manage_gameobject` 의 transform set API 를 사용할 수 있다. plan-implementer 가 MCP 의 정확한 propertyPath 를 시점에 확인 (`manage_components` 의 Transform 노출이 `localEulerAngles` 인지 `m_LocalRotation` 직렬화 quaternion 인지에 따라 호출 형식이 다름).

MCP 의 Transform property set 이 `m_LocalRotation` 만 받는 경우 fallback: `(0, 180, 0)` Euler 의 quaternion = `(0, 1, 0, 0)` (= 180° around Y). 변경 후 quaternion = `(0, 0, 0, 1)` (= identity). plan-implementer 는 이 두 quaternion 중 어느 쪽이 현재 `m_LocalRotation` 직렬화 값인지 확인 후 식 적용.

### 단계 2 — MCP 처리 못 하면 fallback

ARD 04 가 박제한 fallback 진입 조건. `manage_*` 가 Transform property set 을 거부하면 (현재까지 전례는 없으나 가드):

1. plan-implementer 는 본 plan 의 단계 1 단일 직렬화 변경이라는 사실을 사용자에게 보고.
2. 사용자 승인 후, `unity-asset-edit` skill 의 직접 텍스트 Edit 경로로 SampleScene.unity 의 `DrumKitAnchor` block 의 `m_LocalRotation` 을 `{x: 0, y: 0, z: 0, w: 1}` 로 set. 다른 필드 변경 금지. 단일 propertyPath 변경 예외 (decision 04 의 fallback 적용 범위 안).

### 단계 3 — 회귀 방지

- `DrumKitStickAnchor` 컴포넌트 (선행 plan 산출물) 와 그 6 SerializeField 셋업은 변경 금지.
- `TeleportationAnchor.m_TeleportTrigger=0`, `m_MatchOrientation=2` 직렬화 값 보존.
- BoxCollider size/center 보존.
- 선행 plan 의 `[manual-hard]` 검증 항목 (양손 동시 attach / grip·trigger 무력화 / detach / 재도착 attach) 이 본 plan 적용 후에도 동일하게 동작.

## Deliverables

- `Assets/Scenes/SampleScene.unity` — `DrumKitAnchor` root GameObject 의 Transform `localEulerAngles.y` 를 `180` → `0` (= rotation `(0, 0, 0)`).

## Acceptance Criteria

- [ ] `[auto-hard]` `DrumKitAnchor` (path) 의 Transform `localEulerAngles` 가 `(0, 0, 0)` — `manage_scene action=get_hierarchy include_transform=true` 결과의 `DrumKitAnchor` item 의 `transform.rotation` 이 `[0, 0, 0]` (혹은 그에 해당하는 직렬화 quaternion identity).
- [ ] `[auto-hard]` `DrumKitAnchor` 의 Transform 다른 축 (position `(-0.857, 0, -2.551)`, scale `(1, 1, 1)`) 보존 — 위 호출 결과의 `transform.position` / `transform.scale` 가 변경 전 값과 동일. (회귀 가드: 본 plan 이 다른 transform 축을 건드리지 않았음.)
- [ ] `[auto-hard]` `DrumKitAnchor` 의 컴포넌트 구성이 변경 전과 동일 — `componentTypes` = `[Transform, BoxCollider, TeleportationAnchor, DrumKitStickAnchor]` 단일 매치. 본 plan 이 컴포넌트 추가/제거를 하지 않음을 가드.
- [ ] `[auto-hard]` `TeleportationAnchor.m_TeleportTrigger == 0` 보존 (= OnSelectExited per `BaseTeleportationInteractable.TeleportTrigger`). enum 의도 값 회귀 가드.
- [ ] `[auto-hard]` `DrumKitStickAnchor` 컴포넌트의 6 SerializeField (좌·우 stick prefab, 좌·우 ghost wrist source, 좌·우 PlayHandPoseDriver) 모두 non-null 보존. 선행 plan 셋업 회귀 가드.
- [ ] `[manual-hard]` Play 모드 진입 → drum anchor 외부에서 시작 → drum anchor 로 텔레포트 → 도착 직후 사용자가 **드럼킷 정면** (= drum 의 `+Z` 방향, drum body 가 사용자 시야 정면에 보이는 상태) 을 보고 있다. 이전: 사용자가 drum 등 뒤를 보고 있어 시야에 drum 이 안 보이거나 뒤쪽에 있었음.
- [ ] `[manual-hard]` 위 상태에서 선행 plan 의 lifecycle (양손에 stick 동시 attach / grip·trigger 입력으로 stick 떨어지지 않음 / drum anchor 외부로 텔레포트 시 detach / 재도착 시 새 attach) 이 모두 보존된다. 선행 plan manual-hard 4건 회귀 검증.

## Out of Scope

- `AnchoredStickGhostFollower` 의 정렬 식 보정 → 동시 작성 plan [`2026-05-03-sanyoentertain-stick-gripposehand-alignment.md`](./2026-05-03-sanyoentertain-stick-gripposehand-alignment.md).
- `PianoAnchor` 등 다른 anchor 의 forward 방향 — 본 plan 은 drum 만.
- BoxCollider size/center 조정 — 회전 후 텔레포트 라인 hit 영역이 어색하다고 판단되면 별도 후속 plan.
- DrumKit 자체의 위치/회전 변경 — drum 위치는 fixed, anchor 위치만 보정.

## Notes

- 본 plan 은 단일 transform 축 변경. atomic commit 단위로 깔끔히 분리되며, 선행 plan 의 lifecycle 코드와 독립적이라 회귀 위험이 낮다.
- `DrumKitAnchor` 의 instanceID 는 query 시점마다 변할 수 있어 (Unity scene 재load 시 새 ID 부여) AC 검증은 `path` 기반 lookup 권장.
- MCP 의 Transform set 호출 형식이 `localEulerAngles` 직접 노출이 아닐 경우 단계 2 fallback 으로 quaternion identity 직렬화 set. plan-implementer 가 MCP 첫 시도 시 정확한 propertyPath 를 print 해 사용자에게 한 번 보여주는 게 안전.
- 선행 plan `## Handoff` 의 "DrumKitAnchor forward 결손" 항목은 본 plan 의 manual-hard AC 6번이 pass 되면 닫힌 것으로 본다.
