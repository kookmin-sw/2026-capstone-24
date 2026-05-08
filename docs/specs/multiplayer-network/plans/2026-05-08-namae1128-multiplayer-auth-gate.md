# 인-게임 멀티플레이어 진입 게이트

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Status:** `Ready`

## Goal

Quest 빌드(또는 에디터 Play)에서 사용자가 명시적으로 멀티플레이어 모드에 진입할 때까지는 인증·서버 호출이 발생하지 않도록 한다. 진입은 게임 안의 작은 UI 버튼 한 번으로 이뤄지며, 누르면 인증 흐름이 시작되고 후속 멀티플레이어 컴포넌트들이 활성화된다.

## Context

- 현재 [`SampleScene.unity`](../../../../Assets/Scenes/SampleScene.unity)에는 `MultiplayerAuthBootstrap` GameObject 1개가 박혀 있고 그 위에 [`AuthBootstrap`](../../../../Assets/Multiplayer/Scripts/Auth/AuthBootstrap.cs)·[`AuthSmokeProbe`](../../../../Assets/Multiplayer/Scripts/Auth/AuthSmokeProbe.cs) 두 컴포넌트가 올라가 있다.
- `AuthBootstrap.authenticateOnStart`는 기본 `true`라 씬 Play 즉시 백엔드 인증을 시도한다. 백엔드가 떠 있지 않으면 콘솔에 `Authentication failed` 빨간 로그가 한 줄 찍히지만 게임 자체는 멈추지 않는다.
- `AuthSmokeProbe`는 OnGUI로 좌하단에 작은 패널을 항상 표시한다 — 인증·룸 상태 표시 + 수동 버튼.
- 팀원들은 Editor에서 SampleScene을 열어 Quest USB 연결 + 빌드 → 실기기에서 짧은 iteration으로 자기 기능을 테스트한다. 매번 인증을 시도하면 콘솔/오버레이 노이즈가 거슬리고, 백엔드 안 띄우면 의미도 없다.
- 즉 일상 작업에서는 멀티플레이어 컴포넌트가 **꺼져 있어야** 하며, 본 작업자가 멀티플레이어 테스트를 원할 때만 켜져야 한다. Quest 실기기 빌드 안에서도 그 토글이 가능해야 한다.

## Decisions

- 트리거는 **씬 안의 World-Space UI 버튼**으로 한다 — VR 환경에서 광선/포인터로 누를 수 있게. 별도 데스크톱 메뉴 시스템 불필요.
- 한 번 누르면 `MultiplayerAuthBootstrap` GameObject가 `SetActive(true)`로 켜지고 `AuthBootstrap.EnsureAuthenticatedAsync()`가 호출된다. 인증 결과는 같은 UI 패널에 표시한다(성공/실패).
- `AuthBootstrap.authenticateOnStart`는 false로 둔다 — 게이트가 명시적으로 호출하므로 자동 시도 불필요.
- 게이트 자체는 항상 활성 상태로 씬에 남아 있고, 실수로 두 번 누르더라도 `AuthBootstrap.EnsureAuthenticatedAsync`의 idempotent 동작에 위임한다(이미 진행 중이면 같은 Task 반환).
- **재진입은 본 plan의 범위 밖**이다 — 한 번 켜면 그 세션 동안은 그대로 둔다. 다시 끄려면 씬 재시작 또는 플레이 종료.

## Approach

1. **신규 컴포넌트 `MultiplayerAuthGate`** — `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs` 작성:
   - `[SerializeField] GameObject multiplayerAuthBootstrap` — 씬에서 바로 참조 연결.
   - `[SerializeField] AuthBootstrap authBootstrap` — 같은 GameObject의 컴포넌트 참조.
   - `[SerializeField] UnityEngine.UI.Button activateButton` 또는 World-Space `Button` 컴포넌트 참조.
   - `[SerializeField] TMPro.TMP_Text statusLabel` — 상태 텍스트.
   - 버튼 클릭 → `multiplayerAuthBootstrap.SetActive(true)` → `authBootstrap.EnsureAuthenticatedAsync()` await → 성공 시 `statusLabel`에 "Multiplayer 활성화 — <닉네임>" / 실패 시 "실패: <메시지>" 표시.
   - 이미 활성화된 상태면 중복 호출 무시.
2. **씬 변경 — `Assets/Scenes/SampleScene.unity`**:
   - `MultiplayerAuthBootstrap` GameObject의 `m_IsActive` 0(비활성)으로 변경.
   - `AuthBootstrap`의 `authenticateOnStart` false로 변경.
   - 새 GameObject `MultiplayerAuthGate`(World-Space Canvas + Button + Text + 본 컴포넌트) 추가. 위치는 SampleScene 안의 기존 UI 영역 또는 사용자가 시야에서 찾기 쉬운 곳(예: Table 위 또는 별도 패널).
3. **검증** — Editor Play 모드에서 (a) 버튼 누르기 전 콘솔/오버레이 노이즈 0건 확인, (b) 버튼 누른 후 인증 진행 + 상태 라벨 갱신 확인, (c) Quest USB 빌드 후 실기기에서 같은 시나리오.

## Deliverables

- `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs`
- `Assets/Scenes/SampleScene.unity` — `MultiplayerAuthBootstrap` 비활성, `authenticateOnStart` false, World-Space `MultiplayerAuthGate` UI 추가
- (선택) `Assets/Multiplayer/Prefabs/MultiplayerAuthGate.prefab` — 다른 씬에서도 재사용 가능하도록 프리팹화

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs`가 Editor 컴파일 에러 없이 로드된다.
- [ ] `[manual-hard]` SampleScene Play 시 콘솔에 `[AuthBootstrap]` 관련 로그가 0건이고 `AuthSmokeProbe` 오버레이 패널이 표시되지 않는다(GameObject 비활성).
- [ ] `[manual-hard]` SampleScene 안의 `MultiplayerAuthGate` 버튼을 누르면 `MultiplayerAuthBootstrap`이 활성화되고 백엔드 인증이 실행되며 상태 라벨이 "Multiplayer 활성화 — <닉네임>"으로 갱신된다(백엔드가 떠 있을 때).
- [ ] `[manual-hard]` 백엔드가 떠 있지 않을 때 버튼을 누르면 상태 라벨이 "실패: <연결 거부 메시지>"로 표시되고 게임 다른 부분은 정상 동작한다.
- [ ] `[manual-hard]` Quest USB 실기기 빌드(Android)에서 같은 흐름이 동작한다 — 버튼 누르기 전 인증 시도 0건, 누른 후 mock 또는 real 토큰으로 인증 진행.

## Out of Scope

- 멀티플레이어 모드 비활성화 토글(한 번 켜면 세션 종료까지 유지)
- 룸 입장 UI(별도 sub-spec `04-presence-ui` 소관)
- 닉네임/계정 ID 변경 UI
- 메인 메뉴 시스템 도입(World-Space 단순 버튼 1개로 충분)

## Notes

- `MultiplayerAuthGate`는 단순 진입점이라 디자인 패턴을 무겁게 가져갈 필요 없다. MonoBehaviour + UnityEvent로 충분.
- `AuthBootstrap.authenticateOnStart`를 false로 바꾸면 기존 `AuthSmokeTest.unity` 씬은 영향이 없다(그 씬은 SmokeProbe 흐름이 별개).
- World-Space UI는 XR Interaction Toolkit의 ray interactor와 호환되도록 Canvas의 `Render Mode: World Space` + `Tracked Device Graphic Raycaster`(또는 동등)를 설정한다.
- 향후 멀티플레이어 진입 후 룸 합류 UI까지 같은 World-Space 패널에서 다루도록 확장할 수 있다(04-presence-ui plan에서 다룸).

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
