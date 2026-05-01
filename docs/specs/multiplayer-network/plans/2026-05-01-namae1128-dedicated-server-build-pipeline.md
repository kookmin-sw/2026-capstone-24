# Dedicated Server 빌드 파이프라인 (Windows 로컬 + Linux 산출)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Ready`

## Goal

`RoomServerBoot` 기반 전용 서버 진입 경로를 Windows와 Linux Dedicated Server 산출물로 빌드할 수 있게 만들고, Windows 로컬 산출물 1대에 Editor 클라이언트 1대가 합류하는 가장 짧은 수동 검증 경로를 확립한다.

## Context

`docs/specs/multiplayer-network/specs/03-room-session.md`는 룸 권위가 전용 서버 인스턴스에 있고, 클라이언트는 그 서버에 합류해야 한다는 전제를 이미 담고 있다. 현재 작업 트리에는 그 전제를 뒷받침하는 런타임 자산이 상당 부분 준비되어 있다.

- `Assets/Multiplayer/Scenes/RoomServerBoot.unity`가 존재한다.
- `Assets/Multiplayer/Scripts/Room/Server/RoomServerBootstrap.cs`와 `RoomServerConfig.cs`가 존재한다.
- `Assets/Multiplayer/Resources/RoomServerConfig.asset`가 존재한다.
- Editor 클라이언트 측 최소 검증 경로로 사용할 `Assets/Multiplayer/Scenes/RoomClientSmokeTest.unity`와 `RoomClientSmokeProbe.cs`가 존재한다.

반면, 이 서버 진입 경로를 실제 Dedicated Server 산출물로 묶는 빌드 파이프라인은 아직 없다. `Assets/Multiplayer/Scripts/Editor/` 폴더는 비어 있고, 전용 서버용 Build Profile이나 이를 재현 가능한 방식으로 호출하는 Editor 메뉴/CLI 진입점도 없다. 현재 `ProjectSettings/EditorBuildSettings.asset`에는 `RoomServerBoot`와 `Assets/Scenes/SampleScene.unity`가 함께 들어 있지만, 이 전역 설정만으로는 "서버 전용 씬 하나만 포함하는 플랫폼별 Dedicated Server 빌드"를 안정적으로 재현하기 어렵다.

이 plan은 새 네트워크 기능을 추가하는 것이 아니라, 이미 존재하는 서버/클라이언트 최소 흐름을 실제 Windows/Linux 전용 서버 산출물로 묶는 빌드 레이어를 추가하는 후속 작업이다. 핵심 목적은 `2026-05-01-namae1128-room-session-lifecycle.md`의 첫 번째 `[manual-hard]` 검증 항목인 "같은 세션 합류"를 Windows 서버 산출물 기준으로 통과시킬 수 있게 만드는 것이다.

### 핵심 결정

- **연결 spec**: 새 sub-spec을 만들지 않고 `03-room-session.md`에 연결한다. Dedicated Server 빌드는 룸 권위 인스턴스를 실제 산출물로 만드는 구현 상세이기 때문이다.
- **Build Profile 이름**: Unity 6 Build Profiles에 `RoomServer-Windows`, `RoomServer-Linux` 두 개를 만든다.
- **서버 씬 범위**: 두 프로파일 모두 진입 씬을 `Assets/Multiplayer/Scenes/RoomServerBoot.unity` 하나로 고정한다.
- **출력 경로**: 로컬 산출물은 `Builds/DedicatedServer/Windows/`와 `Builds/DedicatedServer/Linux/` 아래에 둔다. `/Builds/`는 gitignore 대상이므로 산출물은 로컬 검증용으로 취급한다.
- **Editor 검증 경로**: 가장 짧은 경로를 위해 Editor 클라이언트 검증은 메인 UX가 아니라 `RoomClientSmokeTest.unity` 기반 smoke probe로 수행한다.
- **Editor 자동화 포함**: 원래 선택 항목이지만, 한 세션 안에서 재현성을 확보하려면 메뉴/CLI 진입점이 유용하므로 이번 plan 범위에 포함한다.
- **Library 자산 취급**: Unity 6 Build Profile 자산은 `Library/` 아래의 로컬 상태로 관리될 수 있고 저장 위치가 에디터 버전에 종속적일 수 있다. 따라서 영속 협업 산출물은 "프로파일 이름/설정 규약 + Editor 메뉴 스크립트 + 본 plan 문서"로 본다.

## Approach

1. **사전 조건 문서화**  
   Unity Hub에서 Windows/Linux Dedicated Server 빌드에 필요한 모듈이 설치되어 있어야 한다는 점을 명시한다. 구현 세션에서는 설치 여부를 먼저 확인하고, 누락 시 어떤 모듈을 추가해야 하는지 사용자가 바로 따라갈 수 있게 남긴다.
2. **Build Profile 2개 생성**  
   Unity 6 Build Profiles 창에서 `RoomServer-Windows`, `RoomServer-Linux`를 만든다. 두 프로파일 모두 Dedicated Server / headless 설정을 켜고, 서버 씬 목록은 `RoomServerBoot.unity`만 남긴다. 각 프로파일의 출력 경로는 각각 `Builds/DedicatedServer/Windows/`, `Builds/DedicatedServer/Linux/`로 맞춘다.
3. **전역 Build Settings 의존 제거**  
   Dedicated Server 빌드가 `ProjectSettings/EditorBuildSettings.asset`의 전역 씬 목록에 기대지 않도록 한다. 서버 산출물의 씬 구성은 Build Profile 내부 설정 또는 Editor 빌드 스크립트에서 명시적으로 제어한다. 이렇게 해야 `SampleScene` 같은 클라이언트 씬이 서버 산출물에 섞이지 않는다.
4. **Editor asmdef 경계 추가**  
   `Assets/Multiplayer/Scripts/Editor/Murang.Multiplayer.Editor.asmdef`를 추가해 전용 서버 빌드 보조 코드를 `Assembly-CSharp-Editor`에서 분리한다. 이 asmdef는 최소한 `Murang.Multiplayer` 런타임 어셈블리와 UnityEditor 빌드 API를 참조할 수 있어야 한다.
5. **빌드 메뉴/CLI 진입점 구현**  
   `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs`를 추가한다. 메뉴 항목은 `Tools/Multiplayer/Build Dedicated Server (Windows)`와 `Tools/Multiplayer/Build Dedicated Server (Linux)` 두 개로 둔다. 같은 정적 메서드를 `-executeMethod`로도 호출할 수 있게 해서 로컬 CLI 재실행 경로를 만든다.
6. **공유 빌드 설정 로직 정리**  
   에디터 스크립트 내부에서 플랫폼별 출력 디렉터리, 서버 씬 경로, 서버 빌드 옵션, headless 여부를 한곳에서 관리한다. Build Profile API를 직접 호출하든 `BuildPlayerOptions`를 사용하든, 결과적으로 두 경로의 설정 값이 문서화된 프로파일 규약과 일치해야 한다.
7. **Windows 로컬 검증 경로 확립**  
   `RoomServer-Windows` 산출물을 실행하고, Editor에서 `RoomClientSmokeTest.unity`를 Play 모드로 띄워 동일한 룸 이름으로 합류시킨다. 이 검증은 `room-session-lifecycle` plan의 첫 번째 `[manual-hard]` 항목을 만족시키는 최소 경로로 기록한다.
8. **Linux 산출 확인까지만 수행**  
   Linux Dedicated Server는 동일 설정으로 산출물 생성까지만 이번 plan에 포함한다. Docker 이미지 포장, 원격 실행, CI 자동화는 이번 plan에서 다루지 않는다.

## Deliverables

- `docs/specs/multiplayer-network/plans/2026-05-01-namae1128-dedicated-server-build-pipeline.md` — Dedicated Server 빌드/검증 절차를 정의한 실행 계획
- Unity 6 Build Profile 로컬 자산 2개 — `RoomServer-Windows`, `RoomServer-Linux`
- `Assets/Multiplayer/Scripts/Editor/Murang.Multiplayer.Editor.asmdef` — Dedicated Server 빌드 메뉴용 Editor 어셈블리 경계
- `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs` — Windows/Linux Dedicated Server 빌드 메뉴와 CLI 진입점
- `Builds/DedicatedServer/Windows/` — Windows Dedicated Server 로컬 산출물
- `Builds/DedicatedServer/Linux/` — Linux Dedicated Server 로컬 산출물

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/Multiplayer/Scripts/Editor/Murang.Multiplayer.Editor.asmdef`와 `RoomServerBuildMenu.cs`가 Editor 컴파일 에러 없이 로드된다.
- [ ] `[manual-hard]` Unity 6 Build Profiles에 `RoomServer-Windows`, `RoomServer-Linux`가 존재하고, 두 프로파일 모두 Dedicated Server/headless 설정이 켜져 있으며 서버 씬 목록이 `Assets/Multiplayer/Scenes/RoomServerBoot.unity` 하나로만 구성된다.
- [ ] `[manual-hard]` `Tools/Multiplayer/Build Dedicated Server (Windows)` 또는 동등한 CLI 진입점 실행 후 `Builds/DedicatedServer/Windows/` 아래에 Windows Dedicated Server 산출물이 생성된다.
- [ ] `[manual-hard]` `Tools/Multiplayer/Build Dedicated Server (Linux)` 또는 동등한 CLI 진입점 실행 후 `Builds/DedicatedServer/Linux/` 아래에 Linux Dedicated Server 산출물이 생성된다.
- [ ] `[manual-hard]` Windows Dedicated Server 산출물을 실행한 뒤 Editor에서 `RoomClientSmokeTest.unity`를 사용해 같은 룸 이름으로 합류하면, `2026-05-01-namae1128-room-session-lifecycle.md`의 첫 번째 `[manual-hard]` 검증 항목("같은 세션 합류")을 통과할 수 있다.

## Out of Scope

- Photon AppId 환경 분리(개발/스테이징/운영)
- Linux Dedicated Server를 Docker 이미지로 패키징
- GitHub Actions 등 CI에서 Dedicated Server 빌드 자동화
- 9명 정원 초과 검증을 위한 헤드리스 클라이언트 다중 실행 스크립트
- `2026-05-01-namae1128-room-session-lifecycle.md`의 나머지 `[manual-hard]` 항목(#2~#4) 통과

## Notes

- `Library/`는 gitignore 대상이므로 Build Profile 자체는 팀 공유 자산이라기보다 로컬 에디터 상태에 가깝다. 따라서 재현성의 핵심은 "프로파일 이름과 설정 규약"을 본 plan에 못 박고, 같은 구성을 재생성할 수 있는 Editor 메뉴 스크립트를 함께 두는 것이다.
- 검증 우선순위는 Windows 로컬 서버 실행 + Editor 클라이언트 1대 합류다. Linux는 산출만 확인하고 실행 검증은 후속 plan으로 미룬다.
- 메인 클라이언트 씬(`Assets/Scenes/SampleScene.unity`)으로의 합류가 아니라 `RoomClientSmokeTest.unity`를 쓰는 이유는, UI/월드 의존성 없이 네트워크 세션 합류만 가장 짧게 증명하기 위해서다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. 예상 후속 인계 포인트:
- Build Profile 이름: `RoomServer-Windows`, `RoomServer-Linux`
- 메뉴 경로: `Tools/Multiplayer/Build Dedicated Server (Windows|Linux)`
- CLI 진입점: `RoomServerBuildMenu`의 정적 메서드 2개
- Windows 수동 검증은 `RoomClientSmokeTest.unity` 기준
-->
