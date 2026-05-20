# Unity Linux Dedicated Server — RoomServer
#
# 전제: docker build 실행 전 RoomServerBuildMenu.BuildDedicatedServerLinux로
#       Builds/RoomAutomation/LinuxServer/ 를 먼저 산출해야 한다.
#
# 빌드:
#   docker build -f docker/dedicated-server/Dockerfile \
#                -t room-server:latest .
#
# 실행:
#   docker run --rm room-server:latest

FROM ubuntu:22.04

# Unity Linux Server 런타임 의존성
# libc6, libstdc++6 은 ubuntu:22.04 기본 포함이고, libdl 은 libc6 에 통합되어 별도 패키지 없음.
# Photon Fusion outbound TLS 와 Unity Linux 런타임에 필요한 최소 항목만 설치.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates \
      libglib2.0-0 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# 빌드 산출 디렉터리만 복사한다.
# .dockerignore 가 Unity 프로젝트 루트의 불필요한 파일을 차단한다.
COPY Builds/RoomAutomation/LinuxServer/ .

# 디버그 심볼은 배포 이미지에서 제외하고 실행 권한 부여
RUN rm -rf ./*_BurstDebugInformation_DoNotShip \
 && chmod +x ./RoomServer.x86_64

# -logFile - 로 Unity Player.log 를 stdout 으로 강제. ECS Fargate awslogs driver
# 가 stdout 만 수집하므로 이게 없으면 Debug.Log 가 CloudWatch 에 안 들어간다.
ENTRYPOINT ["./RoomServer.x86_64", "-batchmode", "-nographics", "-logFile", "-"]
