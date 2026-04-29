# 멀티플레이어 네트워크

## Why

VirtualMusicStudio의 핵심 경험은 다른 유저와 같은 VR 공간에서 악기를 함께 연주하는 것이다. 싱글플레이만으로는 협주·합주라는 음악의 사회적 본질을 재현할 수 없으므로, 유저 간 실시간 공유 공간이 반드시 필요하다.

## What

유저가 Meta 계정으로 로그인하고, 룸을 생성하거나 기존 룸에 입장해 다른 유저와 같은 공간에 함께 존재할 수 있는 기반을 제공한다.

- 유저가 Meta 계정으로 인증하고 서버에 신원이 등록된다.
- 유저 정보가 DB에 영속적으로 저장·조회된다.
- 여러 유저가 하나의 룸에 동시 접속해 같은 공간을 공유한다.
- 실시간으로 어떤 유저가 접속 중인지 UI에서 확인할 수 있다.
- 유저가 룸을 생성하거나 입장·퇴장할 수 있는 UI가 제공된다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| 유저 인증 | `Draft` | [01-user-auth.md](specs/01-user-auth.md) |
| 유저 데이터 영속화 | `Draft` | [02-user-persistence.md](specs/02-user-persistence.md) |
| 멀티플레이어 룸 세션 | `Draft` | [03-room-session.md](specs/03-room-session.md) |
| 접속 현황 UI | `Draft` | [04-presence-ui.md](specs/04-presence-ui.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Out of Scope

- 음성 채팅
- 레코딩 / 리플레이
- 관객 모드
- 아바타·손 동작 실시간 동기화
- 악기 연주 동기화

## Open Questions

- [ ] DB는 어떤 종류를 사용하는가? (PostgreSQL, MySQL 등)
- [ ] 룸 최대 인원 제한은 얼마인가?
- [ ] Photon Fusion 앱 ID 관리 방식 (환경별 분리 여부)

## Status

`Draft`
