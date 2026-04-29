# Unity 클라이언트 인증 흐름

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Status:** `Ready`

## Goal

Unity 클라이언트가 앱 실행 시 자동으로 백엔드에 인증되고, Access Token 만료 시 Refresh Token으로 자동 갱신, Refresh Token까지 만료되면 Meta 토큰 재발급으로 fallback하는 인증 흐름을 구축한다.

## Context

`docs/specs/multiplayer-network/specs/01-user-auth.md`는 다음 Behavior를 요구한다.

- 앱이 실행될 때 Meta 기기 계정이 유효하면 자동 인증이 수행된다 (별도 로그인 화면 없이 입장).
- Access Token 만료 시 Refresh Token으로 새 Access Token이 발급된다.
- Refresh Token도 만료되면 Meta 재인증으로 새 토큰 쌍이 자동 발급된다.
- Meta 토큰 검증은 배포 환경(Production·시연·Quest 실기기)에서는 실 Meta Platform SDK, 개발·에디터 환경에서는 Mock 검증기를 사용한다.

**전제(선행 plan):** `2026-04-29-namae1128-auth-refresh-endpoint-and-tests.md`가 먼저 완료되어 백엔드에 `POST /api/v1/auth/meta-login`, `POST /api/v1/auth/refresh`, `GET /api/v1/users/me`가 살아있어야 한다.

**현재 Unity 상태:**
- `Assets/Photon/Fusion/`에 Photon Fusion SDK가 임포트되어 있다.
- 백엔드 호출 코드는 없다 — HTTP 클라이언트, DTO, 토큰 저장 모두 부재.
- Multiplayer 도메인 폴더(`Assets/Multiplayer/`)는 아직 없다. 새로 만든다.

**제약:**
- 기존 프로젝트는 `Assets/<도메인>/Scripts/` 패턴을 따른다 (예: `Assets/Hands/Scripts/`). 본 plan도 `Assets/Multiplayer/Scripts/`로 정렬한다.
- 실 Meta Platform SDK 호출은 Quest 실기기 검증이 필요하므로 본 plan에서는 **인터페이스 자리만 마련**한다 (실제 구현은 후속 plan).
- 토큰 저장은 일단 `PlayerPrefs` 사용. Quest 보안 저장(Keystore 등)은 추후 강화.

## Approach

1. **도메인 폴더 생성.** `Assets/Multiplayer/Scripts/Auth/`, `Assets/Multiplayer/Scripts/Backend/Http/`, `Assets/Multiplayer/Scripts/Backend/Dto/` 디렉토리. 어셈블리 정의 파일(`Murang.Multiplayer.asmdef`)을 `Assets/Multiplayer/Scripts/`에 두어 도메인 단위로 분리.
2. **DTO 정의.** `MetaLoginRequest`, `MetaLoginResponse`(중첩 `UserSummary`), `RefreshTokenRequest`, `ApiErrorResponse`(ProblemDetail 형식). 모두 `[Serializable]` POCO. JSON 매핑은 `JsonUtility` 또는 `Newtonsoft.Json` (이미 Unity Package로 들어와 있는지 확인 후 결정 — 없으면 `JsonUtility` 사용).
3. **백엔드 설정.** `MultiplayerAuthConfig` ScriptableObject — `backendBaseUrl`(예: `http://localhost:8080`), `useMockMetaToken`(bool), `mockMetaTokenPrefix`(예: `mock-meta:`), `mockAccountId`(예: `quest-user-01`), `defaultNickname`. `Resources/MultiplayerAuthConfig.asset`로 배치해 런타임 로드.
4. **Meta 토큰 발급기 추상화.**
   - `IMetaTokenProvider` 인터페이스: `Task<string> GetMetaIdTokenAsync(CancellationToken ct)`.
   - `MockMetaTokenProvider` 구현 — `mockMetaTokenPrefix + mockAccountId` 형태로 즉시 반환.
   - `RealMetaTokenProvider` 클래스는 **stub만 둔다** (`throw new NotImplementedException()` + TODO 주석). 본 plan의 Out of Scope.
   - 어떤 구현을 쓸지는 `MultiplayerAuthConfig.useMockMetaToken`로 분기 (런타임 결정). 빌드 환경 분기를 원하면 추후 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`로 강제 모드 추가.
5. **HTTP 클라이언트.** `BackendApiClient` — `UnityWebRequest` 래핑. `PostJsonAsync<TReq, TRes>`, `GetAsync<TRes>` 메서드. 응답 비-2xx면 `ApiException`(code, message) throw. 헤더 주입 hook으로 `Authorization: Bearer ...` 부착.
6. **토큰 저장.** `AuthTokenStore` — `PlayerPrefs`로 access/refresh/expiresAt 저장·조회·삭제. 클래스는 인터페이스 + 단일 구현 (추후 보안 저장 교체 대비).
7. **인증 세션.** `AuthSession`:
   - `LoginAsync()` — `IMetaTokenProvider` → `/auth/meta-login` 호출 → 토큰 저장.
   - `RefreshAsync()` — 저장된 refresh 토큰으로 `/auth/refresh` 호출 → 새 토큰 저장. 401이면 throw.
   - `EnsureAuthenticatedAsync()` — 저장된 access 유효하면 그대로, 만료면 `RefreshAsync` 시도, refresh도 실패하면 `LoginAsync` fallback.
   - 모든 경로에서 인증 실패 시 `AuthFailedException` throw.
8. **부트스트랩.** `AuthBootstrap` MonoBehaviour — `Start()`에서 `EnsureAuthenticatedAsync()` 실행. 결과(성공/실패/유저 정보)를 콘솔 로그 + `UnityEvent`로 노출. 씬에 `AuthBootstrap` 게임오브젝트 1개를 두고 `MultiplayerAuthConfig`를 reference.
9. **API 호출 헬퍼.** `BackendApiClient`가 호출 전 `AuthSession.EnsureAuthenticatedAsync()`를 자동 호출. 401 응답 시 한 번에 한해 `RefreshAsync` 또는 `LoginAsync` 후 재시도하는 인터셉터 추가.
10. **검증용 테스트 씬.** `Assets/Multiplayer/Scenes/AuthSmokeTest.unity` — `AuthBootstrap` + `users/me` 호출 후 콘솔 출력하는 간단한 테스트 컴포넌트. `manual-hard` 검증 시 사용.

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
- `Assets/Multiplayer/Scripts/Auth/RealMetaTokenProvider.cs` (stub)
- `Assets/Multiplayer/Scripts/Auth/AuthTokenStore.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthSession.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthFailedException.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthBootstrap.cs`
- `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthConfig.cs`
- `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset`
- `Assets/Multiplayer/Scenes/AuthSmokeTest.unity`
- `Assets/Multiplayer/Scripts/Auth/AuthSmokeProbe.cs` — 테스트 씬용 컴포넌트 (`/users/me` 호출 후 결과 로그)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Multiplayer/Scripts/`가 컴파일 오류 없이 빌드된다 (`Window > General > Console` 에 컴파일 에러 0).
- [ ] `[manual-hard]` `AuthSmokeTest` 씬에서 Play 모드 진입 시 백엔드(`localhost:8080`)에 `/api/v1/auth/meta-login` 호출이 발생하고 응답으로 받은 토큰이 PlayerPrefs에 저장된다 (콘솔 로그 + EditorPrefs 확인).
- [ ] `[manual-hard]` 토큰이 저장된 상태로 Play 모드를 재시작하면 `/meta-login` 호출 없이 저장된 access 토큰으로 `/users/me`가 200을 반환한다.
- [ ] `[manual-hard]` PlayerPrefs의 access 토큰만 의도적으로 손상시킨 뒤 재시작하면 자동으로 `/auth/refresh` 호출이 발생하고 새 토큰을 받아 `/users/me`가 200을 반환한다.
- [ ] `[manual-hard]` PlayerPrefs의 refresh 토큰까지 손상시킨 뒤 재시작하면 자동으로 Mock Meta 토큰 발급 → `/meta-login` 흐름이 동작하고 `/users/me`가 200을 반환한다.
- [ ] `[manual-hard]` `MultiplayerAuthConfig.useMockMetaToken = true` 상태에서 모든 인증 흐름이 실 Meta SDK 호출 없이 동작한다 (Editor 환경 가정).

## Out of Scope

- 실 Meta Platform SDK 토큰 발급 구현 (`RealMetaTokenProvider` 본체) — Quest 실기기 검증 필요, 별도 후속 plan.
- 보안 강화된 토큰 저장 (Quest Keystore 등) — 본 plan은 PlayerPrefs.
- DB 영속화 (`02-user-persistence` 범위).
- 룸 입장·세션 관리 (`03-room-session` 범위).
- 로그인 실패 UI (현재는 콘솔 로그만).
- Refresh Token rotation 정책 — 백엔드가 같은 refresh를 재발급해도 클라는 무리 없이 동작하도록 단순 구현.

## Notes

- 어셈블리 정의(`Murang.Multiplayer.asmdef`)를 두는 이유: 앞으로 룸/세션 코드가 같은 폴더에 추가될 때 컴파일 단위로 묶이도록 미리 경계를 그어둔다. Photon Fusion 어셈블리는 이미 별도 분리되어 있으므로 참조만 추가.
- `Newtonsoft.Json` 사용 가능 여부는 구현 직전에 확인. 없으면 `JsonUtility`로 작성하되 중첩 객체(`UserSummary`) 직렬화에 주의.
- `BackendApiClient`의 401 자동 재시도는 1회로 제한 — 무한 루프 방지.

## Handoff

<!-- /spec-implement 가 plan 완료 시 채움. -->
