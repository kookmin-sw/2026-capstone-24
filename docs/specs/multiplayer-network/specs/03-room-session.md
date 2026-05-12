# 멀티플레이어 룸 세션

**Parent:** [`_index.md`](../_index.md)

## Why

여러 유저가 같은 VR 공간을 공유하려면 유저들을 하나의 세션으로 묶는 룸 개념이 필요하다. 룸이 없으면 각자 독립된 공간에서만 존재하게 되어 협주가 불가능하다.

## What

Photon Fusion을 통해 룸을 생성하거나 기존 룸에 입장·퇴장할 수 있는 세션 관리 기능을 제공한다. 여러 유저가 같은 룸에 동시 접속해 공유 공간을 형성한다. 룸 권위는 전용 서버 인스턴스가 가지며, 클라이언트는 백엔드를 경유해 룸을 생성하고 룸 세션에 직접 연결해 입장한다. 룸 생성 시 비밀번호 설정 여부를 선택할 수 있으며, 모든 룸은 룸 목록에 노출되고 비밀번호가 설정된 룸은 잠금 상태로 표시된다.

룸 서버는 별도 관리 컴포넌트가 lifecycle을 관리하는 풀로 운영된다. 각 룸 서버는 `IDLE → CLAIMED → RUNNING` 상태를 가지며, 룸이 비면 프로세스를 종료하는 대신 IDLE로 복귀해 재사용된다. 클라이언트가 새 룸 생성을 요청하면 백엔드가 요청 유저의 개인 룸 snapshot(DB)·룸 이름·(선택)비밀번호를 모아 별도 관리 컴포넌트에 전달하고, 별도 관리 컴포넌트가 풀에서 IDLE 인스턴스 하나를 claim해 전달받은 정보를 바탕으로 RUNNING 상태로 구성한 뒤 클라이언트는 해당 인스턴스의 세션에 입장한다. 백엔드는 서비스 API와 룸 메타데이터(목록·상태) DB 관리만 담당하고, 풀/컨테이너 오케스트레이션 권한은 별도 관리 컴포넌트에 둔다. 클라이언트와 룸 서버는 같은 prefab을 공유하지만 별도 빌드로 산출되며, 클라이언트 빌드는 클라이언트 모드로, 룸 서버 빌드는 서버 모드로 NetworkRunner를 구동한다.

세션 내부 네트워크 식별자인 `PlayerRef`는 룸 실행 중에만 유효한 전송 식별자로 취급한다. 애플리케이션 레벨의 영구 유저 식별은 백엔드 `playerId`를 기준으로 유지하고, Photon Custom Auth의 `UserId`에도 이 값을 전달한다. `metaAccountId`는 Meta 같은 로그인 제공자 식별자, `nickname`은 표시용 값으로만 다룬다.

## Behavior

- **Given** 로그인된 유저가
  **When** 새 룸 생성을 요청하면
  **Then** 룸이 생성되고 해당 유저가 첫 입장자로 합류한다.

- **Given** 로그인된 유저가
  **When** 기존 룸에 입장을 요청하면
  **Then** 같은 룸에 이미 있는 유저들과 동일한 세션에 합류한다.

- **Given** 룸에 접속 중인 유저가
  **When** 퇴장하면
  **Then** 해당 유저는 세션에서 제거되고 다른 유저에게 반영된다.

- **Given** 룸의 마지막 유저가
  **When** 퇴장하면
  **Then** 룸이 자동으로 소멸한다.

- **Given** 룸에 이미 8명이 접속해 있을 때
  **When** 추가 유저가 입장을 요청하면
  **Then** 입장이 거부되고 거부 사유가 클라이언트에 전달된다.

- **Given** 로그인된 유저가 룸 생성 시 비밀번호 옵션을 설정했을 때
  **When** 룸 생성을 요청하면
  **Then** 비밀번호가 설정된 룸이 만들어지고 룸 목록에 잠금 상태로 표시된다.

- **Given** 비밀번호 룸에 입장하려는 유저가
  **When** 올바른 비밀번호를 제시하면
  **Then** 룸에 합류한다.

- **Given** 비밀번호 룸에 입장하려는 유저가
  **When** 잘못된 비밀번호를 제시하면
  **Then** 입장이 거부되고 비밀번호 불일치 사유가 클라이언트에 전달된다.

## Out of Scope

- 아바타·손 동작 실시간 동기화
- 악기 연주 동기화
- 음성 채팅
- 초대 링크
- 초대 전용 룸 (룸 코드 외 별도 초대 메커니즘)
- 룸 비밀번호 변경·재설정 (룸 생성 시점에만 설정 가능)
- 호스트(클라이언트) 권위 마이그레이션
- 룸 서버 풀의 동적 스케일링·K8s 오케스트레이션 (시연 이후 후속 단계)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-28 | Photon Fusion SDK 패키지 준비 | `Done` | [2026-04-28-namae1128-photon-fusion-import.md](../../_archive/multiplayer-network/plans/2026-04-28-namae1128-photon-fusion-import.md) |
| 2026-05-01 | 룸 세션 라이프사이클 (비밀번호 옵션 포함) | `Done` | [2026-05-01-namae1128-room-session-lifecycle.md](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-room-session-lifecycle.md) |
| 2026-05-01 | Linux Dedicated Server 산출 + 단독 Dockerfile | `Done` | [2026-05-01-namae1128-dedicated-server-build-pipeline.md](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-dedicated-server-build-pipeline.md) |
| 2026-05-01 | 공개 룸 목록 조회 (잠금 표시 포함) | `Done` | [2026-05-01-namae1128-room-list-query.md](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-room-list-query.md) |
| 2026-05-07 | docker-compose 로컬 통합 스택 (spring + mariadb + dedicated-server) | `Done` | [2026-05-07-namae1128-docker-compose-local-stack.md](../../_archive/multiplayer-network/plans/2026-05-07-namae1128-docker-compose-local-stack.md) |
| 2026-05-07 | AWS EC2 docker-compose 배포 | `Ready` | [2026-05-07-namae1128-aws-ec2-compose-deployment.md](../plans/2026-05-07-namae1128-aws-ec2-compose-deployment.md) |
| 2026-05-08 | tools/run-stack-smoke 5시나리오 자동화 (docker-compose 기반) | `Ready` | [2026-05-08-namae1128-stack-smoke-automation.md](../plans/2026-05-08-namae1128-stack-smoke-automation.md) |
| 2026-05-09 | dedicated-server 빌드 시 Standalone OpenXR loader 임시 토글 | `Done` | [2026-05-09-namae1128-dedicated-server-build-openxr-toggle.md](../../_archive/multiplayer-network/plans/2026-05-09-namae1128-dedicated-server-build-openxr-toggle.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용.

## Open Questions

- _없음_
