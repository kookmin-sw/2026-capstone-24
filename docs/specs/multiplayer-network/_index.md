# 멀티플레이어 네트워크

## Why

VirtualMusicStudio의 핵심 경험은 여러 유저가 같은 VR 공간에서 악기를 함께 연주하는 것이다. 이를 위해 공통 신원, 룸 입장, 동기화된 세션, 접속 UI가 필요하다.

## What

유저가 Meta 계정으로 로그인하고 룸을 생성하거나 기존 룸에 입장해 다른 유저와 같은 공간을 공유하는 기반 시스템을 제공한다.

- 유저가 Meta 계정으로 인증한다.
- 유저 정보가 저장되고 재사용된다.
- 여러 유저가 같은 룸에 동시에 접속할 수 있다.
- 현재 접속 상태를 UI에서 확인할 수 있다.
- 모든 룸은 동일한 default 씬을 사용하며, 씬에는 악기·오브젝트가 미리 배치되어 있다. 유저는 그 배치된 오브젝트만 사용할 수 있고 룸 내부에서 오브젝트를 추가·이동·삭제하지 않는다. default 씬이 실제로 어떤 Unity 씬에 매핑되는지는 [`decisions/01-default-scene.md`](decisions/01-default-scene.md) 가 단일 source of truth.
- 룸 서버 lifecycle과 실행 환경을 Spring 내부 `RoomServerManager` 모듈로 관리한다.

## 현재 진행 스냅샷

| 목표 | 상태 | 비고 |
|---|---|---|
| 초기 서버/DB 환경 만들고 실행하기 | `Done (2026-05-25)` | 로컬 docker-compose 스택은 archive 로 닫혔고, EC2 + Spring + MariaDB + ECS 토폴로지도 Quest 실기기 합류까지 통과해 [`aws-dev-topology-ec2-fargate`](../_archive/multiplayer-network/plans/2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md) plan archive 처리. 운영 자계 3건 (CloudWatch custom metric, MariaDB inbound 차단 검증, mysqldump cron) 은 발표 범위 밖으로 deferred. |
| Meta ID로 로그인 | `Done (2026-05-25)` | backend mock·real verifier + Unity 클라이언트 인증 + Quest 실기기 시나리오 5건 모두 통과. [`quest-onsite-integration-verification`](../_archive/multiplayer-network/plans/2026-05-11-namae1128-quest-onsite-integration-verification.md) plan archive 처리. |
| 멀티플레이 룸 생성/참가/나가기 | `Done (2026-05-25)` | 룸 세션 라이프사이클·dedicated-server 빌드·OpenXR 토글·로컬 docker-compose 스택·Quest 실기기 합류 모두 archive 로 닫혔다. docker-compose 후속 자동화였던 `stack-smoke-automation` plan 은 2026-05-25 abandon (dev 무게중심이 EC2+ECS 로 이동) 후 짝 산출물 `tools/run-room-lifecycle-automation.ps1` 도 함께 삭제. |
| 생성된 룸 목록 확인 UI | `Done (2026-05-25)` | `RoomListQuery` 데이터 노출 + lobby 패널 UI 바인딩이 [`presence-ui-lobby-migration-and-ux`](../_archive/multiplayer-network/plans/2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md) 에서 Phase A/B/C 모두 완료. Quest 실기기에서 LobbyPanel end-to-end 동작 확인. |
| 룸 내부 접속자 수 확인 UI | `Done (2026-05-25, 닉네임은 후속)` | [`presence-ui-migration-to-testscenesanyo`](../_archive/multiplayer-network/plans/2026-05-18-namae1128-presence-ui-migration-to-testscenesanyo.md) 가 in-room 패널을 TestSceneSanyo 로 마이그레이션, Quest 실기기 참가자 리스트 동작 확인. nickname 채널 추가는 후속 plan. |
| AWS dev 환경 만들기 | `Done (2026-05-25)` | EC2 control plane + ECS Fargate + Caddy/Cloudflare HTTPS 종단 (api.mu-rang.com) 까지 모두 Quest 실기기 합류로 통과 확인. [`aws-dev-topology-ec2-fargate`](../_archive/multiplayer-network/plans/2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md) + [`aws-dev-https-caddy-cloudflare`](../_archive/multiplayer-network/plans/2026-05-16-namae1128-aws-dev-https-caddy-cloudflare.md) 둘 다 archive 처리. |

## 첫 실기기 테스트 크리티컬 패스 (2026-05-25 통과)

1. ✅ Quest 빌드 device backend URL 을 HTTPS 도메인 (`https://api.mu-rang.com`) 으로 설정 — Caddy + Cloudflare + Let's Encrypt 종단 동작 확인.
2. ✅ Spring `MURANG_META_VERIFIER_MODE=real` + 유효한 `APP_ID`/`APP_SECRET` 으로 backend 재기동.
3. ✅ Quest 빌드 + 사이드로드 후 [`quest-onsite-integration-verification`](../_archive/multiplayer-network/plans/2026-05-11-namae1128-quest-onsite-integration-verification.md) 시나리오 5건 일괄 통과.
4. ✅ [`aws-dev-topology-ec2-fargate`](../_archive/multiplayer-network/plans/2026-05-07-namae1128-aws-dev-topology-ec2-fargate.md) 의 manual-hard 5/8 통과 (Fargate RunTask · ready callback · 합류 로그). 운영 자계 3건 (StopTask 정리, CloudWatch metric, mysqldump cron) 은 발표 범위 밖으로 deferred.
5. ✅ `04-presence-ui` 의 lobby + in-room 패널이 [`presence-ui-migration-to-testscenesanyo`](../_archive/multiplayer-network/plans/2026-05-18-namae1128-presence-ui-migration-to-testscenesanyo.md) + [`presence-ui-lobby-migration-and-ux`](../_archive/multiplayer-network/plans/2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md) 두 plan 으로 TestSceneSanyo 마이그레이션 + UX 리디자인 + VR 키보드 통합 완료.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| 유저 인증 | `Active` | [01-user-auth.md](specs/01-user-auth.md) |
| 유저 데이터 영속화 | `Active` | [02-user-persistence.md](specs/02-user-persistence.md) |
| 멀티플레이어 룸 세션 | `Active` | [03-room-session.md](specs/03-room-session.md) |
| 접속 상태 UI | `Active` | [04-presence-ui.md](specs/04-presence-ui.md) |
| 룸 서버 매니저 | `Active` | [05-room-server-manager.md](specs/05-room-server-manager.md) |
| 빌드 타깃 분리 | `Active` | [06-build-targets.md](specs/06-build-targets.md) |

> 상태 값은 `Draft` / `Active` / `Done` / `Abandoned`

## Out of Scope

- 구체적인 멀티플레이 콘텐츠 동기화 — 아바타·손 동작, 악기 움직임, MIDI event 등 (별도 피처/브랜치에서 다룬다. 본 피처는 인증·룸 lifecycle·실행 환경·오케스트레이션·접속 상태까지로 책임 한정)
- 유저별 룸 상태 영속화 / 개인 룸 snapshot / copy-on-create / save-back 흐름 (default 씬 + 기배치 오브젝트만 사용하므로 룸별 상태 직렬화·복원이 필요 없음)
- 룸 내부 오브젝트 추가·이동·삭제 (씬에 미리 배치된 오브젝트만 사용)
- 텍스트 채팅
- 리플레이/녹화
- 관전 모드

## Open Questions

- _없음_

> 닫힌 OQ trace:
> - "접속 현황 UI는 default 씬 월드스페이스 패널 vs 별도 로비/테스트 씬" → default 씬 월드스페이스 패널 + 같은 씬 lobby/in-room 토글로 결정 ([`specs/04-presence-ui.md`](specs/04-presence-ui.md) What 섹션).
> - "1차 목표는 현재 인원/정원만으로 충분한지 vs 닉네임 목록까지 바로 필요한지" → 1차 출시는 `playerId` prefix 6자만 표시, nickname 채널은 실기기 검증 결과에 따른 후속 plan으로 분리 ([`plans/2026-05-16-namae1128-presence-ui-in-room-panel.md`](plans/2026-05-16-namae1128-presence-ui-in-room-panel.md) Context).

## Status

`Active`
