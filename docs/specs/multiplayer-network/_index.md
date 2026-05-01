# 멀티플레이어 네트워크

## Why

VirtualMusicStudio의 핵심 경험은 여러 유저가 같은 VR 공간에서 악기를 함께 연주하는 것이다. 이를 위해 공통 신원, 룸 입장, 동기화된 세션, 접속 UI가 필요하다.

## What

유저가 Meta 계정으로 로그인하고 룸을 생성하거나 기존 룸에 입장해 다른 유저와 같은 공간을 공유하는 기반 시스템을 제공한다.

- 유저가 Meta 계정으로 인증한다.
- 유저 정보가 저장되고 재사용된다.
- 여러 유저가 같은 룸에 동시에 접속할 수 있다.
- 현재 접속 상태를 UI에서 확인할 수 있다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| 유저 인증 | `Done` | [01-user-auth.md](specs/01-user-auth.md) |
| 유저 데이터 영속화 | `Done` | [02-user-persistence.md](specs/02-user-persistence.md) |
| 멀티플레이어 룸 세션 | `Active` | [03-room-session.md](specs/03-room-session.md) |
| 접속 상태 UI | `Draft` | [04-presence-ui.md](specs/04-presence-ui.md) |

> 상태 값은 `Draft` / `Active` / `Done` / `Abandoned`

## Out of Scope

- 텍스트 채팅
- 리플레이/녹화
- 관전 모드

## Open Questions

- 없음

## Status

`Active`
