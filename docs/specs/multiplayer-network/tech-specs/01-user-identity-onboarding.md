# 닉네임 입력 + real Meta 통합 진입 흐름

**Sub-Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Status:** `Draft`
**Date:** 2026-05-26

<!-- 본 Tech Spec 은 01-user-auth 를 1차 매핑하지만, 02-user-persistence
의 nickname 등록 경로도 cross-cutting 으로 다룬다. 02 와의 경계는
Boundaries 섹션에 명시한다. -->

## Components

- **MultiplayerAuthGate** (기존, TestSceneSanyo) — World-Space UI 패널. 초기에는 ActivateButton + StatusLabel 만 노출. 신규 판정 응답 후 ActivateButton 을 숨기고 NicknameInput + ConfirmButton 자식을 노출하는 토글 게이트.
- **NicknameInput / ConfirmButton 자식** (신규 GameObject 2건, TestSceneSanyo `MultiplayerAuthGate` 의 자식 트리에 추가) — 초기 비활성. 신규 판정 응답 후에만 활성화.
- **AuthBootstrap** (기존) — Meta SDK 토큰 획득 + Spring `/auth/meta-login` 호출. 진입 메서드에 nickname optional 인자 추가.
- **LobbyNicknameInputValidator** (신규) — trim/길이/정규식 검사 단일 함수. `LobbyInputValidator` 패턴 차용해 단위 테스트로 검증.
- **Spring AuthMetaLoginRequest DTO** (기존) — nickname optional 필드 추가.
- **Spring AuthService / UserService** (기존) — `findByMetaAccountId` 분기 + 신규 등록 시 nickname 까지 한 트랜잭션 안에서 create.

## Data / Control Flow

- (1차) ActivateButton click → `AuthBootstrap.AuthenticateAsync(null)` → Meta SDK Token API (real 모드) → HTTPS POST `/api/v1/auth/meta-login` `{ metaIdToken, nickname: null }`
- Spring `AuthController` → `MetaTokenVerifier` → `UserService.findByMetaAccountId(metaAccountId)`
  - **기존 유저**: 정상 응답 `{ playerId, accessToken, refreshToken, nickname }` → `MultiplayerAuthGate.OnAuthenticated(displayName)` → StatusLabel `"Multiplayer Active — <nickname>"` → ActivateButton/NicknameInput/ConfirmButton 모두 숨김
  - **신규 유저**: 400 `{ error: "NICKNAME_REQUIRED" }` → `MultiplayerAuthGate.OnNicknameRequired()` → ActivateButton.SetActive(false) + NicknameInput.SetActive(true) + ConfirmButton.SetActive(true) + StatusLabel 안내
- (2차, 신규 한정) 사용자 입력 후 ConfirmButton click → `LobbyNicknameInputValidator.Validate(nicknameInput.text)`
  - 위반: StatusLabel 사유 표시, 요청 미발사
  - 통과: `AuthBootstrap.AuthenticateAsync(nickname)` → HTTPS POST `/api/v1/auth/meta-login` `{ metaIdToken, nickname }`
- Spring → `UserService.create(metaAccountId, nickname)` → MariaDB → 정상 응답 → `MultiplayerAuthGate.OnAuthenticated(displayName)` → 세 GameObject 모두 숨김 + LobbyPanel 진입
- 1차 실패 분기 (Meta SDK 토큰 획득 실패, 네트워크 오류, backend `NICKNAME_REQUIRED` 이외의 모든 응답): StatusLabel `"Failed: <message>"`, ActivateButton `m_IsActive: 1` 유지, NicknameInput / ConfirmButton 토글 발생 안 함 (계속 비활성).
- 2차 실패 분기 (닉네임 등록 호출 시 오류): StatusLabel `"Failed: <message>"`, NicknameInput.text 보존, NicknameInput / ConfirmButton 활성 상태 유지 (사용자 재시도 가능).

## Boundaries

- **건드린다**: TestSceneSanyo `MultiplayerAuthGate` 자식 트리 (NicknameInput / ConfirmButton 추가), `AuthBootstrap` signature, `MultiplayerAuthConfig` (`useMockMetaToken` 토글 유지), Spring `AuthController` / `AuthService` / `AuthMetaLoginRequest` DTO / `UserService.create+findByMetaAccountId` 경로, MariaDB `users` 테이블의 신규 row 생성 시 nickname 컬럼 set.
- **건드리지 않는다**: `MultiplayerLobbyPanel` / `MultiplayerInRoomPanel` 자식 트리, Photon Custom Auth payload, RoomJoinTicket payload, `users.user_id` / `users.player_id` / `users.metaAccountId` 컬럼, JWT 발급 로직, refresh token 흐름, 기존 유저의 `users.nickname` 갱신 경로 (등록 후 update 안 함).

## Invariants

- `nickname` 은 trim 후 1~16자, 정규식 `^[A-Za-z0-9_-]{1,16}$` (영문/숫자/언더스코어/하이픈) 만족.
- 신규 유저 등록 시점에만 `users.nickname` 이 셋업. 동일 `metaAccountId` 로 재로그인 시 `nickname` 은 변경되지 않음.
- 초기 UI: ActivateButton `m_IsActive: 1`, NicknameInput / ConfirmButton `m_IsActive: 0`.
- 신규 판정 응답 후: ActivateButton `m_IsActive: 0`, NicknameInput / ConfirmButton `m_IsActive: 1`. 세 GameObject 의 활성 상태는 mutually exclusive (ActivateButton vs NicknameInput + ConfirmButton).
- 인증 최종 성공 시 세 GameObject 모두 `m_IsActive: 0` 으로 수렴.
- ActivateButton → NicknameInput/ConfirmButton 으로의 UI transition 트리거는 **400 `NICKNAME_REQUIRED` 응답 한 가지만**. 그 외 1차 실패 (토큰 실패 / 네트워크 / 401 / 500 / `NICKNAME_REQUIRED` 이외 4xx 등) 는 transition 을 발생시키지 않음.
- 모든 오류 응답은 사유를 StatusLabel 에 표시. 클라이언트는 사유 텍스트를 backend message 또는 표준 라벨 (`"NETWORK_ERROR"`, `"AUTH_INVALID_TOKEN"` 등) 그대로 출력.
- `useMockMetaToken=true` 일 때도 동일 흐름 (mock 도 신규/기존 분기 결과 동일).
- 인증 실패 시 NicknameInput.text 미변경.

## Assumptions

- Meta SDK 토큰 발급은 Quest 실기기 빌드에서만 동작 — 출처: `Read docs/specs/multiplayer-network/specs/01-user-auth.md (2026-05-26)`.
- Meta Horizon 개발자 대시보드 팀원 멤버 초대 완료 + Quest 기기들 개발자 모드 ON — 출처: 사용자 명시 (2026-05-26).
- backend HTTPS 도메인 `api.mu-rang.com` 동작 + ECS Fargate room-server 연결 가능 — 출처: archive `aws-dev-https-caddy-cloudflare` plan (Done 2026-05-25).
- `02-user-persistence` 가 이미 `users.nickname` 컬럼 박제 — 출처: `Read docs/specs/multiplayer-network/specs/02-user-persistence.md L11 (2026-05-26)`.
- `MultiplayerAuthGate` 의 root Canvas (fileID 800001000) 와 두 자식 (ActivateButton 800001010, StatusLabel 800001030) 구조 — 출처: `unity-scene-reader` Pattern A 보고 (2026-05-26).

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `MultiplayerLobbyPanel.CreateForm.RoomNameInput` (fileID 310003000, TMP_InputField + VR 키보드 통합) | `MultiplayerAuthGate` 의 신규 `NicknameInput` 자식 | LobbyPanel 의 InputField + VR 키보드 패턴 그대로 차용. anchor 만 (0,1) → (0.5, ~0.6) 중앙 정렬로 교체. |
| `MultiplayerLobbyPanel.CreateForm.CreateButton` (Button + label) | `MultiplayerAuthGate` 의 신규 `ConfirmButton` 자식 | LobbyPanel Create 버튼 패턴 차용. label 만 "확인" 으로 교체. ActivateButton 자리를 이어받는 anchor. |
| `LobbyInputValidator` (archive `presence-ui-lobby-panel` plan 의 단위 테스트 박제 검사기) | `LobbyNicknameInputValidator` | 같은 정규식 기반 검사기 패턴. 규칙만 `^[A-Za-z0-9_-]{1,16}$` 로 교체. |

### Prefab Hierarchy

`unity-scene-reader` Pattern A 보고 (2026-05-26) — 현 상태:

```
MultiplayerAuthGate (fileID 800001000, root)
  RectTransform — AnchorMin(0,0) AnchorMax(0,0) AnchoredPos(4, 0.8) SizeDelta(400, 200) LocalScale(0.002)
  Canvas (WorldSpace, RenderMode=2)
  CanvasScaler + GraphicRaycaster ×2 + MultiplayerAuthGate (MonoBehaviour, script GUID a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6)
  ├─ ActivateButton (800001010) — AnchorMin/Max(0.5, 0.6) SizeDelta(240, 60), Button (800001014) + Image (800001013)
  │  └─ ButtonLabel (800001020) — TMP "Activate Multiplayer"
  └─ StatusLabel (800001030) — AnchorMin(0,0) AnchorMax(1,0.5), TMP (800001033)
```

SerializeField 와이어링 (현재): `multiplayerAuthBootstrap` (1211601285) / `authBootstrap` (1211601287) / `activateButton` (800001014 Button) / `statusLabel` (800001033 TMP).

신규 추가될 두 자식 GameObject (`NicknameInput` TMP_InputField + `ConfirmButton` Button) 의 권장 anchor 영역 — ActivateButton 자리를 이어받는 위치 (`AnchorMin/Max ≈ (0.5, 0.6)` 부근, SizeDelta 는 InputField (240, 50) + ConfirmButton (240, 60) 수직 스택). 초기 `m_IsActive: 0`. 정확한 좌표·계층 순서·라벨 텍스트는 implementer 책임. SerializeField 추가는 `nicknameInput` / `confirmButton` 2개.

## Open Tech Decisions

- [x] nickname 전송 채널 → [`decisions/03-nickname-transport-channel.md`](../decisions/03-nickname-transport-channel.md)
- [x] nickname 입력 cadence → [`decisions/04-nickname-input-cadence.md`](../decisions/04-nickname-input-cadence.md)
