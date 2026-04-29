# 유저 인증

**Parent:** [`_index.md`](../_index.md)

## Why

멀티플레이어 공간에 입장하는 유저가 누구인지 식별할 수 있어야 룸 배정, 유저 정보 저장, 접속 현황 표시가 가능하다. Meta Quest 기기 사용자를 대상으로 하므로 Meta 계정을 신원 소스로 활용한다.

## What

Meta 계정 기반 인증 흐름을 제공한다. 클라이언트가 Meta ID 토큰을 서버에 제출하면 서버가 검증 후 자체 JWT를 발급하고, 이후 요청은 JWT로 인가된다.

## Behavior

- **Given** 유저가 Meta 기기에서 앱을 처음 실행했을 때
  **When** 로그인을 시도하면
  **Then** Meta ID 토큰이 서버로 전달되고, 검증 성공 시 JWT가 반환된다.

- **Given** 유효한 JWT를 보유한 유저가
  **When** 보호된 API 엔드포인트에 요청하면
  **Then** 인가가 허용되고 응답이 반환된다.

- **Given** 만료되거나 유효하지 않은 JWT로
  **When** 요청하면
  **Then** 401 응답이 반환된다.

## Out of Scope

- DB 유저 정보 저장 (→ `02-user-persistence`)
- 게스트/익명 로그인
- 소셜 로그인 (Meta 외 플랫폼)
- 토큰 갱신(Refresh Token) 흐름

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-29 | Spring 서버 인증 프로토타입 | `Done` | [2026-04-29-namae1128-spring-auth-prototype.md](../plans/2026-04-29-namae1128-spring-auth-prototype.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용.

## Open Questions

- [ ] 실 기기에서 Meta Platform SDK의 토큰 발급 방식 확인 필요 (현재 Mock 구현)
- [ ] JWT 만료 시간 정책
