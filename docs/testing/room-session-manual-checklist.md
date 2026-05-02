# Room Session Manual Checklist

## Scope

`docs/specs/multiplayer-network/plans/2026-05-01-namae1128-room-session-lifecycle.md`의 남은 수동 검증 항목을 빠르게 확인하는 순서다.

2026-05-02 기준 자동 검증은 이미 끝났다.

- `Murang.Multiplayer.Room.Tests` EditMode: 12/12 통과
- 결과 XML: `TestResults/room-editmode-results.xml`
- 실행 로그: `Logs/room-editmode-tests.log`

## Important Context

- Dedicated Server 경로에서는 `RoomServerBootstrap`가 부팅 직후 세션을 만든다.
- 그래서 **Dedicated Server 산출물을 검증할 때 첫 클라이언트는 `룸 생성`보다 `룸 입장`을 누르는 쪽이 더 자연스럽다.**
- `룸 생성` 경로는 별도로 확인할 수 있지만, 그건 Dedicated Server 산출물 검증과는 조금 다른 smoke 성격이다.
- 동시 접속 클라이언트는 **서로 다른 mock 계정**으로 띄워야 한다.
- 이제 클라이언트 실행 시 `-mockAccountId <id>` 인자를 줄 수 있다.

## One-Time Setup

1. Unity Hub에서 `Unity 6000.3.10f1`에 `Windows Build Support (IL2CPP)`와 `Windows Dedicated Server Build Support`가 설치되어 있는지 확인한다.
2. 서버용 씬은 `Assets/Multiplayer/Scenes/RoomServerBoot.unity`를 쓴다.
3. 클라이언트용 씬은 `Assets/Multiplayer/Scenes/RoomClientSmokeTest.unity`를 쓴다.
4. 기본 룸 이름은 `murang-room`이고 기본 최대 인원은 `8`이다.
5. 패스워드 해시는 아래 명령으로 만든다.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\get-room-password-hash.ps1 -Password "1234"
```

## Quickest Dedicated Server Path

### 1. Server 빌드/실행

1. Unity에서 `Assets/Multiplayer/Scenes/RoomServerBoot.unity`를 연다.
2. Windows Dedicated Server 대상으로 빌드한다.
3. 서버를 실행한다.
4. 기본 설정이면 `murang-room` 세션이 바로 열린다.

필요하면 실행 인자로 덮어쓸 수 있다.

```powershell
.\RoomServerBoot.exe -roomName murang-room -maxPlayers 8
```

패스워드 룸은 `-passwordHash`까지 붙인다.

```powershell
.\RoomServerBoot.exe -roomName locked-room -passwordHash "<base64-sha256>"
```

### 2. Editor 클라이언트 1

1. Unity에서 `Assets/Multiplayer/Scenes/RoomClientSmokeTest.unity`를 연다.
2. Play를 누른다.
3. `Room Client Smoke Probe` 박스에서 룸 이름을 서버와 같게 맞춘다.
4. Dedicated Server 검증 경로에서는 `룸 입장`을 누른다.
5. 상태가 `룸 입장 성공: <room>`이면 통과다.

Editor는 기본 mock 계정 `quest-user-01`을 쓴다.

### 3. Standalone 클라이언트 2

1. `Assets/Multiplayer/Scenes/RoomClientSmokeTest.unity`를 Windows 클라이언트로 한 번 빌드한다.
2. 같은 클라이언트 빌드를 실행하되, Editor와 다른 계정을 준다.

```powershell
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-02"
```

3. 클라이언트에서 같은 룸 이름을 넣고 `룸 입장`을 누른다.
4. 상태가 `룸 입장 성공: <room>`이면 두 번째 클라이언트 합류까지 통과다.

## Password Scenario

### Wrong Password

1. 서버를 아래처럼 패스워드 룸으로 띄운다.
2. 클라이언트는 비밀번호를 비우거나 틀린 값을 넣고 `룸 입장`을 누른다.
3. 상태에 `WrongPassword`가 보이면 통과다.

```powershell
$hash = powershell -ExecutionPolicy Bypass -File .\tools\get-room-password-hash.ps1 -Password "1234"
.\RoomServerBoot.exe -roomName locked-room -passwordHash $hash
```

### Correct Password

1. 같은 서버에 접속한다.
2. 클라이언트 비밀번호 입력칸에 `1234`를 넣고 `룸 입장`을 누른다.
3. 상태가 `룸 입장 성공: locked-room`이면 통과다.

## Room Full Scenario

이 항목은 손이 많이 간다. 그래도 지금은 같은 클라이언트 빌드를 여러 번 띄우면서 `-mockAccountId`만 바꿔서 검증할 수 있다.

1. 서버를 `-roomName full-room -maxPlayers 8`로 띄운다.
2. 첫 번째 클라이언트부터 여덟 번째 클라이언트까지 각각 다른 계정으로 실행한다.

```powershell
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-01"
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-02"
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-03"
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-04"
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-05"
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-06"
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-07"
Start-Process ".\RoomClientSmokeTest.exe" -ArgumentList "-mockAccountId", "quest-user-08"
```

3. 여덟 명 모두 `full-room`에 `룸 입장`한다.
4. 아홉 번째 클라이언트를 `quest-user-09`로 띄운다.
5. 같은 룸에 입장 시도한다.
6. 상태가 `RoomFull`이면 통과다.

## Last Leave / Room Cleanup

현재 구현은 마지막 클라이언트가 나가면 `runner.Shutdown()`을 호출한다. 다만 **서버 프로세스 자체를 자동 종료시키지는 않는다.**

그래서 이 항목은 아래처럼 확인한다.

1. 둘 이상의 클라이언트를 같은 룸에 붙인다.
2. 모두 `룸 퇴장`을 누르거나 클라이언트를 닫는다.
3. 새 클라이언트를 다른 `-mockAccountId`로 띄운다.
4. 아무도 다시 룸을 만들지 않은 상태에서 같은 룸 이름으로 `룸 입장`을 누른다.
5. `RoomNotFound` 또는 그에 준하는 실패가 나오면 룸 정리까지 된 것으로 본다.

## Optional: CreateRoomAsync Smoke

`RoomClient`의 `룸 생성` 경로만 따로 확인하고 싶으면, Dedicated Server를 띄우지 않은 상태에서 `RoomClientSmokeTest`를 사용해 본다.

1. Editor에서 `RoomClientSmokeTest.unity`를 Play한다.
2. `룸 생성`을 누른다.
3. 두 번째 클라이언트를 다른 `-mockAccountId`로 띄워 같은 룸 이름으로 `룸 입장`한다.

이 경로는 `CreateRoomAsync` smoke에는 좋지만, Dedicated Server 산출물 검증 자체와는 별개로 보면 된다.

## Troubleshooting

- 클라이언트 둘이 같은 사용자처럼 보이면 실행 인자 `-mockAccountId`가 빠졌는지 확인한다.
- mock 계정을 바꿔 실행하면 새 인스턴스 시작 시 auth 토큰 캐시를 비우고 다시 로그인한다. 정상 동작이다.
- 패스워드 룸은 평문이 아니라 SHA-256 base64 해시를 서버에 넘겨야 한다.
- Dedicated Server 검증 중에는 첫 클라이언트도 `룸 입장`을 쓰는 쪽이 현재 구현과 더 잘 맞는다.
