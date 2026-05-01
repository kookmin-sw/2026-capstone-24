# 유저 데이터 영속화

**Parent:** [`_index.md`](../_index.md)

## Why

인증만으로는 유저 정보가 서버 프로세스 재시작 시 사라진다. 멀티플레이어 서비스에서는 같은 Meta 계정이 다시 접속해도 동일한 유저로 식별되고, 닉네임과 마지막 접속 시각 같은 기본 프로필이 유지되어야 한다.

## What

서버에서 인증된 유저 정보를 DB에 저장하고 조회하는 기능을 제공한다. 최초 로그인 시 유저 레코드를 생성하고, 재접속 시 기존 레코드를 조회해 같은 유저로 연결한다.

## Behavior

- **Given** 최초 로그인한 유저가  
  **When** 인증에 성공하면  
  **Then** 유저 레코드가 DB에 생성된다.

- **Given** 이미 가입한 유저가  
  **When** 다시 로그인하면  
  **Then** 기존 레코드가 조회되어 동일한 유저로 연결된다.

- **Given** 서버가 재시작되더라도  
  **When** 유저가 다시 접속하면  
  **Then** 이전 유저 정보가 그대로 유지된다.

## Out of Scope

- 유저 프로필 수정 UI
- 유저 차단/제재 기능
- 게임 플레이 통계 저장

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-30 | 백엔드 유저 영속화 전환 | `Done` | [2026-04-30-namae1128-backend-user-persistence.md](../plans/2026-04-30-namae1128-backend-user-persistence.md) |

> 상태 값은 `Ready` / `In Progress` / `Done`

## Open Questions

- 없음
