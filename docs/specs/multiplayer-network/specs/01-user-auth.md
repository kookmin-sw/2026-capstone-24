# 유저 인증

**Parent:** [`_index.md`](../_index.md)

## Why

멀티플레이어 공간에 입장하는 유저가 누구인지 식별되어야 룸 배정, 유저 정보 표시, 접속 상태 추적이 가능하다. Meta Quest 기기를 주요 대상으로 삼고 있으므로 Meta 계정을 신원 소스로 사용한다.

## What

Meta 계정 기반 인증 흐름을 제공한다. 클라이언트가 Meta ID 토큰을 서버에 전달하면 서버가 이를 검증하고 자체 JWT를 발급하며, 이후 보호된 요청은 JWT로 인증한다.

- Access Token은 짧은 유효기간을 갖고 Refresh Token으로 갱신된다.
- Refresh Token이 만료되면 Meta 계정이 유효한 경우 자동으로 다시 로그인된다.
- 개발/에디터 환경에서는 Mock 검증기를 사용할 수 있다.

## Behavior

- **Given** 유저가 Meta 기기에서 앱을 처음 실행했을 때  
  **When** 로그인을 시도하면  
  **Then** Meta ID 토큰이 서버로 전달되고 검증 성공 후 JWT가 반환된다.

- **Given** 유효한 JWT를 보유한 유저가  
  **When** 보호된 API 엔드포인트에 요청하면  
  **Then** 인가가 허용되고 응답이 반환된다.

- **Given** 만료되었거나 유효하지 않은 JWT로  
  **When** 요청하면  
  **Then** 401 응답이 반환된다.

- **Given** Access Token이 만료된 유저가  
  **When** Refresh Token으로 갱신을 요청하면  
  **Then** 새 Access Token이 발급된다.

- **Given** 앱이 실행될 때  
  **When** Meta 기기 계정이 유효하면  
  **Then** 자동 인증이 수행되어 별도 로그인 화면 없이 진입한다.

- **Given** Refresh Token이 만료된 유저가  
  **When** Meta 계정이 여전히 유효하면  
  **Then** Meta 로그인으로 새 토큰을 자동 발급받는다.

## Out of Scope

- DB 기반 유저 프로필 영속화
- 게스트/익명 로그인

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-29 | Spring 서버 인증 프로토타입 | `Done` | [2026-04-29-namae1128-spring-auth-prototype.md](../plans/2026-04-29-namae1128-spring-auth-prototype.md) |
| 2026-04-29 | 백엔드 Refresh 엔드포인트 및 테스트 보강 | `Done` | [2026-04-29-namae1128-auth-refresh-endpoint-and-tests.md](../plans/2026-04-29-namae1128-auth-refresh-endpoint-and-tests.md) |
| 2026-04-29 | Unity 클라이언트 인증 흐름 | `Done` | [2026-04-29-namae1128-unity-client-auth-flow.md](../plans/2026-04-29-namae1128-unity-client-auth-flow.md) |
| 2026-04-30 | Quest 앱 인증 브리지 후속 구현 | `Done` | [2026-04-30-namae1128-quest-app-auth-bridge.md](../plans/2026-04-30-namae1128-quest-app-auth-bridge.md) |

> 상태 값은 `Ready` / `In Progress` / `Done`

## Open Questions

- _없음_
