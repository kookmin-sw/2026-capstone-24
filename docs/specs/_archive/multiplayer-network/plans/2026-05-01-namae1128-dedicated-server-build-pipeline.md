# Linux Dedicated Server 산출 + 단독 Dockerfile

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Done`

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
- [x] `[manual-hard]` Unity 6 Build Profiles에 `RoomServer-Linux` 자산이 존재한다.
- [x] `[manual-hard]` `RoomServerBuildMenu.BuildDedicatedServerLinux` CLI 경로로 `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`가 생성된다.
- [x] `[manual-hard]` `docker build -f docker/dedicated-server/Dockerfile .`가 성공하고 결과 이미지가 정상 기동된다.

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
- 2026-05-08: 초안 Dockerfile에 `libdl-dev`가 들어 있었으나 Ubuntu 22.04에는 존재하지 않는 패키지라 제거. `libdl`은 `libc6`에 통합되어 별도 설치 불필요. 최종 의존성은 `ca-certificates`, `libglib2.0-0` 두 개로 좁혔다.
- 2026-05-08: `*_BurstDebugInformation_DoNotShip/` 디렉터리는 배포 이미지에서 제외하도록 Dockerfile RUN 단계에서 삭제하는 라인을 추가했다.
- 2026-05-08: `docker run --rm room-server:latest` 실행 결과 컨테이너가 Unity LinuxServer 모드로 부팅되어 Photon Cloud `kr` 리전 마스터 서버까지 연결 확인. `OpenXR DllNotFoundException`은 Linux Server 빌드에 UnityOpenXR 네이티브 plugin이 포함되지 않아 발생하는 무해한 메시지로 확인 (서버 동작에 영향 없음).
- 2026-05-08: 원래 acceptance criteria #5(Editor `RoomClientSmokeTest` 합류 smoke)는 클라이언트가 Spring 백엔드 인증을 선행 호출하는 구조라 본 plan 단독으로는 검증 불가. 동일 항목이 다음 plan(`docker-compose 로컬 통합`)에 이미 존재하므로 본 plan에서는 제거하고 그 plan의 manual-hard로 통합했다.

## Handoff

다음 plan(`docker-compose 로컬 통합 스택`)이 의존하는 산출과 제약:

- **Dockerfile 진입점**: `docker/dedicated-server/Dockerfile`. 베이스 `ubuntu:22.04`, build context는 워크트리(또는 프로젝트) 루트, `COPY Builds/RoomAutomation/LinuxServer/ .` + `ENTRYPOINT ["./RoomServer.x86_64", "-batchmode", "-nographics"]`.
- **빌드 산출 사전 조건**: `docker build` 실행 전 `RoomServerBuildMenu.BuildDedicatedServerLinux`로 `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`(launcher) + `UnityPlayer.so` + `RoomServer_Data/`를 먼저 산출해야 한다. 산출 경로는 Unity Editor가 열린 *프로젝트 루트* 기준이라 워크트리에서 빌드하지 않은 경우 산출물을 워크트리로 복사해야 docker build가 동작한다.
- **이미지 태그 규약**: 기본값 `room-server:latest`. docker-compose 서비스명을 동일하게 두거나 `build:` 블록으로 워크트리 Dockerfile을 가리킬 수 있다.
- **Photon AppId 주입 방식**: 현재는 `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`에 동봉되어 컨테이너 이미지에 포함된다. 별도 환경변수 주입은 불필요하지만, 운영 환경 분리(dev/prod AppId)가 필요해지면 후속 plan에서 외부 주입 방식으로 변경 필요.
- **네트워크 가정**: Photon Cloud Relay가 outbound라 Dedicated Server 컨테이너에 인바운드 포트 매핑은 불필요. 기본 bridge 네트워크로 동작.
- **비포함 산출**: `*_BurstDebugInformation_DoNotShip/`는 Dockerfile RUN 단계에서 삭제하므로 이미지에 들어가지 않는다.
- **클라이언트 합류 smoke 위임**: 본 plan에서 검증 불가했던 "Editor 클라이언트 합류" 항목은 다음 plan AC `[manual-hard] Editor의 RoomClientSmokeTest.unity가 Photon Cloud 경유로 컨테이너 Dedicated Server에 합류해 서버 로그에 입장이 기록된다`로 통합되었다.
