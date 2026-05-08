# 백엔드 real Meta 토큰 verifier (Oculus Graph API)

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Status:** `Ready`

## Goal

Quest Android 빌드의 `RealMetaTokenProvider`가 발급한 `meta-user-proof:` payload를 백엔드에서 **Oculus Graph API로 실제 검증**해서, mock 토큰이 아닌 실 Meta 계정 인증으로 `playerId`까지 일관되게 발급한다.

## Context

- 현재 [`backend/src/main/resources/application.yml`](../../../../backend/src/main/resources/application.yml)는 `app.security.meta.verifier-mode: mock`으로 고정되어 있고, 검증기는 [`MockMetaIdTokenVerifier`](../../../../backend/src/main/java/com/murang/auth/service/MockMetaIdTokenVerifier.java)뿐이다. `mock-meta:<accountId>` prefix만 검증한다.
- Unity 측 [`RealMetaTokenProvider.cs`](../../../../Assets/Multiplayer/Scripts/Auth/RealMetaTokenProvider.cs)는 Quest Android에서 Meta Platform SDK의 `Core.AsyncInitialize → Entitlements.IsUserEntitledToApplication → Users.GetLoggedInUser → Users.GetUserProof` 순서로 계정 증명을 받아 `{userId, userProof}` JSON을 base64url로 인코딩해 `meta-user-proof:` prefix를 붙인 payload를 만든다 — 이미 작성되어 있다.
- 즉 클라이언트 측 실 토큰 발급은 준비되어 있고, **백엔드 검증기만 구현하면 end-to-end 실 인증이 동작**한다.
- Oculus Graph API의 user proof 검증 endpoint:
  ```
  GET https://graph.oculus.com/user_proof_validate?access_token=<APP_ID>|<APP_SECRET>&user_id=<USER_ID>&nonce=<USER_PROOF>
  ```
  성공 시 200 + `{"is_valid": true}`. 실패 시 `{"is_valid": false}` 또는 4xx.
- App ID와 App Secret은 Meta Developer Dashboard의 앱 설정에서 발급받는다(Quest 빌드의 `OculusPlatformSettings`에 이미 App ID가 박혀 있고, App Secret은 별도 secrets로 관리).

## Decisions

- **검증기 인터페이스 유지** — 기존 `MetaIdTokenVerifier` SPI를 그대로 두고 `RealMetaIdTokenVerifier` 구현 추가. mock/real은 `app.security.meta.verifier-mode` 프로퍼티로 분기.
- **HTTP 클라이언트** — Spring 6의 `RestClient`(blocking) 사용. 비동기 필요 없음(인증 단일 호출).
- **App Secret 주입 경로** — `MURANG_META_APP_SECRET` 환경변수. App ID는 이미 Unity 클라이언트에 박혀 있으므로 백엔드도 같은 값 필요 — `MURANG_META_APP_ID` 환경변수.
- **타임아웃** — 5초 connect / 5초 read. Oculus API 평균 응답은 200ms 미만.
- **payload 디코딩** — `meta-user-proof:` prefix 제거 → base64url 디코딩 → Jackson으로 `{userId, userProof}` 파싱. 형식 깨지면 `AUTH_INVALID_TOKEN(401)`.
- **검증 실패 응답** — `is_valid: false` 또는 4xx → `AUTH_INVALID_TOKEN(401)`. 5xx 또는 timeout → `META_VERIFIER_UNAVAILABLE(503)` — Meta 측 일시 장애와 토큰 위조 구분.
- **단위 테스트** — `RestClient`를 mock해서 검증 성공/실패/장애 분기를 모두 커버. 실 Oculus API 호출은 통합 테스트 X(외부 의존성).

## Approach

1. **DTO 추가** — `backend/src/main/java/com/murang/auth/dto/MetaUserProofPayload.java` (record `{ String userId, String userProof }`).
2. **`RealMetaIdTokenVerifier` 구현**:
   - `String prefix = "meta-user-proof:"` 검증.
   - prefix 제거 후 base64url 디코딩 → `MetaUserProofPayload`로 deserialize.
   - `RestClient.get()` → `https://graph.oculus.com/user_proof_validate?access_token=APP_ID|APP_SECRET&user_id=...&nonce=...` 호출.
   - 응답 JSON에서 `is_valid` 필드 확인 → true이면 `MetaIdentity(userId)` 반환.
   - false 또는 4xx → `ApiException(AUTH_INVALID_TOKEN)`.
   - 5xx/timeout → `ApiException(META_VERIFIER_UNAVAILABLE)`.
3. **`MetaProperties`에 `appId`, `appSecret` 추가** — `app.security.meta.app-id`, `app.security.meta.app-secret`. `application.yml`에 환경변수 주입.
4. **`SecurityConfig` 또는 `AuthConfiguration` 빈 등록** — `verifier-mode: real`이면 `RealMetaIdTokenVerifier`, `mock`이면 기존 `MockMetaIdTokenVerifier` 주입. `@ConditionalOnProperty`로 분기.
5. **`ErrorCode` 추가** — `META_VERIFIER_UNAVAILABLE(503, "Meta 인증 서버에 일시적으로 접근할 수 없습니다.")`.
6. **`application.yml` / `application-prod.yml` 갱신**:
   - `application.yml`: `verifier-mode: ${MURANG_META_VERIFIER_MODE:mock}` (default mock으로 두어 개발 환경 영향 없음).
   - `application-prod.yml`: 별도 변경 없이 환경변수로 `real` 주입 권장.
7. **`.env.example` 갱신** — `MURANG_META_VERIFIER_MODE`, `MURANG_META_APP_ID`, `MURANG_META_APP_SECRET` placeholder 추가.
8. **단위 테스트** — `backend/src/test/java/com/murang/auth/service/RealMetaIdTokenVerifierTest.java`:
   - 정상 payload + Graph API 200 `{is_valid: true}` → `MetaIdentity` 반환.
   - prefix 누락 → `AUTH_INVALID_TOKEN`.
   - base64url 깨짐 → `AUTH_INVALID_TOKEN`.
   - Graph API 200 `{is_valid: false}` → `AUTH_INVALID_TOKEN`.
   - Graph API 4xx → `AUTH_INVALID_TOKEN`.
   - Graph API 5xx → `META_VERIFIER_UNAVAILABLE`.
   - Graph API timeout → `META_VERIFIER_UNAVAILABLE`.
   - `RestClient`는 `MockRestServiceServer` 또는 직접 모의 객체로 주입.
9. **Unity 측 변경** — [`MultiplayerAuthConfig.asset`](../../../../Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset)의 `useMockMetaToken: 1` → `0`. (Editor에서 Play 시 RealMetaTokenProvider가 호출되지만 비-Android 환경에서는 안전 실패하므로 Editor 검증은 mock으로 다시 돌리거나 별도 mock 설정 유지 가능 — 본 plan은 Quest 실기기 검증을 우선시.)
10. **수동 검증 절차 문서화** — `docs/dev/local-stack.md`에 짧은 섹션 추가:
    - `.env`에 `MURANG_META_VERIFIER_MODE=real`, `MURANG_META_APP_ID`, `MURANG_META_APP_SECRET` 채우기.
    - `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d`.
    - Quest 실기기 빌드 → `MultiplayerAuthGate` 누름 → 컨테이너 spring 로그에서 `RealMetaIdTokenVerifier` Graph API 호출 + 200 `is_valid: true` 응답 확인.

## Deliverables

- `backend/src/main/java/com/murang/auth/dto/MetaUserProofPayload.java`
- `backend/src/main/java/com/murang/auth/service/RealMetaIdTokenVerifier.java`
- `backend/src/main/java/com/murang/auth/config/MetaProperties.java` (또는 기존 SecurityProperties 확장)
- `backend/src/main/java/com/murang/auth/config/MetaVerifierConfiguration.java` — `@ConditionalOnProperty`로 mock/real 분기
- `backend/src/main/java/com/murang/common/exception/ErrorCode.java` — `META_VERIFIER_UNAVAILABLE` 추가
- `backend/src/main/resources/application.yml` — `app.security.meta.verifier-mode` 환경변수 기반화
- `backend/src/test/java/com/murang/auth/service/RealMetaIdTokenVerifierTest.java`
- `.env.example` — Meta 환경변수 추가
- `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset` — `useMockMetaToken: 0`
- `docs/dev/local-stack.md` — 실 verifier 사용 절차 추가

## Acceptance Criteria

- [ ] `[auto-hard]` `cd backend && ./gradlew test`가 통과한다(신규 단위 테스트 7개 포함).
- [ ] `[auto-hard]` `RealMetaIdTokenVerifierTest`의 7개 시나리오(정상/prefix누락/base64깨짐/is_valid:false/4xx/5xx/timeout)가 모두 통과한다.
- [ ] `[auto-hard]` Spring `MURANG_META_VERIFIER_MODE=mock`으로 띄우면 기존 mock 흐름이 그대로 동작한다(회귀 없음).
- [ ] `[manual-hard]` Spring `MURANG_META_VERIFIER_MODE=real` + 유효한 `MURANG_META_APP_ID`/`MURANG_META_APP_SECRET`으로 띄운 상태에서 Quest 실기기 빌드의 `MultiplayerAuthGate`를 누르면, Graph API에 호출이 가고 spring 로그에 `is_valid: true` 응답 확인 후 200 + ULID `playerId` 응답이 반환된다.
- [ ] `[manual-hard]` Quest 실기기에서 두 번째 로그인 시 동일 `metaAccountId`(= Oculus userId)에 대해 동일 `playerId`가 반환된다(MariaDB 영속성 + 실 Meta 계정 매칭).
- [ ] `[manual-hard]` `MURANG_META_APP_SECRET`을 잘못된 값으로 주입한 상태에서 Quest 빌드가 인증 시도하면 `AUTH_INVALID_TOKEN(401)` 응답 확인.

## Out of Scope

- Meta App ID/Secret을 AWS Secrets Manager로 옮기는 작업(별 plan, AWS 배포 단계).
- App Secret rotation 정책.
- Quest 외 Meta 플랫폼(예: Meta Quest PC App) 지원.
- App ID/Secret 발급 절차(외부 작업 — Meta Developer Dashboard).
- Editor에서 real verifier 검증(Editor는 mock 유지가 자연스러움).

## Notes

- App Secret은 절대 git에 커밋되지 않도록 `.env.example`에는 placeholder만 두고 `.gitignore`에서 `.env` 차단을 재확인한다.
- Oculus Graph API endpoint URL은 Meta 측 변경 가능성이 있으므로 `application.yml`에 `app.security.meta.graph-base-url` 같은 값으로 외부화하는 것도 고려(기본값 `https://graph.oculus.com`).
- `Users.GetUserProof`가 반환하는 nonce는 1회용 — 본 plan의 검증기는 stateless로 동작하지만 Meta 측이 같은 nonce를 두 번 검증해도 동일하게 처리할지는 문서 확인 필요. 만약 1회용이면 클라이언트가 매 인증마다 새 nonce를 요청해야 하며, 이는 `RealMetaTokenProvider` 동작과 일치한다(매 EnsureAuthenticatedAsync 호출마다 새 proof 발급).
- Editor 환경에서 `useMockMetaToken: 0`으로 두면 `RealMetaTokenProvider`가 비-Android 환경 가드로 안전 실패한다 — 즉 Editor Play는 본 plan 적용 후 인증 흐름이 깨질 수 있으니, 본 plan과 함께 [인-게임 멀티플레이어 진입 게이트](2026-05-08-namae1128-multiplayer-auth-gate.md) plan을 우선 적용해 자동 인증을 방지하는 것이 안전하다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
