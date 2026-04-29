# 유저 인증

**Parent:** [`_index.md`](../_index.md)

## Why

멀티플레이어 공간에 입장하는 유저가 누구인지 식별할 수 있어야 룸 배정, 유저 정보 저장, 접속 현황 표시가 가능하다. Meta Quest 기기 사용자를 대상으로 하므로 Meta 계정을 신원 소스로 활용한다.

## What

Meta 계정 기반 인증 흐름을 제공한다. 클라이언트가 Meta ID 토큰을 서버에 제출하면 서버가 검증 후 자체 JWT를 발급하고, 이후 요청은 JWT로 인가된다.

- Access Token은 짧은 유효 기간(수 분~수십 분)을 갖고 Refresh Token으로 갱신된다. Refresh Token은 30일 이상의 유효 기간을 갖는다.
- Refresh Token 만료 시에도 Meta 계정이 기기에서 유효하면 사용자 개입 없이 새 토큰 쌍이 발급된다.
- Meta 토큰 검증은 배포 환경(Production·시연·Quest 실기기)에서는 실 Meta Platform SDK를, 개발·에디터 환경에서는 Mock 검증기를 사용하도록 분기된다.

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

- **Given** Access Token이 만료된 유저가
  **When** Refresh Token으로 갱신을 요청하면
  **Then** 새 Access Token이 발급된다.

- **Given** 앱이 실행될 때
  **When** Meta 기기 계정이 유효하면
  **Then** 자동 인증이 수행되어 별도 로그인 화면 없이 입장한다.

- **Given** Refresh Token이 만료된 유저가
  **When** Meta 계정이 기기에서 유효하면
  **Then** Meta 재인증으로 새 토큰 쌍이 사용자 개입 없이 자동 발급된다.

## Out of Scope

- DB 유저 정보 저장 (→ `02-user-persistence`)
- 게스트/익명 로그인
- 소셜 로그인 (Meta 외 플랫폼)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-29 | Spring 서버 인증 프로토타입 | `Done` | [2026-04-29-namae1128-spring-auth-prototype.md](../plans/2026-04-29-namae1128-spring-auth-prototype.md) |
| 2026-04-29 | 백엔드 Refresh 엔드포인트 및 테스트 보강 | `Ready` | [2026-04-29-namae1128-auth-refresh-endpoint-and-tests.md](../plans/2026-04-29-namae1128-auth-refresh-endpoint-and-tests.md) |
| 2026-04-29 | Unity 클라이언트 인증 흐름 | `Ready` | [2026-04-29-namae1128-unity-client-auth-flow.md](../plans/2026-04-29-namae1128-unity-client-auth-flow.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용.

## Open Questions

- _없음_
