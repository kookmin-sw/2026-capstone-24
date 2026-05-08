# Dedicated Server Docker 이미지

Photon Fusion 기반 RoomServer를 Linux 컨테이너로 실행하기 위한 Dockerfile과 빌드 절차.

## 사전 조건

- Unity Editor에 **Linux Build Support (IL2CPP)** 모듈이 설치되어 있어야 한다.
- Docker가 설치된 Linux/macOS/WSL2 환경에서 빌드한다.

---

## 1단계 — Linux 서버 바이너리 산출

Unity Editor 메뉴 또는 CLI 중 하나를 선택한다.

### 메뉴 방식

```
Tools > Multiplayer > Build Dedicated Server (Linux)
```

### CLI 방식

```bash
# 프로젝트 루트에서 실행
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" \
  -batchmode \
  -projectPath . \
  -executeMethod Murang.Multiplayer.Editor.RoomServerBuildMenu.BuildDedicatedServerLinux \
  -quit \
  -logFile -
```

산출 경로: `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`

---

## 2단계 — Docker 이미지 빌드

프로젝트 루트에서 실행한다. `.dockerignore`가 Unity 소스 전체를 차단하고 산출 디렉터리만 이미지에 포함시킨다.

```bash
docker build \
  -f docker/dedicated-server/Dockerfile \
  -t room-server:latest \
  .
```

---

## 3단계 — 컨테이너 실행

```bash
docker run --rm room-server:latest
```

### 환경변수

| 변수 | 설명 | 기본값 |
|---|---|---|
| `PHOTON_APP_ID` | Photon Fusion App ID (Custom Auth 없이 테스트 시) | — |

> Photon Fusion은 매치메이킹 트래픽이 outbound이므로 별도의 인바운드 포트 매핑이 필요 없다. 기본 bridge 네트워크로 충분하다.

---

## 다음 단계

`docker-compose` 기반 로컬 통합 스택(spring + mariadb + dedicated-server)은
[`2026-05-07-namae1128-docker-compose-local-stack.md`](../../docs/specs/multiplayer-network/plans/2026-05-07-namae1128-docker-compose-local-stack.md)에서 다룬다.
