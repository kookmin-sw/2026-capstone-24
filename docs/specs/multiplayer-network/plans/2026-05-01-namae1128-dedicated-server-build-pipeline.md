# Dedicated Server 빌드 파이프라인 (Windows 로컬 + Linux 산출)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)  
**Status:** `In Progress`

## Goal

`RoomServerBoot` 진입 경로를 Windows/Linux Dedicated Server 산출물로 빌드하고, 가장 짧은 검증 경로로 서버 산출물과 클라이언트 런타임이 실제로 합류하는지 확인한다.

## Context

`2026-05-01-namae1128-room-session-lifecycle.md`는 이미 구현 및 자동 검증까지 완료되었다. 이번 plan은 그 구현을 전용 서버 산출물과 빌드 워크플로우 차원에서 정리하는 후속 단계다.

- `RoomServerBoot.unity`, `RoomServerBootstrap`, `RoomAuthority`, `RoomClientSmokeTest.unity`는 이미 존재한다.
- `Murang.Multiplayer.Editor.asmdef`는 이미 존재한다.
- 2026-05-03 기준 `RoomServerBuildMenu.cs`와 `tools/run-room-lifecycle-automation.ps1`가 추가되어 Windows 전용 자동 검증 경로는 동작한다.

## Current Progress

- 2026-05-03: `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs`를 추가해 Windows/Linux Dedicated Server 메뉴와 CLI 진입점을 만들었다.
- 2026-05-03: 현재 구현은 `Builds/RoomAutomation/WindowsServer/RoomServer.exe`, `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`, `Builds/RoomAutomation/WindowsClient/RoomClientSmokeTest.exe` 경로를 기준으로 산출물을 다룬다.
- 2026-05-03: `tools/run-room-lifecycle-automation.ps1`가 Windows Dedicated Server + Windows test client 조합을 다시 빌드하고 5개 룸 라이프사이클 시나리오를 모두 통과했다.
- 아직 남은 항목은 Unity 6 Build Profiles 자산 자체의 정리, Linux 산출 검증, Editor 클라이언트 수동 합류 경로 문서화다.

## Approach

1. `RoomServerBuildMenu`를 기준으로 Dedicated Server 빌드 진입점을 유지한다.
2. Windows 전용 자동 검증 경로는 `run-room-lifecycle-automation.ps1`로 계속 재현 가능하게 둔다.
3. Unity 6 Build Profiles `RoomServer-Windows`, `RoomServer-Linux`를 추가해 에디터에서도 동일한 빌드 구성을 확인할 수 있게 한다.
4. Linux Dedicated Server 산출을 실제로 생성하고 결과 경로를 문서에 반영한다.
5. Editor에서 `RoomClientSmokeTest.unity`로 Windows Dedicated Server에 합류하는 가장 짧은 수동 경로를 별도 체크리스트로 남긴다.

## Deliverables

- `Assets/Multiplayer/Scripts/Editor/Murang.Multiplayer.Editor.asmdef`
- `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs`
- Unity 6 Build Profiles 로컬 자산 2개: `RoomServer-Windows`, `RoomServer-Linux`
- `Builds/RoomAutomation/WindowsServer/RoomServer.exe`
- `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`
- `Builds/RoomAutomation/WindowsClient/RoomClientSmokeTest.exe`
- 관련 검증 메모와 실행 절차

## Acceptance Criteria

- [x] `[auto-hard]` `Assets/Multiplayer/Scripts/Editor/Murang.Multiplayer.Editor.asmdef`와 `RoomServerBuildMenu.cs`가 Editor 컴파일 에러 없이 로드된다.
- [ ] `[manual-hard]` Unity 6 Build Profiles에 `RoomServer-Windows`, `RoomServer-Linux`가 존재한다.
- [x] `[manual-hard]` `RoomServerBuildMenu`의 Windows CLI 경로로 `Builds/RoomAutomation/WindowsServer/RoomServer.exe`가 생성된다.
- [ ] `[manual-hard]` Linux Dedicated Server 산출이 실제로 생성되고 경로가 검증된다.
- [ ] `[manual-hard]` Windows Dedicated Server 산출물에 Editor의 `RoomClientSmokeTest.unity`가 합류하는 가장 짧은 수동 경로가 문서화되고 확인된다.

## Out of Scope

- Photon AppId 환경 분리
- Linux Dedicated Server Docker 패키징
- GitHub Actions 등 CI 빌드 자동화
- 9명 초과 시나리오용 대량 헤드리스 클라이언트 실행 스크립트
- 나머지 멀티플레이어 UI 작업

## Notes

- 현재 가장 강한 검증 근거는 `tools/run-room-lifecycle-automation.ps1`와 `TestResults/room-lifecycle-automation/20260503-200059/summary.json`이다.
- 현재 자동 검증은 Editor 클라이언트가 아니라 Windows test client 빌드를 사용한다. 기능 검증은 충분하지만, 원래 plan의 "Editor 클라이언트 1대 합류" 항목은 아직 별도 체크가 필요하다.
- Build Profiles 자산은 `Library/` 아래 로컬 상태에 가까우므로, 재현성의 핵심은 프로파일 이름/설정 규약과 Editor 메뉴 스크립트를 함께 유지하는 것이다.

## Handoff

- 다음 작업자는 Build Profiles 생성 여부와 Linux 산출 검증부터 이어서 확인하면 된다.
- `RoomServerBuildMenu.BuildDedicatedServerWindows`, `BuildDedicatedServerLinux`, `BuildRoomAutomationWindowsArtifacts`가 현재 진입점이다.
