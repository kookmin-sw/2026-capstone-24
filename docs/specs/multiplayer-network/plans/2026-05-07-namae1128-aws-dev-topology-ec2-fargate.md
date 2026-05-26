# AWS dev 토폴로지 (EC2 Spring + MariaDB, ECS Fargate room-server)

**Linked Spec:** [`05-room-server-manager.md`](../specs/05-room-server-manager.md)
**Status:** `Ready (2026-05-26 reopen) — 2026-05-25 Done 처리는 mock 모드 검증만으로 잘못 닫은 것. manual-hard 8건 중 #1·#3·#4·#5 (control plane, 룸 row, Fargate ready callback, Quest 합류) 는 mock 으로 통과 가능 → 통과 확인. #2 (동일 metaAccountId 두 번째 로그인 동일 playerId) 는 mock 으로는 의미 있는 검증 불가, real 모드 필수로 미검증. 운영 자계 3건 (CloudWatch custom metric / MariaDB inbound 차단 / mysqldump cron) 은 발표 범위 밖으로 deferred 유지. 후속 'nickname input + real Meta 통합 시도' plan 사이클에서 AC#2 검증 예정.`

## Goal

단일 AWS dev 환경에서 EC2는 Spring Boot App(+ 내부 `RoomServerManager`)과 MariaDB를 담당하고, 실제 room-server는 ECS Fargate task로 on-demand 실행되는 토폴로지를 구축한다. 실 Quest 클라이언트 또는 Editor 클라이언트가 Spring API를 통해 인증·룸 생성·입장 요청을 보내고, Spring 내부 `RoomServerManager`가 AWS API로 room-server task를 실행·조회·종료하며, 유저는 Fargate room-server의 공개 endpoint로 룸 세션에 합류할 수 있게 만든다.

## Context

- 선행 plan [`2026-05-07-namae1128-docker-compose-local-stack.md`](../../_archive/multiplayer-network/plans/2026-05-07-namae1128-docker-compose-local-stack.md)에서 local 통합 검증용 `spring + mariadb + dedicated-server` compose 스택이 동작함이 검증된다.
- 이제 local 검증용 compose와 AWS dev topology의 역할을 분리한다. local에서는 `docker-compose`를 유지하고, AWS dev에서는 control plane만 EC2에 두고 room-server만 ECS Fargate로 분리한다.
- Spring Boot App 안에는 `Client-facing API`, `Auth/User/Room Service`, `RoomServerManager`가 함께 포함된다. `RoomServerManager`는 `RunTask`, `DescribeTasks`, `StopTask`를 호출하고 `room_server_instances` 상태를 관리한다.
- MariaDB는 같은 EC2 안의 별도 컨테이너로 실행하되 volume으로 영속성을 유지한다. dev 단계에서는 RDS를 도입하지 않는다.
- 본 plan은 dev topology만 다룬다. single VPC, 단일 public subnet, 단일 EC2, on-demand Fargate room-server를 전제로 한다.
- CloudWatch 기반 최소 관측성(logs + custom metrics + alarms)을 함께 도입한다.

## Decisions

- **EC2 역할**: EC2 1대가 Spring Boot App 컨테이너와 MariaDB 컨테이너를 함께 호스팅한다.
- **인스턴스 타입**: t3.small (vCPU 2, 메모리 2GB)부터 시작. Spring + MariaDB control plane 용도로 충분한 최소치로 본다.
- **OS**: Ubuntu 24.04 LTS.
- **Spring 배치 방식**: Spring Boot App과 MariaDB는 각각 독립 컨테이너로 만들고, EC2에서는 control-plane 전용 `docker-compose`로 함께 기동한다.
- **RoomServerManager 위치**: `RoomServerManager`는 별도 서비스가 아니라 Spring Boot App 내부 모듈이다.
- **room-server 실행 방식**: room-server는 ECS Fargate task를 on-demand로 실행한다. task 이미지는 ECR에서 pull한다.
- **네트워크 단순화**: dev 단계에서는 EC2와 Fargate task를 같은 VPC의 public subnet에 둔다. task에는 public IP를 부여해 외부 클라이언트가 `public_ip:game_port/udp`로 접근할 수 있게 한다.
- **내부 통신**: Fargate task는 EC2 private endpoint로 ready callback/heartbeat를 보낼 수 있어야 한다.
- **DB 영속성**: MariaDB는 EC2 로컬 volume으로 유지하고 외부에는 공개하지 않는다.
- **Secrets**: `.env`는 EC2의 로컬 파일로 관리한다. AWS Secrets Manager 도입은 후속 단계.
- **DB 백업**: 일 1회 cron으로 `mysqldump` 결과를 EC2 EBS의 별도 디렉터리에 보관. S3 동기화는 후속.
- **관측성**: 로그는 CloudWatch Logs, 메트릭은 CloudWatch custom metrics를 사용한다. Grafana는 도입하지 않는다.
- **배포 방식**: control plane은 수동 `git pull && docker compose up -d`, room-server 이미지는 수동 빌드 후 ECR push + ECS task definition 갱신 방식으로 시작한다. CI/CD 자동화는 후속.

## Approach

1. **EC2 control-plane compose 분리** — EC2에서 기동할 `spring` + `mariadb` 전용 compose 파일을 정의한다.
   - Spring Boot App 컨테이너 안에 `Client-facing API`, `Auth/User/Room Service`, `RoomServerManager`가 포함된다.
   - MariaDB는 별도 컨테이너와 persistent volume을 사용한다.
   - room-server는 이 compose에 포함하지 않는다.
2. **VPC / Subnet / Security Group 설계** — dev topology를 아래처럼 고정한다.
   - `VPC`
   - `Public Subnet`
   - `EC2`
   - `ECS Fargate room-server task`
   - `Internet Gateway`
   - 외부 유저는 Spring public endpoint와 Fargate room-server `public_ip:game_port/udp`에 접근 가능해야 한다.
   - Fargate task는 EC2 private endpoint로 접근 가능해야 한다.
   - MariaDB는 외부 비공개여야 한다.
3. **DB 스키마 확장** — 최소한 아래 테이블/컬럼을 Spring migration으로 정의한다.
   - `rooms`
   - `room_id`
   - `owner_user_id`
   - `photon_session_name`
   - `max_players`
   - `created_at`
   - `closed_at`
   - `room_server_instances`
   - `id`
   - `room_id`
   - `status`
   - `ecs_cluster_arn`
   - `ecs_task_arn`
   - `task_public_ip`
   - `game_port`
   - `created_at`
   - `ready_at`
   - `last_heartbeat_at`
   - `terminated_at`
   - `users`는 기존 영속 스키마를 유지한다.
4. **RoomServerManager ECS 연동** — Spring 내부 `RoomServerManager`가 다음 책임을 수행한다.
   - room 생성 시 `rooms` row 생성
   - `room_server_instances` row를 `PROVISIONING`으로 생성
   - `RunTask` 호출 후 `ecs_cluster_arn`, `ecs_task_arn` 갱신
   - `DescribeTasks` 기반 reconciliation
   - ready callback 수신 시 `READY`, `ready_at` 갱신
   - heartbeat 수신 시 `last_heartbeat_at` 갱신
   - room 종료 시 `StopTask`, `TERMINATED`, `terminated_at`, `rooms.closed_at` 갱신
5. **ECR + Fargate task definition 준비** — room-server 이미지를 ECR에 push하고 Fargate task definition에서 이를 참조하게 한다.
   - task에 public IP 부여
   - `game_port`
   - ready callback URL
   - heartbeat URL
   - `roomRuntimeVersion`
   를 환경변수 또는 인자로 주입한다. (default 씬 모델이므로 snapshot 관련 환경변수는 주입하지 않는다.)
6. **CloudWatch 최소 관측성** — control plane과 room-server 모두 CloudWatch로 관측한다.
   - Spring 로그
   - room-server task 로그
   - custom metrics: `active_room_count`, `room_provision_latency`, `room_ready_latency`, `unhealthy_termination_count`, `admission_rejection_count`, `run_task_failure_count`
   - 기본 alarm: `RunTask 실패`, `ready timeout`, `UNHEALTHY 급증`, `capacity 부족`
7. **Smoke 절차** — Quest 빌드 또는 Editor 빌드가 EC2 Spring endpoint를 사용해 인증하고, 룸 생성 시 Fargate task가 실행되어 ready callback 후 room join이 가능한지 검증한다.

## Deliverables

- `docker-compose.ec2-dev.yml` 또는 동등 — EC2 control plane용 `spring + mariadb` 정의
- `backend/src/main/resources/application-aws-dev.yml` 또는 동등 — AWS dev 환경설정
- Spring migration — `rooms`, `room_server_instances` 스키마 생성
- `docs/ops/aws-dev-topology.md` — VPC / EC2 / Fargate / DB / SG 토폴로지 문서
- `docs/ops/aws-dev-runbook.md` — EC2 부트스트랩, ECR push, task definition 갱신, smoke 절차
- `docs/ops/cloudwatch-minimum-observability.md` — 로그/메트릭/알람 기준
- `tools/deploy-ec2-control-plane.{ps1,sh}` 또는 동등 — EC2 control plane 기동 래퍼
- `tools/push-room-server-image.{ps1,sh}` 또는 동등 — room-server 이미지 push 및 task definition 갱신 래퍼

## Acceptance Criteria

- [ ] `[auto-hard]` `docker compose -f docker-compose.ec2-dev.yml config`가 에러 없이 통과한다.
- [ ] `[manual-hard]` 새 EC2 인스턴스에서 `spring` + `mariadb` control plane 컨테이너가 기동되고, Spring public endpoint가 외부에서 도달 가능하다.
- [ ] `[manual-hard]` 동일 `metaAccountId`로 두 번째 로그인 시 같은 `playerId`가 반환된다 (DB 영속성 확인).
- [ ] `[manual-hard]` 룸 생성 요청 시 `rooms` row와 `room_server_instances` row가 생성되고, `RunTask` 호출 결과의 `ecs_task_arn`이 저장된다.
- [ ] `[manual-hard]` Fargate room-server task가 EC2 private endpoint로 ready callback을 보내고 `room_server_instances.status`가 `READY`로 전이된다.
- [ ] `[manual-hard]` Quest 빌드 또는 Editor 빌드가 Spring endpoint를 통해 룸 생성 후 Fargate room-server `public_ip:game_port/udp`에 합류하고, 서버 로그에 입장이 기록된다.
- [ ] `[manual-hard]` 마지막 유저 퇴장 또는 명시적 종료 시 `StopTask`가 호출되고 `room_server_instances.terminated_at`, `rooms.closed_at`가 기록된다.
- [ ] `[manual-hard]` CloudWatch Logs에서 Spring 로그와 room-server task 로그를 확인할 수 있고, 최소 1개의 custom metric(`active_room_count` 또는 `room_ready_latency`)이 발행된다.
- [ ] `[manual-hard]` MariaDB는 인터넷에서 직접 접근되지 않는다.
- [ ] `[manual-hard]` 일 1회 `mysqldump` 백업이 cron으로 실행되고 결과 파일이 생성된다.

## Out of Scope

- 다중 EC2/오토스케일 그룹
- ECS Fargate를 넘는 EKS/Agones/Kubernetes 마이그레이션
- AWS Secrets Manager·Parameter Store 통합
- S3 백업·복구 자동화
- CI/CD 파이프라인(GitHub Actions 등)
- RoomServerManager의 별도 서비스 분리
- 다중 리전 / 고급 스케줄링 / 매치메이커 분리
- 실 Meta SDK verifier 활성화
- Photon AppId의 dev/prod 환경 분리

## Notes

- t3.small은 control plane 용도로는 충분하지만, build 작업까지 같은 인스턴스에서 수행하면 병목이 날 수 있다. 가능하면 빌드는 별도 환경에서 하고 EC2에는 실행 산출만 배포한다.
- Fargate cold start와 Unity 부팅 시간 때문에 room create에서 `READY`까지 수 초~수십 초 지연이 발생할 수 있다. 클라이언트는 waiting 상태를 표현할 수 있어야 한다.
- room-server를 Fargate에서 띄우려면 이미지 레지스트리가 필요하므로 ECR은 사실상 필수다. Spring/MariaDB control plane과 달리 room-server task는 로컬 이미지로 바로 기동할 수 없다.
- Photon AppId가 dev/prod에 동일하면 dev 클라이언트가 prod 룸 목록에 노출될 수 있다. 운영 시점에 AppId 분리가 필수가 되며, 이는 별도 후속 plan에서 다룬다.
- DB 백업 파일이 EC2 로컬에만 있으면 인스턴스 손실 시 데이터 회복이 불가능하다. 운영 안정화 단계에서 S3 동기화를 추가해야 한다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. 후속 작업이 의존하는 운영 산출:
- EC2 control plane의 `spring + mariadb` 기동 정의
- `RoomServerManager`의 ECS RunTask / DescribeTasks / StopTask 연동 규약
- `rooms`, `room_server_instances` 스키마와 상태 전이 규약
- CloudWatch 로그/메트릭/알람 기준
-->
