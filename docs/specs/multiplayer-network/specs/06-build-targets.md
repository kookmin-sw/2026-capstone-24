# 빌드 타깃 분리

**Parent:** [`_index.md`](../_index.md)

## Why

VR 클라이언트와 헤드리스 룸 서버는 같은 코드베이스를 공유하지만 산출 요건이 완전히 다르다. 클라이언트는 VR 입력·렌더링·UI·오디오가 필요하고, 룸 서버는 권위 시뮬레이션과 동기화만 필요해 위 자원을 모두 제거해야 한다. 두 빌드의 범위와 제외 항목을 한 곳에 정의해두지 않으면 빌드마다 무엇이 포함/제외되는지가 흔들리고 두 빌드 간 일관성(특히 네트워크로 동기화되는 자산의 식별자 일치)이 깨진다.

## What

같은 Unity 프로젝트로부터 산출되는 두 빌드 타깃의 범위와 제외 항목을 정의한다.

- **Quest Client 빌드** (Android, Quest용): XR/입력/UI/오디오/UX 자산 포함. NetworkRunner는 클라이언트 모드로 구동한다.
- **Headless Server 빌드** (Linux 서버용): XR/입력/UI/오디오 자산 제외, 그래픽 디바이스 비활성. NetworkRunner는 서버 모드로 구동한다. 권위 시뮬레이션·판정·동기화에 필요한 자산만 포함한다.
- 두 빌드는 동일한 prefab과 동일한 NetworkObject 식별자를 공유한다. 네트워크로 동기화되는 모든 객체의 식별자가 두 빌드 간 일치해야 동기화가 가능하다.

## Behavior

- **Given** Quest Client 빌드 산출을 요청할 때
  **When** 빌드가 완료되면
  **Then** 산출물에 XR·입력·UI·오디오 자산이 포함되어 있고, NetworkRunner는 클라이언트 모드로 시작되도록 구성되어 있다.

- **Given** Headless Server 빌드 산출을 요청할 때
  **When** 빌드가 완료되면
  **Then** 산출물에서 XR·입력·UI·오디오 자산이 제외되어 있고, 그래픽 디바이스가 비활성으로 구성되어 있으며, NetworkRunner는 서버 모드로 시작되도록 구성되어 있다.

- **Given** 두 빌드가 산출되었을 때
  **When** 네트워크로 동기화되는 prefab의 식별자를 비교하면
  **Then** 두 빌드의 식별자가 정확히 일치한다.

## Out of Scope

- 컨테이너 이미지화·docker 빌드 (plan/ops 영역)
- 컨테이너 레지스트리·배포 채널·서명 (plan/ops 영역)
- 빌드 자동화 CI/CD (별 plan)
- 빌드된 산출물의 배치·실행 관리 ([`05-room-orchestration.md`](05-room-orchestration.md) 책임)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [ ] Headless Server 빌드에서 제거할 패키지 목록 확정 (XR Plug-in Management, Input System, Audio 등)
- [ ] prefab/asset 공유 일관성을 강제할 위치 (asmdef 경계, 빌드 검증 스크립트, CI 가드)
- [ ] Quest Client 빌드의 ARM64 / ARMv7 산출 정책
