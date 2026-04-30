# Quest 앱 인증 브리지 후속 구현

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)  
**Status:** `Done`

## Goal

기존 로컬 스모크 테스트용 Unity 인증 흐름을 실제 Quest 앱 연동 방향으로 확장해, 실기기용 Meta 계정 증명 취득 경로와 씬 부트스트랩까지 준비한다.

## Context

- 기존 plan [`2026-04-29-namae1128-unity-client-auth-flow.md`](2026-04-29-namae1128-unity-client-auth-flow.md)는 전체 인증 흐름과 스모크 테스트를 마쳤지만, 실제 Meta Platform SDK 토큰 취득 구현은 out-of-scope로 남겨두었다.
- 현재 백엔드 [`application.yml`](/D:/2026-capstone-24/backend/src/main/resources/application.yml)는 `app.security.meta.verifier-mode: mock` 상태라서, 이번 턴의 실기기 경로는 Unity 쪽 payload/설정/씬 연결까지를 우선 완료하고 서버 실검증기는 후속 작업으로 둔다.
- 닉네임은 백엔드 validation 상 2~32자의 문자/숫자/공백만 허용되고, 서로 다른 Meta 계정 간 중복 시 409 충돌이 난다. 고정 닉네임만 두면 실제 디바이스 로그인에서 쉽게 충돌한다.

## Approach

1. `IMetaTokenProvider`가 단순 문자열 대신 `metaIdToken + metaAccountId + displayName`을 함께 반환하도록 확장한다.
2. `MockMetaTokenProvider`는 기존 mock prefix 규약을 유지하면서 계정 ID도 함께 넘긴다.
3. `RealMetaTokenProvider`는 Quest Android 빌드에서 Meta Platform SDK의 `Core.AsyncInitialize -> Entitlements.IsUserEntitledToApplication -> Users.GetLoggedInUser -> Users.GetUserProof` 순서로 계정 증명을 취득하고, `{ userId, userProof }`를 `meta-user-proof:` prefix의 base64url envelope로 포장해 백엔드용 문자열 payload로 만든다.
4. `MultiplayerAuthConfig`는 editor/device backend URL을 분리하고, 명시적 닉네임 override가 없을 때는 `defaultNickname + accountId suffix` 형태의 계정별 기본 닉네임을 생성한다.
5. `SampleScene`에 `AuthBootstrap` GameObject를 직접 연결해, 메인 씬에서도 기존 `Resources/MultiplayerAuthConfig.asset` 기반 자동 인증이 시작되도록 한다.

## Deliverables

- `Assets/Multiplayer/Scripts/Auth/IMetaTokenProvider.cs`
- `Assets/Multiplayer/Scripts/Auth/MockMetaTokenProvider.cs`
- `Assets/Multiplayer/Scripts/Auth/RealMetaTokenProvider.cs`
- `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthConfig.cs`
- `Assets/Multiplayer/Scripts/Auth/AuthSession.cs`
- `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset`
- `Assets/Scenes/SampleScene.unity`

## Acceptance Criteria

- [x] `[auto-hard]` `IMetaTokenProvider` 계약이 계정 기반 닉네임 전략을 지원할 수 있도록 확장된다.
- [x] `[auto-hard]` `MultiplayerAuthConfig`가 editor/device 별 backend URL 분기를 제공한다.
- [x] `[auto-hard]` 닉네임 override가 비어 있을 때 `metaAccountId` 기반 suffix가 붙은 기본 닉네임이 사용된다.
- [x] `[manual-hard]` `SampleScene` 진입 시 `AuthBootstrap`이 자동 인증을 시도할 수 있는 연결 상태가 된다.
- [x] `[manual-hard]` Quest Android 빌드에서 Meta Platform SDK가 설치/설정되어 있으면 `RealMetaTokenProvider`가 userId + userProof payload를 구성한다.

## Notes

- 이번 턴의 `meta-user-proof:` envelope 형식은 아직 백엔드 real verifier가 없는 상태에서 Unity 측 payload를 먼저 고정하기 위한 합의안이다. 서버 후속 구현은 이 prefix와 base64url JSON `{ userId, userProof }`를 해석하면 된다.
- 현재 백엔드가 `verifier-mode: mock` 이므로 `useMockMetaToken = false` 상태의 실제 디바이스 end-to-end 성공은 서버 실검증기 구현 전까지 보장되지 않는다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
