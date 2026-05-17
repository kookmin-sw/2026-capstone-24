# 빌드 타깃 분리

**Parent:** [`_index.md`](../_index.md)

## Why

VR 클라이언트와 헤드리스 룸 서버는 같은 코드베이스를 공유하지만 산출 요건이 다르다. 클라이언트는 XR 입력·렌더링·UI·오디오가 필요하고, 룸 서버는 권위 시뮬레이션과 동기화만 필요하다. 두 빌드의 범위와 제외 항목을 한 곳에 정의하지 않으면 어떤 자산과 런타임 계약이 어느 빌드에 포함되는지 흔들리고, 나중에 local Docker / ECS / Kubernetes 어느 환경에서 실행하든 같은 룸 서버 계약을 재현하기 어려워진다.

## What

같은 Unity 프로젝트로부터 산출되는 두 빌드 타깃의 범위와 공통 계약을 정의한다.

- **Quest Client 빌드** (Android, Quest용): XR/입력/UI/오디오/UX 자산 포함. NetworkRunner는 클라이언트 모드로 구동한다.
- **Headless Server 빌드** (Linux 서버용): XR/입력/UI/오디오 자산 제외, 그래픽 디바이스 비활성. NetworkRunner는 서버 모드로 구동한다. 권위 시뮬레이션·판정·동기화에 필요한 자산만 포함한다.
- 두 빌드는 동일한 default 씬([`../decisions/01-default-scene.md`](../decisions/01-default-scene.md)), 동일한 prefab, 동일한 NetworkObject 식별자를 공유한다. 네트워크로 동기화되는 모든 객체의 식별자가 두 빌드 간 일치해야 한다.
- 클라이언트와 room server는 네트워크/콘텐츠 계약을 대표하는 `clientCompatibilityVersion` 또는 동등한 호환 버전 개념을 공유해야 한다. 이 값은 prefab 식별자, NetworkBehaviour/RPC, 룸 입장 계약 등 네트워크 상호운용성을 대표한다.
- room server build는 room instance 기동 시 사용할 `roomRuntimeVersion`을 가진다.
- Spring 내부 `RoomServerManager`는 room provisioning 요청에 필요한 `roomRuntimeVersion`을 포함하고, room server는 ready callback에 자신이 실행 중인 실제 버전을 보고한다. 요청 버전과 실제 버전이 호환되지 않으면 room instance는 admission을 열지 않고 실패 처리된다.
- 로컬 Docker, ECS, 향후 Kubernetes 구현체는 모두 같은 room server startup contract(환경변수, CLI 인자, ready/heartbeat 계약)를 사용해야 한다.

## Behavior

- **Given** Quest Client 빌드 산출을 요청할 때
  **When** 빌드가 완료되면
  **Then** 산출물에 XR·입력·UI·오디오 자산이 포함되어 있고, NetworkRunner는 클라이언트 모드로 시작되도록 구성되어 있다.

- **Given** Headless Server 빌드 산출을 요청할 때
  **When** 빌드가 완료되면
  **Then** 산출물에서 XR·입력·UI·오디오 자산이 제외되어 있고, 그래픽 디바이스가 비활성으로 구성되어 있으며, NetworkRunner는 서버 모드로 시작되도록 구성되어 있다.

- **Given** 두 빌드가 산출되었을 때
  **When** 네트워크로 동기화되는 prefab과 호환 버전 계약을 비교하면
  **Then** client/server가 같은 `clientCompatibilityVersion` 정책을 만족한다.

- **Given** `RoomServerManager`가 특정 `roomRuntimeVersion`으로 room instance를 기동했을 때
  **When** room server ready callback이 다른 런타임 버전을 보고하면
  **Then** 해당 room instance는 admission을 열지 않고 실패 처리된다.

## Out of Scope

- 컨테이너 이미지화·레지스트리·배포 채널
- CI/CD 파이프라인과 자동 배포
- ECS task definition, Kubernetes manifest 같은 실행 플랫폼별 리소스 템플릿
- 빌드된 산출물의 배치·실행 관리 ([`05-room-server-manager.md`](05-room-server-manager.md) 책임)
- 유저별 룸 상태 직렬화 형식·`snapshotSchemaVersion`·호환성 검사 (default 씬 모델이므로 본 피처 전체 Out of Scope)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [ ] Headless Server 빌드에서 제거할 패키지/시스템 최종 목록
- [ ] `roomRuntimeVersion`의 표현 방식 (semantic version / build hash / image tag)
- [ ] prefab/network schema 호환성 검증을 어디서 강제할지 (빌드 단계 / CI / 런타임 가드)
- [ ] Quest Client 빌드의 ARM64 / ARMv7 산출 정책
