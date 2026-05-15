# 멀티플레이어 네트워크

## Why

VirtualMusicStudio의 핵심 경험은 여러 유저가 같은 VR 공간에서 악기를 함께 연주하는 것이다. 이를 위해 공통 신원, 룸 입장, 동기화된 세션, 접속 UI가 필요하다.

## What

유저가 Meta 계정으로 로그인하고 룸을 생성하거나 기존 룸에 입장해 다른 유저와 같은 공간을 공유하는 기반 시스템을 제공한다.

- 유저가 Meta 계정으로 인증한다.
- 유저 정보가 저장되고 재사용된다.
- 여러 유저가 같은 룸에 동시에 접속할 수 있다.
- 현재 접속 상태를 UI에서 확인할 수 있다.
- 모든 룸은 동일한 default 씬(`SampleScene`)을 사용하며, 씬에는 악기·오브젝트가 미리 배치되어 있다. 유저는 그 배치된 오브젝트만 사용할 수 있고 룸 내부에서 오브젝트를 추가·이동·삭제하지 않는다.
- 룸 서버 lifecycle과 실행 환경을 Spring 내부 `RoomServerManager` 모듈로 관리한다.

## 현재 진행 스냅샷

| 목표 | 상태 | 비고 |
|---|---|---|
| 초기 서버/DB 환경 만들고 실행하기 | `로컬 완료 / AWS dev control plane 완료, 합류 미검증` | 로컬 `docker-compose` 스택은 archive 로 닫혔다. EC2 + Spring + MariaDB + ECS 토폴로지는 [`aws-dev-topology-ec2-fargate`](plans/2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md) plan AC #1·#2 통과 (compose syntax + 외부 actuator/health). 나머지 manual-hard 8건은 Quest 합류 검증과 묶여 있다. |
| Meta ID로 로그인 | `백엔드 mock + real 완료 / Quest 실기기 검증 보류` | backend mock·real verifier 와 Unity 클라이언트 인증 흐름은 모두 archive 로 닫혔다. Quest 빌드 + 헤드셋 시나리오 5건은 [`quest-onsite-integration-verification`](plans/2026-05-11-namae1128-quest-onsite-integration-verification.md) plan 에 묶여 있다. |
| 멀티플레이 룸 생성/참가/나가기 | `핵심 로직 + dedicated-server 빌드 완료 / 실기기 합류 보류` | 룸 세션 라이프사이클·dedicated-server 빌드·OpenXR 토글·로컬 docker-compose 스택 모두 archive 로 닫혔다. Quest 실기기 합류는 위 통합 검증 plan 에서 일괄 검증. |
| 생성된 룸 목록 확인 UI | `데이터 계층 완료 / UI plan 작성 완료 / 미구현` | `RoomListQuery` 데이터 노출 plan 은 archive 로 닫혔고, UI 바인딩은 [`2026-05-16-namae1128-presence-ui-lobby-panel`](plans/2026-05-16-namae1128-presence-ui-lobby-panel.md) plan 책임. |
| 룸 내부 접속자 수 확인 UI | `plan 작성 완료 / 미구현 / 닉네임은 후속` | [`2026-05-16-namae1128-presence-ui-in-room-panel`](plans/2026-05-16-namae1128-presence-ui-in-room-panel.md) plan 이 1차 출시 범위에서 playerId 기반 행을 표시. nickname 채널은 후속 plan. |
| AWS dev 환경 만들기 | `control plane 동작 / Quest 합류 미검증` | EC2 control plane 부팅과 외부 `/actuator/health` 통과까지 완료. ECS RunTask → Fargate room-server 합류는 Quest 빌드와 동시 검증 예정. |

## 첫 실기기 테스트 크리티컬 패스

1. `Quest 빌드 device backend URL` 을 EC2 public DNS 로 설정
2. `MURANG_META_VERIFIER_MODE=real` + 유효한 `APP_ID`/`APP_SECRET` 으로 backend 재기동
3. Quest 빌드 + 사이드로드 후 [`quest-onsite-integration-verification`](plans/2026-05-11-namae1128-quest-onsite-integration-verification.md) 시나리오 5건 일괄 검증
4. `aws-dev-topology-ec2-fargate` plan 의 잔여 manual-hard (Fargate RunTask, ready callback, 합류 로그, StopTask, CloudWatch, mysqldump cron) 동시 통과
5. `04-presence-ui` 2개 plan 구현 (lobby panel + in-room panel — Quest 듀얼 사이드로드로 입퇴장 실시간 표시 검증)

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| 유저 인증 | `Active` | [01-user-auth.md](specs/01-user-auth.md) |
| 유저 데이터 영속화 | `Active` | [02-user-persistence.md](specs/02-user-persistence.md) |
| 멀티플레이어 룸 세션 | `Active` | [03-room-session.md](specs/03-room-session.md) |
| 접속 상태 UI | `Active` | [04-presence-ui.md](specs/04-presence-ui.md) |
| 룸 서버 매니저 | `Active` | [05-room-server-manager.md](specs/05-room-server-manager.md) |
| 빌드 타깃 분리 | `Draft` | [06-build-targets.md](specs/06-build-targets.md) |

> 상태 값은 `Draft` / `Active` / `Done` / `Abandoned`

## Out of Scope

- 구체적인 멀티플레이 콘텐츠 동기화 — 아바타·손 동작, 악기 움직임, MIDI event 등 (별도 피처/브랜치에서 다룬다. 본 피처는 인증·룸 lifecycle·실행 환경·오케스트레이션·접속 상태까지로 책임 한정)
- 유저별 룸 상태 영속화 / 개인 룸 snapshot / copy-on-create / save-back 흐름 (default 씬 + 기배치 오브젝트만 사용하므로 룸별 상태 직렬화·복원이 필요 없음)
- 룸 내부 오브젝트 추가·이동·삭제 (씬에 미리 배치된 오브젝트만 사용)
- 텍스트 채팅
- 리플레이/녹화
- 관전 모드

## Open Questions

- 접속 현황 UI는 `SampleScene` 월드스페이스 패널로 시작할지, 별도 로비/테스트 씬으로 분리할지?
- 실기기 테스트 1차 목표는 `현재 인원/정원`만으로 충분한지, 아니면 `닉네임 목록`까지 바로 필요한지?

## Status

`Active`
