# Linux Dedicated Server 산출 + 단독 Dockerfile

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `In Progress`

## Goal

`RoomServerBoot` 진입 경로를 **Linux Dedicated Server 산출물**로 빌드하고, 그 산출물을 **단독 Docker 이미지**로 패키징한다. 빌드된 컨테이너에 Editor 클라이언트 1대가 합류하는 가장 짧은 수동 검증 경로를 확보한다.

## Context

- 본 plan은 `2026-05-01-namae1128-room-session-lifecycle.md`(Done)의 후속이다. 룸 라이프사이클 자체는 EditMode 12종 + Windows 자동화 5시나리오로 이미 검증되었다.
- 운영 배포는 AWS Linux 환경(Docker Compose)을 전제로 한다. 따라서 산출 OS는 **Linux 단일화**한다.
- 기존 `RoomServerBuildMenu.cs`의 Windows 빌드 메서드는 코드상 남아 있어도 무방하나, 본 plan에서는 acceptance criteria 대상에서 제외한다.
- 기존 `tools/run-room-lifecycle-automation.ps1`의 Windows 자동화 하네스는 더 이상 본 plan의 검증 수단이 아니다. 본 plan 종료 후에는 docker-compose 기반 통합 plan(`2026-05-07-namae1128-docker-compose-local-stack.md`)에서 자동화 회귀를 다시 구성한다.

## Approach

1. `RoomServerBuildMenu.BuildDedicatedServerLinux` 메뉴 + CLI 진입점을 검증하고, Linux Dedicated Server 산출물(`Builds/RoomAutomation/LinuxServer/RoomServer.x86_64` 등)이 실제로 생성되는지 확인한다.
2. Unity 6 Build Profiles에 `RoomServer-Linux` 자산을 추가해 에디터에서도 동일 빌드 구성을 재현할 수 있게 한다.
3. `docker/dedicated-server/Dockerfile`을 작성한다. 베이스는 슬림한 Linux 이미지(`ubuntu:22.04` 또는 `debian:bookworm-slim`) + Unity Linux 빌드 의존성. Unity Linux Server 산출 디렉터리를 컨테이너에 COPY하고 `ENTRYPOINT`로 `RoomServer.x86_64 -batchmode -nographics`를 실행한다.
4. `docker/dedicated-server/README.md`(또는 `docs/dev/dedicated-server-container.md`)에 빌드/실행 절차를 짧게 남긴다.
5. 수동 합류 검증 — 호스트에서 `docker run`으로 Dedicated Server 컨테이너를 띄운 뒤, Unity Editor에서 `RoomClientSmokeTest.unity`로 Photon Cloud 매치메이킹을 거쳐 같은 룸에 합류해 1명 입장이 서버 로그에 찍히는지 확인한다.

## Deliverables

- `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs` (Linux 메서드 동작 보장)
- Unity 6 Build Profiles 로컬 자산 1개: `RoomServer-Linux`
- `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64` (재현 가능한 산출물 경로)
- `docker/dedicated-server/Dockerfile`
- `docker/dedicated-server/.dockerignore`
- `docker/dedicated-server/README.md` 또는 동등한 짧은 절차 문서

## Acceptance Criteria

- [x] `[auto-hard]` `Assets/Multiplayer/Scripts/Editor/Murang.Multiplayer.Editor.asmdef`와 `RoomServerBuildMenu.cs`가 Editor 컴파일 에러 없이 로드된다.
- [ ] `[manual-hard]` Unity 6 Build Profiles에 `RoomServer-Linux` 자산이 존재한다.
- [ ] `[manual-hard]` `RoomServerBuildMenu.BuildDedicatedServerLinux` CLI 경로로 `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`가 생성된다.
- [ ] `[manual-hard]` `docker build -f docker/dedicated-server/Dockerfile .`가 성공하고 결과 이미지가 정상 기동된다.
- [ ] `[manual-hard]` 위 컨테이너가 떠 있는 상태에서 Unity Editor의 `RoomClientSmokeTest.unity`가 Photon Cloud 경로로 합류해 서버 로그에 입장이 기록된다.

## Out of Scope

- Windows Dedicated Server 산출/검증 (운영 OS와 무관해 Out of Scope로 강등)
- Windows 기반 자동화 하네스 유지·확장 (docker-compose 통합 plan에서 e2e 자동화를 다시 구성)
- Spring Boot, MariaDB와의 통합 (다음 plan)
- AWS 배포 (다음 plan)
- Photon AppId 환경 분리
- GitHub Actions 등 CI 빌드 자동화

## Notes

- Photon Fusion은 매치메이킹/Relay 트래픽이 outbound이므로 Dedicated Server 컨테이너에 별도의 인바운드 포트 매핑이 강제되지 않는다. 컨테이너 networking은 기본 bridge로 충분하다.
- Unity Linux Server 빌드는 Editor에 Linux Build Support 모듈이 설치되어 있어야 한다. 누락 시 빌드 메뉴가 즉시 실패한다.
- 기존 Windows 자동화 결과(`TestResults/room-lifecycle-automation/20260503-200059/summary.json`)는 룸 라이프사이클 plan의 검증 근거로 유효하지만, 본 plan에서는 인용만 하고 의존하지 않는다.

## Handoff

- 다음 plan은 [`2026-05-07-namae1128-docker-compose-local-stack.md`](2026-05-07-namae1128-docker-compose-local-stack.md)이다. 본 plan에서 만든 Dedicated Server 이미지가 docker-compose 서비스의 한 컴포넌트로 그대로 들어간다.
- `RoomServerBuildMenu.BuildDedicatedServerLinux`가 진입점이며, Dockerfile은 산출 디렉터리를 `COPY`하므로 빌드 산출 경로가 변경되면 Dockerfile도 함께 갱신해야 한다.
