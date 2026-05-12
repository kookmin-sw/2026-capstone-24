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
| 유저 인증 | `Active` | [01-user-auth.md](specs/01-user-auth.md) |
| 유저 데이터 영속화 | `Active` | [02-user-persistence.md](specs/02-user-persistence.md) |
| 멀티플레이어 룸 세션 | `Active` | [03-room-session.md](specs/03-room-session.md) |
| 접속 상태 UI | `Draft` | [04-presence-ui.md](specs/04-presence-ui.md) |
| 룸 오케스트레이션 | `Draft` | [05-room-orchestration.md](specs/05-room-orchestration.md) |
| 빌드 타깃 분리 | `Draft` | [06-build-targets.md](specs/06-build-targets.md) |
| 룸 상태 snapshot | `Draft` | [07-room-state-snapshot.md](specs/07-room-state-snapshot.md) |

> 상태 값은 `Draft` / `Active` / `Done` / `Abandoned`

## Out of Scope

- 구체적인 멀티플레이 콘텐츠 동기화 — 아바타·손 동작, 악기 움직임, MIDI event 등 (별도 피처/브랜치에서 다룬다. 본 피처는 빌드 파이프라인·실행 환경·인프라·아키텍처·룸 lifecycle·접속 상태까지로 책임 한정)
- 텍스트 채팅
- 리플레이/녹화
- 관전 모드

## Open Questions

- 없음

## Status

`Active`
