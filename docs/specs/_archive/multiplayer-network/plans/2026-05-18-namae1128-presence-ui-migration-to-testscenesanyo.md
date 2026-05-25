# Presence UI SampleScene → TestSceneSanyo 마이그레이션

**Linked Spec:** [`04-presence-ui.md`](../../../multiplayer-network/specs/04-presence-ui.md)
**Caused By:** default 씬 매핑이 `SampleScene` → `TestSceneSanyo` 로 전환된 [`decisions/01-default-scene.md`](../../../multiplayer-network/decisions/01-default-scene.md) 결정. SampleScene 에만 박혀 있는 multiplayer UI 4개 root GameObject 가 새 default 씬에서 누락된 정합성 차이를 닫는다.
**Status:** `Done (2026-05-25) — auto-hard 6건 통과 (commit 20cf4c0), manual-hard 1건 (Quest AuthGate→인증→statusLabel) AWS dev backend (HTTPS 도메인) 실기기 검증 통과.`

## Goal

[`c495623 feat(presence/scene): SampleScene에 MultiplayerInRoomPanel + ParticipantRow.prefab + 와이어링`](https://github.com/kookmin-sw/2026-capstone-24/commit/c495623) 으로 SampleScene 에 박힌 multiplayer UI 4개 root GameObject 를 default 씬([`decisions/01-default-scene.md`](../decisions/01-default-scene.md))으로 이전해서, 새 default 씬에서 인증·룸 합류·접속자 패널이 동작하는 상태를 만든다.

본 plan 은 lobby 패널 신규 ([`2026-05-16-namae1128-presence-ui-lobby-panel.md`](./2026-05-16-namae1128-presence-ui-lobby-panel.md))의 선결 조건이다. lobby plan 의 default 씬 추상 표기는 본 plan 완료 후 자동으로 새 default 씬 기준으로 해석된다.

## Context

- 2026-05-17 [`decisions/01-default-scene.md`](../decisions/01-default-scene.md) 으로 default 씬 매핑이 `SampleScene` → `TestSceneSanyo` 로 전환됐다. 그러나 multiplayer UI 4개 root GameObject 는 SampleScene 에만 박혀 있어 default 씬 빌드 시 multiplayer 기능이 빠진 산출물이 된다.
- 새 default 씬에는 multiplayer 관련 GameObject 0건 (`Grep 결과 0 매치 (2026-05-18)`).
- 두 씬은 같은 VR Player rig prefab (guid `7bf49c709166cad468c62b67a3e78040`) 인스턴스를 LocalPosition/Rotation 동일하게 보유 — multiplayer UI 의 World Space Canvas 좌표를 그대로 옮겨도 VR Player 시야 기준으로 호환된다 (`unity-scene-reader 보고 (2026-05-18)`).
- [`presence-ui-in-room-panel.md`](./2026-05-16-namae1128-presence-ui-in-room-panel.md) plan 의 Status `Ready` 는 stale — [c495623] 이 그 plan 의 Approach 대부분을 SampleScene 에 이미 실현했고, 본 plan 이 그것을 새 default 씬으로 옮긴다. 본 plan 완료 후 그 plan Status 갱신은 별도 후속 정리 commit 책임 (Notes 참조).

## Verified Structural Assumptions

SampleScene 의 multiplayer UI 4개 root GameObject 구조 (`unity-scene-reader 보고 (2026-05-18)`):

- **`MultiplayerAuthGate`** (World Space Canvas) — Position `(0, 0, 0.6)`, LocalScale `(0.002, 0.002, 0.002)`, AnchoredPosition `(0, 1.2)`, SizeDelta `(400, 200)`. 자식 `ActivateButton`, `StatusLabel`. SerializeField `multiplayerAuthBootstrap`, `authBootstrap`, `activateButton`, `statusLabel`. Script GUID `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6`.
- **`MultiplayerInRoomPanel`** (World Space Canvas) — Position `(0, 0, 0)`, LocalRotation Y=90° (`0, 0.7071068, 0, 0.7071068`), LocalScale `(0.002, 0.002, 0.002)`, AnchoredPosition `(4, 1.5)`, SizeDelta `(1200, 800)`. 자식 `InRoomRoot` (inactive) → `RoomNameLabel`, `ParticipantCountLabel`, `ParticipantList`, `LeaveButton`. SerializeField 10건 (`lobbyPanel`, `authGate`, `roomClient`, `networkRunner`, `participantRowParent`, `participantRowPrefab` GUID `c57f723f47142f446930f83bbde8a115`, `roomNameLabel`, `participantCountLabel`, `leaveButton`, `inRoomRoot`). Script GUID `a29273255ccbe3c439c95dcb9e7af985`. `lobbyPanel` 와이어는 stale — lobby plan 미실행 상태.
- **`MultiplayerAuthBootstrap`** (GameObject, **inactive** `m_IsActive: 0`) — `AuthBootstrap` (config GUID `e446380221ca43f2b77b3d17c7f05105`, `authenticateOnStart: 0`), `AuthSmokeProbe` (`runOnStart: 1`, `showDebugOverlay: 1`).
- **`MultiplayerRoomNetworking`** (GameObject, active) — `RoomListQuery` + `RoomClient` (둘 다 config GUID `bbd8ee6d975a7db43ba0cf4d010d096e`), `NetworkRunner` (Fusion).

`ParticipantRow.prefab` 은 `Assets/Multiplayer/Resources/` 산하라 두 씬 모두에서 접근 가능 — 마이그레이션 후에도 prefab GUID reference 가 깨지지 않는다.

## Approach

1. **default 씬에 4개 root GameObject 이식** — `unity-scene-writer` 로 SampleScene 의 `MultiplayerAuthGate`, `MultiplayerInRoomPanel`, `MultiplayerAuthBootstrap`, `MultiplayerRoomNetworking` 4개 root GameObject 를 default 씬에 신설.

   - Transform: 위 Verified Structural Assumptions 의 LocalPosition / Rotation / Scale / AnchoredPosition / SizeDelta 값을 1차로 그대로 복제. implementer 는 신설 후 unity-scene-reader 또는 Editor Scene View 로 VR Player rig 시야와의 정합을 확인해 좌표 미세 조정 가능 — 단 폭은 **좌·우 ±1m, 회전 ±45° 이내**로 한정. 본격 UX 리디자인은 Out of Scope.
   - 컴포넌트: 4개 GameObject 의 각 컴포넌트(Canvas, RectTransform, CanvasScaler, GraphicRaycaster / TrackedDeviceGraphicRaycaster, MultiplayerAuthGate / MultiplayerInRoomPanel / AuthBootstrap / AuthSmokeProbe / RoomListQuery / RoomClient / NetworkRunner) 를 모두 동일 type · 동일 SerializeField 값으로 신설.
   - 자식 트리: `MultiplayerAuthGate` → `ActivateButton` (+ ButtonLabel), `StatusLabel`. `MultiplayerInRoomPanel` → `InRoomRoot` (inactive) → `RoomNameLabel`, `ParticipantCountLabel`, `ParticipantList`, `LeaveButton`. 자식의 RectTransform, Image, Button, TextMeshProUGUI 컴포넌트 + 텍스트·폰트 설정을 그대로 이전.

2. **MultiplayerAuthBootstrap 은 inactive 유지** — SampleScene 동일하게 `m_IsActive: 0`. MultiplayerAuthGate 가 ActivateButton 클릭 시 SetActive(true) 하는 기존 패턴 유지.

3. **와이어링 재구성** — 4개 root 가 모두 새 default 씬 인스턴스이므로 SerializeField fileID 가 새 fileID 로 직렬화된다. 다음 cross-ref 만 정확히 해결:

   - `MultiplayerAuthGate.multiplayerAuthBootstrap` → 새 `MultiplayerAuthBootstrap` GameObject.
   - `MultiplayerAuthGate.authBootstrap` → 새 `AuthBootstrap` component.
   - `MultiplayerAuthGate.activateButton` → 새 `ActivateButton.Button`.
   - `MultiplayerAuthGate.statusLabel` → 새 `StatusLabel.TextMeshProUGUI`.
   - `MultiplayerInRoomPanel.authGate` → 새 `MultiplayerAuthGate` component.
   - `MultiplayerInRoomPanel.roomClient` → 새 `MultiplayerRoomNetworking` 의 `RoomClient` component.
   - `MultiplayerInRoomPanel.networkRunner` → 새 `MultiplayerRoomNetworking` 의 `NetworkRunner` component.
   - `MultiplayerInRoomPanel.participantRowParent` → 새 `ParticipantList` Transform.
   - `MultiplayerInRoomPanel.roomNameLabel` / `participantCountLabel` / `leaveButton` / `inRoomRoot` → 각각 새 자식 GameObject 의 해당 컴포넌트.
   - `MultiplayerInRoomPanel.lobbyPanel` → lobby plan 미실행 상태이므로 `{fileID: 0}` 으로 명시 (SampleScene 의 stale 값을 따라가지 않도록). lobby plan 실행 시 정상 와이어.
   - `MultiplayerInRoomPanel.participantRowPrefab` → prefab GUID `c57f723f47142f446930f83bbde8a115` 그대로 (자산 reference 보존).

4. **SampleScene 정리는 별도 plan** — 본 plan 은 default 씬에 복제만 한다. SampleScene 의 multiplayer UI 4개 root 를 제거할지·유지할지는 별도 plan 으로 분리 (git history 보존 vs cleanup trade-off, 디버그 fallback 으로 SampleScene 활용 여부 결정 필요).

## Deliverables

- default 씬 자산 ([`../decisions/01-default-scene.md`](../decisions/01-default-scene.md) 가 현재 매핑된 `.unity` 경로 박제) — 4개 root GameObject + 자식 트리 + 컴포넌트 + 와이어링 신설.

## Acceptance Criteria

- [ ] `[auto-hard]` `Grep MultiplayerAuthGate|MultiplayerInRoomPanel|MultiplayerAuthBootstrap|MultiplayerRoomNetworking` 가 default 씬 `.unity` 에서 ≥ 4 root GameObject 매치.
- [ ] `[auto-hard]` `Grep RoomClient|NetworkRunner|RoomListQuery|AuthBootstrap|AuthSmokeProbe` 가 default 씬 `.unity` 에서 ≥ 5 컴포넌트 매치.
- [ ] `[auto-hard]` `MultiplayerInRoomPanel` 의 SerializeField 10건 중 `lobbyPanel` 제외 9건이 모두 non-zero fileID 로 직렬화 (`lobbyPanel: {fileID: 0}` 만 예외, 나머지 매치 0건).
- [ ] `[auto-hard]` `MultiplayerAuthBootstrap` GameObject 의 `m_IsActive: 0` (inactive 유지).
- [ ] `[auto-hard]` `participantRowPrefab` 직렬화에 prefab GUID `c57f723f47142f446930f83bbde8a115` 매치.
- [ ] `[auto-hard]` Unity Editor 로드 시 콘솔 에러·예외 0건 (`read_console` 호출 결과 verified).
- [ ] `[manual-hard]` Quest 실기기 빌드 (default 씬 = TestSceneSanyo) + aws-dev backend (mock 모드 OK): MultiplayerAuthGate 의 ActivateButton 클릭 → MultiplayerAuthBootstrap SetActive → AuthBootstrap 으로 인증 → `Multiplayer Active — <nickname>` statusLabel 표시. SampleScene 에서의 [c495623] 사이클과 같은 동작 재현.

## Out of Scope

- SampleScene 에서 multiplayer UI 4개 root 제거 (별도 plan).
- lobby 패널 신규 ([`2026-05-16-namae1128-presence-ui-lobby-panel.md`](./2026-05-16-namae1128-presence-ui-lobby-panel.md) 책임). 본 plan 완료 후 lobby plan 의 default 씬 추상 표기는 자동으로 새 default 씬 기준.
- in-room 패널 위치/크기의 본격적 UX 리디자인 — 본 plan 은 좌표 미세 조정만 허용 (±1m, ±45° 이내). 본격 리디자인은 사용자 1차 실기기 검증 결과를 반영한 후속 plan.
- nickname 채널 추가 (별도 plan, [`presence-ui-in-room-panel.md`](./2026-05-16-namae1128-presence-ui-in-room-panel.md) Out of Scope 박제 그대로).
- real Meta verifier 전환·`useMockMetaToken: 0` 토글 — 1번 후속 작업 사이클 책임. 본 plan acceptance 의 manual-hard 는 mock 모드로 검증 가능.

## Notes

- 본 plan 완료 후 [`presence-ui-in-room-panel.md`](./2026-05-16-namae1128-presence-ui-in-room-panel.md) plan 의 Status 는 stale 가 된다. 본 plan atomic commit 의 분리된 후속 정리 commit 에서 in-room plan Status 를 `Done — superseded by 2026-05-18 migration plan` 으로 갱신하거나 archive 로 이동시킨다.
- lobby plan 의 "Verified Structural Assumptions" 중 `default 씬 (당시 매핑 SampleScene, 2026-05-16 기준) 안 MultiplayerAuthGate ...` 자산 fact 는 본 plan 완료 후 새 default 씬 기준으로 재검증 대상이다. lobby plan 실행 시 implementer 가 unity-scene-reader 로 재검증해야 한다 (이미 lobby plan L26 에 그 절차가 박제됨).
- 좌표 미세 조정이 ±1m / ±45° 한계를 넘어가야 한다고 판단되면 본 plan 의 acceptance 범위 밖. 별도 UX 리디자인 plan 으로 분리하고 본 plan 은 1차 복제만으로 닫는다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
