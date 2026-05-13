# 멀티플레이어 네트워크

## Why

VirtualMusicStudio의 핵심 경험은 여러 유저가 같은 VR 공간에서 악기를 함께 연주하는 것이다. 이를 위해 공통 신원, 룸 입장, 동기화된 세션, 접속 UI가 필요하다.

## What

유저가 Meta 계정으로 로그인하고 룸을 생성하거나 기존 룸에 입장해 다른 유저와 같은 공간을 공유하는 기반 시스템을 제공한다.

- 유저가 Meta 계정으로 인증한다.
- 유저 정보가 저장되고 재사용된다.
- 여러 유저가 같은 룸에 동시에 접속할 수 있다.
- 현재 접속 상태를 UI에서 확인할 수 있다.
- 개인 룸 snapshot을 기반으로 멀티 룸 초기 상태를 구성한다.
- 룸 서버 lifecycle과 실행 환경을 Spring 내부 `RoomServerManager` 모듈로 관리한다.

## 현재 진행 스냅샷

| 목표 | 상태 | 비고 |
|---|---|---|
| 초기 서버/DB 환경 만들고 실행하기 | `로컬 완료 / AWS dev 미완료` | 로컬 `docker-compose` 스택은 닫혔고, EC2 배포는 active plan으로 남아 있다. |
| Meta ID로 로그인 | `mock 경로 완료 / real Meta 미완료` | Unity device 토큰 브리지는 들어가 있지만, backend real verifier와 실기기 검증은 아직 남아 있다. |
| 멀티플레이 룸 생성/참가/나가기 | `핵심 로직 완료` | 룸 세션 라이프사이클은 닫혔지만, dedicated-server 빌드 안정화(OpenXR 토글)와 실기기 검증은 아직 필요하다. |
| 생성된 룸 목록 확인 UI | `부분 구현 / 검증 미종결` | 룸 목록 조회 코드와 smoke 자산은 들어왔지만 acceptance가 안 닫혀 active로 복귀시켰다. |
| 룸 내부 접속자 수 확인 UI | `미착수` | 아바타가 없으므로 다중 Wi-Fi 실기기 테스트 전 필수 항목이다. |
| AWS dev 환경 만들기 | `미완료` | 첫 실기기 테스트의 가장 큰 병목이다. |

## Plan 정리 기준

- active plan: `7`
- archive plan: `8`

이번 정리에서는 archive 안에 있던 plan 중 아래 두 개를 다시 active로 꺼냈다.

- `2026-04-29-namae1128-auth-refresh-endpoint-and-tests.md`
- `2026-05-01-namae1128-room-list-query.md`

이유는 두 plan 모두 문서상 `Status: Done`으로 닫혀 있었지만 acceptance criteria 체크가 끝나지 않았기 때문이다. 현재 규칙은 "구현 흔적이 있다"보다 "acceptance가 닫혔다"를 우선한다.

## 첫 실기기 테스트 크리티컬 패스

1. `AWS EC2 dev 배포`
2. `dedicated-server 빌드 OpenXR 토글`
3. `인-게임 멀티플레이 진입 게이트`
4. `backend real Meta verifier`
5. `룸 목록 조회 acceptance 종료`
6. `접속 현황 UI(04-presence-ui) plan 작성 및 구현`

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| 유저 인증 | `Active` | [01-user-auth.md](specs/01-user-auth.md) |
| 유저 데이터 영속화 | `Active` | [02-user-persistence.md](specs/02-user-persistence.md) |
| 멀티플레이어 룸 세션 | `Active` | [03-room-session.md](specs/03-room-session.md) |
| 접속 상태 UI | `Draft` | [04-presence-ui.md](specs/04-presence-ui.md) |
| 룸 서버 매니저 | `Draft` | [05-room-server-manager.md](specs/05-room-server-manager.md) |
| 빌드 타깃 분리 | `Draft` | [06-build-targets.md](specs/06-build-targets.md) |
| 룸 상태 snapshot | `Draft` | [07-room-state-snapshot.md](specs/07-room-state-snapshot.md) |

> 상태 값은 `Draft` / `Active` / `Done` / `Abandoned`

## Out of Scope

- 구체적인 멀티플레이 콘텐츠 동기화 — 아바타·손 동작, 악기 움직임, MIDI event 등 (별도 피처/브랜치에서 다룬다. 본 피처는 인증·개인 룸 snapshot·룸 lifecycle·실행 환경·오케스트레이션·접속 상태까지로 책임 한정)
- 텍스트 채팅
- 리플레이/녹화
- 관전 모드

## Open Questions

- 접속 현황 UI는 `SampleScene` 월드스페이스 패널로 시작할지, 별도 로비/테스트 씬으로 분리할지?
- 실기기 테스트 1차 목표는 `현재 인원/정원`만으로 충분한지, 아니면 `닉네임 목록`까지 바로 필요한지?

## Status

`Active`
