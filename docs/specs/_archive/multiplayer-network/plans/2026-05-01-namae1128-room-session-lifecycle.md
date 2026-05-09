# 룸 세션 라이프사이클 (비밀번호 옵션 포함)

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)  
**Status:** `Done`

## Goal

Photon Fusion Server 권위 인스턴스를 기준으로 룸 생성, 합류, 퇴장, 정원 제한, 비밀번호 잠금, 마지막 플레이어 퇴장 시 세션 정리까지 포함한 룸 세션 라이프사이클을 구현한다.

## Context

`03-room-session.md`는 여러 사용자가 같은 룸에 합류해 동일한 세션을 공유하는 흐름을 요구한다. 이 plan은 그중에서도 가장 핵심적인 서버/클라이언트 라이프사이클을 담당한다.

- Photon Fusion SDK import는 선행 plan에서 완료되어 있다.
- Unity 클라이언트 인증/백엔드 연동 기반은 선행 plan에서 완료되어 있다.
- 룸 목록 조회와 Presence UI는 각각 별도 후속 plan에서 다룬다.

## Approach

1. `Assets/Multiplayer/Scripts/Room/` 아래에 `Common`, `Client`, `Server`, `Tests` 구조를 정리한다.
2. `RoomCreateOptions`, `RoomJoinOptions`, `RoomJoinResult`, `RoomJoinFailureReason`, `RoomPasswordHasher`, `RoomSessionPropertyKeys` 등 공용 타입을 만든다.
3. `RoomServerBootstrap`과 `RoomAuthority`로 Dedicated Server 런타임을 부팅하고, 정원/비밀번호 검증과 세션 종료를 서버 권위에서 처리한다.
4. `RoomClient`와 `RoomClientSmokeProbe`로 생성/합류/퇴장 API와 smoke 경로를 제공한다.
5. mock 계정 CLI 인자와 자동화용 결과 파일 쓰기 경로를 추가해 여러 클라이언트를 무인으로 실행할 수 있게 한다.
6. EditMode 단위 테스트와 end-to-end 자동화 하네스를 추가해 주요 수용 기준을 반복 검증한다.

## Deliverables

- `Assets/Multiplayer/Scripts/Room/Common/`
- `Assets/Multiplayer/Scripts/Room/Client/`
- `Assets/Multiplayer/Scripts/Room/Server/`
- `Assets/Multiplayer/Scripts/Room/Tests/`
- `Assets/Multiplayer/Scenes/RoomServerBoot.unity`
- `Assets/Multiplayer/Scenes/RoomClientSmokeTest.unity`
- `Assets/Multiplayer/Resources/RoomServerConfig.asset`
- `Assets/Multiplayer/Resources/RoomClientConfig.asset`
- `tools/run-room-editmode-tests.ps1`
- `tools/run-room-lifecycle-automation.ps1`

## Progress Update

- 2026-05-03: 서버/클라이언트 룸 라이프사이클, 비밀번호 해시, 세션 메타데이터, 거절 사유 전달, mock 계정 CLI 분기 구현을 완료했다.
- 2026-05-03: `powershell -ExecutionPolicy Bypass -File .\tools\run-room-editmode-tests.ps1` 기준 `Murang.Multiplayer.Room.Tests`가 `12/12` 통과했다.
- 2026-05-03: `powershell -ExecutionPolicy Bypass -File .\tools\run-room-lifecycle-automation.ps1` 기준 `same-session`, `room-full`, `wrong-password`, `correct-password`, `room-cleanup` 5개 시나리오가 모두 통과했다.
- 최신 자동화 요약은 `TestResults/room-lifecycle-automation/20260503-200059/summary.json`에 남긴다.

## Acceptance Criteria

- [x] `[auto-hard]` `Assets/Multiplayer/Scripts/Room/`가 컴파일 에러 없이 빌드된다.
- [x] `[auto-hard]` `RoomPasswordHasher` 단위 테스트가 모두 통과한다.
- [x] `[auto-hard]` `RoomAuthority.ValidateJoin` 단위 테스트가 모두 통과한다.
- [x] `[manual-hard]` 같은 세션 합류 시나리오가 통과한다.
- [x] `[manual-hard]` 정원 8명 상태에서 9번째 클라이언트가 `RoomFull`로 거절된다.
- [x] `[manual-hard]` 잘못된 비밀번호로 합류 시 `WrongPassword`가 전달된다.
- [x] `[manual-hard]` 올바른 비밀번호로 합류 시 정상 입장한다.
- [x] `[manual-hard]` 마지막 플레이어가 떠나면 세션이 정리되고, 늦게 오는 클라이언트는 `RoomNotFound`를 받는다.

## Out of Scope

- 룸 목록 조회
- Presence UI
- 비밀번호 변경/재설정
- 호스트 마이그레이션
- 음성 채팅
- Photon AppId 환경 분리

## Notes

- 비밀번호 해시는 SHA-256 base64로 계산하고, 로비에 노출되는 `SessionProperties`에는 해시를 넣지 않는다. 공개 메타데이터는 `IsLocked`만 사용한다.
- 최신 검증 근거는 `TestResults/room-editmode-results.xml`, `Logs/room-editmode-tests.log`, `TestResults/room-lifecycle-automation/20260503-200059/summary.json`이다.
- 자동화 하네스는 결과 파일 기록 이후 프로세스를 정리하는 방식이라 일부 실행 요약에 `processForcedStop: true`가 남는다. 기능 검증은 통과했지만 종료 경로 polish는 후속 개선 여지로 남긴다.

## Handoff

- Dedicated Server Build Profiles와 Linux 전용 산출/검증 정리는 [`2026-05-01-namae1128-dedicated-server-build-pipeline.md`](2026-05-01-namae1128-dedicated-server-build-pipeline.md)에서 이어간다.
- 룸 목록 조회 plan은 `RoomSessionPropertyKeys.IsLocked`와 룸 생성/합류 결과 타입을 재사용할 수 있다.
