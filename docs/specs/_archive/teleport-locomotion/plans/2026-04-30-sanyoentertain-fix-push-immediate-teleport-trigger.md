# Fix Push-Immediate Teleport Trigger

**Linked Spec:** [`01-base-teleport.md`](../specs/01-base-teleport.md)
**Caused By:** [`2026-04-30-sanyoentertain-base-teleport-on-controller-hand-root.md`](./2026-04-30-sanyoentertain-base-teleport-on-controller-hand-root.md)
**Status:** `Done`

## Goal

SampleScene Plane의 `TeleportationArea.m_TeleportTrigger` 직렬화 값을 `1`(OnSelectEntered)에서 `0`(OnSelectExited)으로 보정해, thumbstick을 release하는 순간에만 텔레포트가 발동되도록 한다. 현재는 select 시작(=push) 시점에 `BaseTeleportationInteractable.SendTeleportRequest`가 호출되어 즉시 텔레포트가 발동된다.

## Context

> **선행 plan 검증 실패에서 파생됨.** 선행: `2026-04-30-sanyoentertain-base-teleport-on-controller-hand-root.md`.
> 실패한 Acceptance Criteria:
> - `[manual-hard]` 왼손 thumbstick만으로 즉시 텔레포트 라인 표시 — 사용자 보고: 라인 자체는 표시되지만 push→라인 즉시 표시 흐름이 아니고 push 시 라인이 텔레포트 직후에 생기는 순서.
> - `[manual-hard]` thumbstick 놓는 순간 카메라 리그가 그 지점으로 이동, 라인 사라짐 — 사용자 보고: push 즉시 텔레포트가 발동되어 release 시점이 아닌 push 시점에 이동. 의도와 정반대.
> - `[manual-hard]` 라인 끝이 Plane 외(허공/Table/악기) 가리킬 때 thumbstick 놓으면 이동 X, 라인만 사라짐 — AC 10이 깨졌으므로 release 기반 차단 로직 자체가 발동하지 않음.
>
> 본 plan은 위 항목을 다시 통과 가능하게 만드는 부속 작업을 다룬다.

선행 plan은 LCH.prefab(`Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab`)에 Teleport Interactor 인스턴스를 박고, root에 ControllerInputActionManager를 부착하고, m_SelectInput의 두 슬롯을 모두 `XRI Left Locomotion/Teleport Mode`(Value/Vector2) 액션으로 override했다. VR Player.prefab root에 TeleportationProvider를 부착하고 DynamicMoveProvider를 제거했다. SampleScene Plane에는 TeleportationArea를 부착했다. 이 선행 변경은 working tree에 그대로 남아 있다.

### Root cause (확정)

XRI 패키지 소스(`com.unity.xr.interaction.toolkit@5f736ad4ccd8`)의 `BaseTeleportationInteractable.cs`에서 `TeleportTrigger` enum 정의는 다음과 같다.

```csharp
public enum TeleportTrigger
{
    OnSelectExited,    // value = 0  (default — release 시 발동)
    OnSelectEntered,   // value = 1  (push 시 즉시 발동)
    OnActivated,       // value = 2
    OnDeactivated,     // value = 3
}
```

같은 파일의 `OnSelectEntered` 처리:

```csharp
if (m_TeleportTrigger == TeleportTrigger.OnSelectEntered)
    SendTeleportRequest(args.interactorObject);
```

SampleScene(`Assets/Scenes/SampleScene.unity`)의 Plane GameObject에 부착된 TeleportationArea의 직렬화 값은 `m_TeleportTrigger: 1` — 즉 `OnSelectEntered`로 박혀 있다. 선행 plan 적용 시 plan-orchestrator의 manage_components 호출이 enum value를 잘못 매핑(0과 1을 뒤바꾼 것으로 추정)한 결과로 보인다. 결과적으로 사용자가 thumbstick을 push해 select가 시작되는 순간 곧장 SendTeleportRequest가 호출되고, release 시점(=OnSelectExited)에는 처리 분기가 없어 다시 발동되지 않는다 — 사용자 보고("push 즉시 텔레포트 → 그 후 라인 → release에는 아무 일도 없음")와 정확히 일치한다.

### 1차 시도(m_SelectActionTrigger 0→1) 분석

본 plan의 1차 plan-implementer는 LCH.prefab의 Teleport Interactor 인스턴스 PrefabInstance modification에 `m_SelectActionTrigger: 1`(State) 한 줄을 추가했다. 이 변경은 `XRRayInteractor`의 select state 평가 모드를 StateChange(0)에서 State(1)로 바꾸는 것으로, push-hold-release 동작 안정성에는 도움이 될 수는 있으나 본 root cause(m_TeleportTrigger=1)와는 무관하다 — Plane이 OnSelectEntered면 select state 평가 모드가 무엇이든 select 시작 즉시 텔레포트가 발동된다. 사용자가 "변경 후에도 동작이 동일하다"고 보고한 것이 이 무관성을 입증.

본 plan은 1차 시도 변경(m_SelectActionTrigger=1)은 working tree에 그대로 두고 root cause 수정에 집중한다. 1차 변경이 정상 push-hold-release 동작에 부정적 영향을 주지 않는다는 가설이 검증 단계에서 깨지면 그때 별도 plan으로 원복한다.

### 입력 동작 분석 (참고)

XRI 입력 자산의 `Teleport Mode` 액션 정의는 type=Value(Vector2)이고 binding(`<XRController>{LeftHand}/{Primary2DAxis}`)에 `Sector(directions=1)` interaction이 적용되어 있다. Sector interaction은 thumbstick이 sector 안에 있는 동안 `phase=Performed`를 유지하는 button-like 동작을 만든다. `XRInputButtonReader.ReadIsPerformed()`는 `phase==Performed || (phase!=Disabled && action.WasPerformedThisFrame())`로 판정하므로 thumbstick 유지 동안 IsPerformed=true가 유지된다. 즉 입력 측 push-hold-release 흐름은 정상이다 — root cause는 입력이 아니라 텔레포트 발동 시점(TeleportTrigger)이다.

### 제약

- 본 plan은 선행 plan과 1차 시도(m_SelectActionTrigger=1) 변경을 보존한 채 SampleScene의 Plane TeleportationArea 한 필드만 보정한다.
- 새 C# 코드, 입력 액션 자산 변경, prefab 자식 추가 등은 일체 없다.
- 본 plan 적용 후 정상 동작이 확인되면, 1차 시도 변경(m_SelectActionTrigger=1)은 부수적으로 그대로 보존한다 (push-hold-release 안정성에 도움이 될 수 있고 negative effect 보고 없음).

## Verified Structural Assumptions

- `BaseTeleportationInteractable.TeleportTrigger` enum: `OnSelectExited=0` / `OnSelectEntered=1` / `OnActivated=2` / `OnDeactivated=3`. enum 첫 값(=0)이 OnSelectExited (release 시 발동) — `Read Library/PackageCache/com.unity.xr.interaction.toolkit@5f736ad4ccd8/Runtime/Locomotion/Teleportation/BaseTeleportationInteractable.cs (2026-04-30)`.
- SampleScene(`Assets/Scenes/SampleScene.unity`)의 Plane GameObject TeleportationArea 직렬화 값 `m_TeleportTrigger: 1` (=OnSelectEntered, push 시 즉시 발동) — `Grep "m_TeleportTrigger" Assets/Scenes/SampleScene.unity (2026-04-30)`.
- LCH.prefab의 Teleport Interactor 인스턴스 PrefabInstance modification에 1차 plan-implementer가 추가한 `m_SelectActionTrigger: 1`(State) 한 줄 존재. 이는 root cause와 무관한 부수 변경 — `Grep "m_SelectActionTrigger" Assets/Hands/Prefabs/Roots/LeftControllerHandRoot.prefab (2026-04-30)`.
- `XRI Left Locomotion/Teleport Mode` 액션은 type=Value(Vector2)이고 binding `<XRController>{LeftHand}/{Primary2DAxis}`에 `Sector(directions=1)` interaction 적용. `XRInputButtonReader.ReadIsPerformed()`가 thumbstick 유지 동안 true 유지 — `Read XRI Default Input Actions.inputactions Line 1294-1361 + XRInputButtonReader.cs Line 401-433 (2026-04-30)`.
- VR Player.prefab(`Assets/Characters/Prefabs/VR Player.prefab`) root의 TeleportationProvider/SnapTurnProvider/LocomotionMediator/XRBodyTransformer/InputActionManager/XRInputModalityManager 6종 부착 + DynamicMoveProvider 미부착, LCH.prefab의 Teleport Interactor 자식과 ControllerInputActionManager 셋업이 선행 plan 결과 그대로 유지 — `선행 plan plan-orchestrator evidence + 메인 세션 grep 재확인 (2026-04-30)`.

## Approach

### 1. SampleScene Plane.m_TeleportTrigger 보정

`Assets/Scenes/SampleScene.unity`의 Plane GameObject에 부착된 TeleportationArea 컴포넌트의 `m_TeleportTrigger` 필드를 `1`에서 `0`으로 변경한다.

- `manage_components` MCP 또는 `manage_gameobject` MCP로 Plane의 TeleportationArea를 찾아 `m_TeleportTrigger` 또는 인스펙터 노출 이름(`Teleport Trigger`)을 `OnSelectExited`(value 0)으로 설정.
- MCP enum 매핑이 다시 잘못될 위험을 줄이기 위해 적용 직후 `Grep "m_TeleportTrigger" Assets/Scenes/SampleScene.unity`로 직렬화 결과가 정확히 `0`인지 재확인.

### 2. 검증

각 Acceptance Criteria 수행. 자동: 직렬화 값(`m_TeleportTrigger: 0`) + 컴파일 + 인스턴스화 sanity. 수동: 선행 plan 의도(push→라인→조정→release→발동) Play 모드 검증 + 선행 plan의 fail AC 3건 재검증.

## Deliverables

- `Assets/Scenes/SampleScene.unity` — Plane GameObject TeleportationArea의 `m_TeleportTrigger` 필드 `1` → `0`.

(다른 자산 변경 없음. LCH.prefab의 m_SelectActionTrigger=1 부수 변경은 그대로 보존 — root cause와 무관해 negative effect 없음.)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`의 Plane GameObject TeleportationArea 직렬화에서 `m_TeleportTrigger: 0`이 나온다 (`Grep "m_TeleportTrigger" Assets/Scenes/SampleScene.unity`로 단일 매치 + value `0`).
- [ ] `[auto-hard]` `Assets/Scenes/SampleScene.unity`의 Plane GameObject에 TeleportationArea 컴포넌트 부착 상태가 그대로 유지된다 (선행 plan 결과 보존).
- [ ] `[auto-hard]` LCH.prefab/VR Player.prefab의 선행 plan 결과(TeleportationProvider 부착, DynamicMoveProvider 미부착, Teleport Interactor 인스턴스, ControllerInputActionManager 세 슬롯 비어있지 않음, m_SelectInput 두 슬롯 Teleport Mode 가리킴)가 모두 보존된다.
- [ ] `[auto-hard]` Unity Editor가 컴파일 오류 없이 Domain Reload를 마친다 — `read_console` error/exception 0건.
- [ ] `[auto-hard]` 본 plan 적용 후 VR Player.prefab을 SampleScene에 인스턴스화한 상태에서 핵심 자식 노드 (`Camera Offset/Hands/Left/LeftControllerHandRoot`, `Camera Offset/Hands/Left/LeftControllerHandRoot/Teleport Interactor`, `Camera Offset/Hands/Left/LeftPhysicsHand`, `Main Camera`)가 모두 등장하고 인스턴스화 콘솔 에러 0건.
- [ ] `[manual-hard]` Play 모드 + VR(또는 XR Device Simulator)에서 왼손 thumbstick을 앞으로 밀고 **유지하는 동안** 라인이 계속 표시되고 컨트롤러를 움직여 라인 끝 위치를 조정할 수 있다 (push 시점에 텔레포트가 즉시 발동되지 않는다).
- [ ] `[manual-hard]` Plane 위 한 지점을 가리키는 동안 thumbstick을 release하는 순간 카메라 리그가 그 지점으로 이동하고 라인이 사라진다.
- [ ] `[manual-hard]` 선행 plan `2026-04-30-sanyoentertain-base-teleport-on-controller-hand-root.md`의 실패 AC ("Play 모드 + VR(또는 XR Device Simulator)에서 왼손 thumbstick만(트리거·") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-04-30-sanyoentertain-base-teleport-on-controller-hand-root.md`의 실패 AC ("라인이 Plane 위 한 지점을 가리키는 동안 thumbstick을 놓는 순간(다른 어떤 버튼도 ") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-04-30-sanyoentertain-base-teleport-on-controller-hand-root.md`의 실패 AC ("라인 끝이 Plane을 벗어난 허공/Table/악기를 가리킬 때 thumbstick을 놓으면 이동이") 가 이 plan 적용 후 재검증에서 통과한다.

## Out of Scope

- 페이드 인/아웃 같은 시각 전환 효과 도입.
- 노 텔레포트 존 (`02-no-teleport-zones.md`).
- 악기 텔레포트 anchor (`03-instrument-anchors.md`).
- Snap Turn 동작 변경.
- 운영 씬 외 다른 씬 동기화.
- LCH.prefab의 m_SelectActionTrigger=1 부수 변경 원복 — 본 plan에서는 보존. 필요해지면 별도 plan.

## Notes

- **2026-04-30 진단 정정.** 본 plan의 1차 작성안은 옵션 C/B/A(m_SelectInput 셋업 변경, button-form composite 신설 등)를 root cause로 가정했고, 1차 plan-implementer는 그 가이드를 따르지 않고 m_SelectActionTrigger 0→1 변경을 적용했다. 그러나 사용자 검증에서 동작이 1차 시도 전과 동일했다 — 두 진단 모두 root cause를 잘못 짚은 것이다.
- **확정된 root cause**: SampleScene Plane TeleportationArea의 `m_TeleportTrigger` 직렬화 값이 `1`(OnSelectEntered)이라 select 시작 즉시 텔레포트가 발동되고 있었다. enum 인덱스(0=OnSelectExited, 1=OnSelectEntered)를 plan-orchestrator의 manage_components 호출이 잘못 매핑한 것이 직접적 원인. `0`(OnSelectExited)으로 1줄 변경하면 해결.
- **부수 변경 보존 결정**: 1차 시도의 m_SelectActionTrigger=1(State)는 root cause와 무관하지만 push-hold-release select state 평가에 도움이 될 수 있어 그대로 둔다. negative effect가 검증 단계에서 발견되면 별도 plan으로 원복.
- 본 plan 본문은 1차 작성안을 사용자 동의로 in-place 대체한 결과다. plan 파일 경로/제목은 유지.

## Handoff

- SampleScene(`Assets/Scenes/SampleScene.unity`)의 Plane GameObject TeleportationArea의 직렬화 필드 `m_TeleportTrigger`를 `1`(OnSelectEntered)에서 `0`(OnSelectExited)으로 한 줄 보정. enum value 매핑은 `BaseTeleportationInteractable.TeleportTrigger`(`OnSelectExited=0` / `OnSelectEntered=1`).
- 변경 방법: 단일 propertyPath 스칼라 변경이라 `manage_components` MCP의 enum 매핑 함정을 회피하기 위해 직접 텍스트 Edit로 적용.
- LCH.prefab의 1차 시도 변경(`m_SelectActionTrigger: 1`, State 모드)은 working tree에 그대로 보존. push-hold-release select state 평가 안정성에 도움이 될 수 있고 negative effect 보고 없음 — 후속 plan에서 root cause로 의심되면 별도 plan으로 원복.
- 후속 plan은 본 plan의 변경을 그대로 신뢰해도 된다 — Plane 위로는 release 시점에만 텔레포트가 발동되며, push-hold-release 흐름이 spec(`01-base-teleport.md`) Behavior와 일치.
- 노 텔레포트 존(`02-no-teleport-zones.md`)이 추가되면 그 영역은 추가 TeleportationArea 미부착 또는 별도 차단 컴포넌트로 처리. 본 plan은 Plane 단일 surface의 trigger 시점만 다룸.
