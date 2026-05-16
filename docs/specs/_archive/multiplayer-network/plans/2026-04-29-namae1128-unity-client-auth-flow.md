# Unity 클라이언트 인증 흐름

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)  
**Status:** `Done`

## Goal

Unity 클라이언트가 앱 실행 시 자동으로 백엔드에 인증하고, Access Token 만료 시 Refresh Token으로 자동 갱신하며, Refresh Token까지 만료되면 Meta 로그인으로 fallback하는 인증 흐름을 구축한다.

## Context

`docs/specs/multiplayer-network/specs/01-user-auth.md`는 다음 동작을 요구한다.

- 앱이 실행될 때 Meta 기기 계정이 유효하면 자동 인증이 수행된다.
- Access Token이 만료되면 Refresh Token으로 새 Access Token을 발급받는다.
- Refresh Token도 만료되면 Meta 로그인으로 다시 토큰을 발급받는다.
- Meta 토큰 검증은 배포 환경에서는 실제 SDK를, 개발/에디터 환경에서는 Mock 검증기를 사용한다.

선행 plan `2026-04-29-namae1128-auth-refresh-endpoint-and-tests.md`가 먼저 완료되어, 백엔드에 `POST /api/v1/auth/meta-login`, `POST /api/v1/auth/refresh`, `GET /api/v1/users/me`가 준비되어 있어야 한다.

## Approach

1. `Assets/Multiplayer/Scripts/` 아래에 인증/백엔드 런타임 코드를 배치한다.
2. `MetaLoginRequest`, `MetaLoginResponse`, `RefreshTokenRequest`, `ApiErrorResponse` DTO를 정의한다.
3. `MultiplayerAuthConfig` ScriptableObject와 `Resources/MultiplayerAuthConfig.asset`로 런타임 설정을 공급한다.
4. `IMetaTokenProvider`, `MockMetaTokenProvider`, `RealMetaTokenProvider`를 분리해 Meta 토큰 취득 경로를 추상화한다.
5. `BackendApiClient`로 `/auth/meta-login`, `/auth/refresh`, `/users/me` HTTP 호출을 감싼다.
6. `AuthTokenStore`로 `PlayerPrefs` 기반 access/refresh/expiresAt 저장소를 구현한다.
7. `AuthSession`에서 `CachedAccessToken -> Refresh -> MetaLogin fallback` 인증 흐름을 관리한다.
8. `AuthBootstrap`으로 씬 시작 시 인증을 자동 실행하고 결과를 로그/이벤트로 노출한다.
9. `AuthSmokeProbe`로 `/users/me` 호출까지 포함한 수동 검증용 씬을 제공한다.

## Deliverables

- `Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef`
- `Assets/Multiplayer/Scripts/Backend/Dto/MetaLoginRequest.cs`
- `Assets/Multiplayer/Scripts/Backend/Dto/MetaLoginResponse.cs`
- `Assets/Multiplayer/Scripts/Backend/Dto/RefreshTokenRequest.cs`
- `Assets/Multiplayer/Scripts/Backend/Dto/ApiErrorResponse.cs`
- `Assets/Multiplayer/Scripts/Backend/Http/BackendApiClient.cs`
- `Assets/Multiplayer/Scripts/Backend/Http/ApiException.cs`
- `Assets/Multiplayer/Scripts/Auth/IMetaTokenProvider.cs`
- `Assets/Multiplayer/Scripts/Auth/MockMetaTokenProvider.cs`
- `Assets/Multiplayer/Scripts/Auth/RealMetaTokenProvider.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthTokenStore.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthSession.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthFailedException.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthBootstrap.cs`
- `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthConfig.cs`
- `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset`
- `Assets/Multiplayer/Scenes/AuthSmokeTest.unity`
- `Assets/Multiplayer/Scripts/Auth/AuthSmokeProbe.cs`

## Acceptance Criteria

- [x] `[auto-hard]` `Assets/Multiplayer/Scripts/`가 컴파일 에러 없이 빌드된다.
- [x] `[manual-hard]` `AuthSmokeTest` 씬 Play 모드 진입 시 `POST /api/v1/auth/meta-login`이 발생하고 응답 토큰이 `PlayerPrefs`에 저장된다.
- [x] `[manual-hard]` 토큰이 저장된 상태로 Play 모드를 다시 시작하면 `/meta-login` 없이 저장된 access token으로 `/users/me`가 200을 반환한다.
- [x] `[manual-hard]` `PlayerPrefs`의 access token만 손상시킨 뒤 재시작하면 `/auth/refresh`가 발생하고 새 토큰을 받아 `/users/me`가 200을 반환한다.
- [x] `[manual-hard]` `PlayerPrefs`의 refresh token까지 손상시킨 뒤 재시작하면 Mock Meta 토큰 발급 후 `/meta-login` 흐름으로 `/users/me`가 200을 반환한다.
- [x] `[manual-hard]` `MultiplayerAuthConfig.useMockMetaToken = true` 상태에서 전체 인증 흐름이 Meta SDK 호출 없이 동작한다.

## Out of Scope

- 실제 Meta Platform SDK 토큰 취득 구현
- Quest 전용 보안 저장소로의 토큰 저장소 강화
- DB 영속화
- 룸 입장/세션 관리
- 인증 실패 UI

## Notes

- `Murang.Multiplayer.asmdef`를 통해 멀티플레이어 런타임 코드를 별도 경계로 분리했다.
- `BackendApiClient`의 401 재시도는 1회로 제한해 무한 루프를 막았다.
- 2026-04-30: Unity MCP로 `AuthSmokeTest`를 검증했고 `MetaLogin`, `CachedAccessToken`, `Refresh`, `MetaLogin fallback` 경로를 모두 확인했다.
- 2026-04-30: 개발 중 포트 충돌을 피하기 위해 Unity MCP 포트를 `8081`로 분리했고, 백엔드는 `8080`에서 유지했다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
