# 백엔드 Refresh 엔드포인트 및 테스트 보강

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Status:** `Done`

## Goal

Spring 백엔드에 Refresh Token 갱신 엔드포인트를 추가하고, spec Behavior 시나리오 전반을 자동 테스트로 검증한다.

## Context

`docs/specs/multiplayer-network/specs/01-user-auth.md`는 다음을 요구한다.

- Access Token은 짧은 유효 기간(현재 `application.yml`에서 `PT15M`)을 갖고 Refresh Token(현재 `P7D`, 향후 30일 이상)으로 갱신된다.
- Refresh Token으로 새 Access Token을 발급받을 수 있어야 한다 (spec Behavior #4).
- 보호 엔드포인트는 유효 JWT에서만 200, 만료·무효 JWT는 401 (spec Behavior #2, #3).
- 닉네임 중복 시 `AUTH_NICKNAME_DUPLICATE(409)`, validation 실패 시 `VALIDATION_REQUEST(400)`.

**현재 백엔드 상태:**
- `POST /api/v1/auth/meta-login`은 구현되어 있고 응답에 access/refresh 토큰을 함께 반환한다.
- `JwtTokenService`는 access/refresh 두 종류 토큰 발급·검증을 모두 지원한다 (`parseAccessToken`만 public, refresh 측 파싱은 private).
- `JwtAuthenticationFilter`가 Authorization 헤더를 파싱해 SecurityContext에 `AuthPrincipal`을 주입한다.
- 보호된 엔드포인트는 아직 없다 (spec Behavior #2 검증을 위해 1개 추가 필요).
- 기존 테스트(`AuthControllerTest`)는 정상 로그인 + 잘못된 토큰 거부 2건만 있다.
- `application.yml`에 `app.security.jwt.refresh-token-ttl: P7D`. spec은 30일 이상을 요구하므로 `P30D`로 상향한다.

**제약:**
- DB 영속화는 `02-user-persistence` 범위. 본 plan은 InMemoryUserRegistry 위에서 동작한다.
- 실 Meta Platform SDK 검증기 구현은 Quest 실기기 검증이 필요하므로 별도 후속 plan으로 미룬다.

## Approach

1. **Refresh 토큰 검증 노출.** `JwtTokenService`에 refresh 토큰 파싱 메서드(`parseRefreshToken`)를 추가하고, refresh 성공 시 발급할 새 토큰 쌍 메서드(`reissueTokens(metaAccountId)`)를 노출한다. 기존 `parseAndValidate` 로직 재사용.
2. **AuthService.refresh** 추가. Refresh Token 파싱 → `InMemoryUserRegistry`에서 metaAccountId로 유저 조회 → 새 토큰 쌍 발급 → `MetaLoginResponse` 동일 형식으로 반환. 유저가 없으면 `AUTH_INVALID_JWT`로 통일 (refresh 후 등록 해제된 경우 등).
3. **`POST /api/v1/auth/refresh`** 엔드포인트를 `AuthController`에 추가. 요청 DTO `RefreshTokenRequest(String refreshToken)`. 응답은 `MetaLoginResponse` 재사용.
4. **SecurityConfig** 의 `permitAll` 패턴에 `/api/v1/auth/refresh` 추가.
5. **보호 엔드포인트 추가** — `GET /api/v1/users/me`. `SecurityContextHolder` 기반 `AuthPrincipal` 조회 후 `{userId, metaAccountId, nickname}` 반환. 컨트롤러는 `com.murang.user.controller.UserController`.
6. **JWT TTL 정책 갱신** — `application.yml`의 `refresh-token-ttl`을 `P30D`로 변경. `application-test.yml`에는 짧은 만료(`PT1S` 등) 시나리오용 프로필 또는 테스트에서 동적 오버라이드 가능하도록 두는 방안 중 후자(`@DynamicPropertySource` 또는 `@TestPropertySource`)를 사용해 만료 테스트 작성.
7. **테스트 보강** — `AuthControllerTest`에 시나리오 추가:
   - 닉네임 중복(`AUTH_NICKNAME_DUPLICATE`) — 다른 Meta 계정으로 같은 닉네임 두 번째 로그인.
   - 닉네임 validation(`VALIDATION_REQUEST`) — 빈/32자 초과/특수문자.
   - `/api/v1/auth/refresh` 정상 — 로그인 후 받은 refresh로 갱신 성공.
   - `/api/v1/auth/refresh` 거부 — access 토큰을 refresh로 사용 시 401, 무효 토큰 401.
   - `GET /api/v1/users/me` 정상 — Bearer access 토큰으로 200.
   - `GET /api/v1/users/me` 401 — 토큰 없음, 무효, (별도 짧은 만료 프로파일로) 만료.
8. **gradle test** 로 회귀 확인.

## Deliverables

- `backend/src/main/java/com/murang/auth/dto/RefreshTokenRequest.java` — 새 요청 DTO
- `backend/src/main/java/com/murang/auth/service/JwtTokenService.java` — refresh 파싱·재발급 메서드 추가
- `backend/src/main/java/com/murang/auth/service/AuthService.java` — `refresh()` 메서드 추가
- `backend/src/main/java/com/murang/auth/controller/AuthController.java` — `POST /refresh` 추가
- `backend/src/main/java/com/murang/auth/config/SecurityConfig.java` — `/refresh` permitAll
- `backend/src/main/java/com/murang/user/controller/UserController.java` — 새 컨트롤러, `GET /me`
- `backend/src/main/resources/application.yml` — `refresh-token-ttl: P30D`
- `backend/src/test/java/com/murang/auth/controller/AuthControllerTest.java` — 시나리오 추가
- `backend/src/test/java/com/murang/user/controller/UserControllerTest.java` — 보호 엔드포인트 테스트

## Acceptance Criteria

- [ ] `[auto-hard]` `POST /api/v1/auth/refresh`에 유효한 Refresh Token을 보내면 200과 새 access/refresh 토큰을 반환한다.
- [ ] `[auto-hard]` `POST /api/v1/auth/refresh`에 access 토큰이나 변조된 토큰을 보내면 401 `AUTH_INVALID_JWT`를 반환한다.
- [ ] `[auto-hard]` `GET /api/v1/users/me`에 유효한 access 토큰으로 요청하면 200과 `{userId, metaAccountId, nickname}`을 반환한다.
- [ ] `[auto-hard]` `GET /api/v1/users/me`에 토큰 없이/무효 토큰으로 요청하면 401 `AUTH_INVALID_JWT`를 반환한다.
- [ ] `[auto-hard]` 만료된 access 토큰으로 보호 엔드포인트에 요청하면 401을 반환한다 (테스트 프로파일에서 짧은 TTL 적용).
- [ ] `[auto-hard]` 다른 Meta 계정이 동일 닉네임으로 로그인을 시도하면 409 `AUTH_NICKNAME_DUPLICATE`를 반환한다.
- [ ] `[auto-hard]` 빈/32자 초과/허용되지 않는 특수문자 닉네임으로 로그인하면 400 `VALIDATION_REQUEST`를 반환한다.
- [ ] `[auto-hard]` `cd backend && ./gradlew test` 가 0 exit code로 통과한다.

## Out of Scope

- DB 영속화 (→ `02-user-persistence`).
- 실 Meta Platform SDK 검증기 구현 (별도 후속 plan, Quest 실기기 검증 필요).
- Unity 클라이언트 측 토큰 저장·자동 로그인 흐름 (→ `2026-04-29-namae1128-unity-client-auth-flow.md`).
- Refresh Token rotation/revocation 정책 (필요 시 후속 plan).

## Notes

- `users/me` 엔드포인트를 본 plan에서 추가하는 이유: spec Behavior #2 ("유효한 JWT를 보유한 유저가 보호된 API 엔드포인트에 요청하면 인가가 허용된다")의 자동 검증 대상이 필요하기 때문. DB 영속화가 추가되더라도 동일 응답 형식을 유지하면 된다.

## Handoff

- Unity 클라이언트 인증 plan은 `POST /api/v1/auth/meta-login`, `POST /api/v1/auth/refresh`, `GET /api/v1/users/me`가 준비된 상태를 전제로 이어서 구현하면 된다.
- `POST /api/v1/auth/refresh`는 `{ "refreshToken": "<jwt>" }` 요청 바디를 받고, 응답 포맷은 `meta-login`과 동일하다.
- `GET /api/v1/users/me`는 Bearer access token을 요구하고 `ApiResponse.data`에 `{ userId, metaAccountId, nickname }`를 반환한다.
- 자동 검증은 `backend/src/test/java/com/murang/auth/controller/AuthControllerTest.java`와 `backend/src/test/java/com/murang/user/controller/UserControllerTest.java`에 있다.
