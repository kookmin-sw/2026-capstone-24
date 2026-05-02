# Unity Room EditMode Tests

## Purpose

`Murang.Multiplayer.Room.Tests` Edit Mode 테스트를 Unity Test Framework 방식으로 실행한다.

수동 런타임 검증 절차는 `docs/testing/room-session-manual-checklist.md`를 본다.

## Recommended CLI Command

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-room-editmode-tests.ps1
```

성공하면 종료 코드는 `0`이다.

단, 이 경로는 **프로젝트가 이미 Unity Editor에 열려 있지 않아야 한다.**

## Outputs

- Result XML: `TestResults/room-editmode-results.xml`
- Unity log: `Logs/room-editmode-tests.log`

## Run Only Specific Tests

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-room-editmode-tests.ps1 `
  -TestFilter "Murang.Multiplayer.Room.Tests.RoomPasswordHasherTests"
```

여러 필터를 줄 때는 배열로 넘긴다. 스크립트가 내부적으로 `;` 구분자로 합친다.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-room-editmode-tests.ps1 `
  -TestFilter @(
    "Murang.Multiplayer.Room.Tests.RoomPasswordHasherTests",
    "Murang.Multiplayer.Room.Tests.RoomAuthorityValidateJoinTests"
  )
```

## Run With a Different Unity Editor

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-room-editmode-tests.ps1 `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe"
```

## Important Notes

- `dotnet test`나 `dotnet vstest`가 아니라 **Unity Editor의 `-runTests`** 경로를 사용해야 한다.
- `-quit`를 함께 주지 않는다. Unity Test Framework가 테스트 완료 후 자체적으로 종료 코드를 반환한다.
- `-assemblyNames`는 `;` 구분자를 쓴다.
- 현재 프로젝트의 룸 테스트는 Edit Mode 전용 asmdef(`Murang.Multiplayer.Room.Tests.asmdef`) 기준으로 실행된다.

## Manual Editor Path

1. Unity Editor에서 프로젝트를 연다.
2. `Tools > Multiplayer > Run Room EditMode Tests`를 누르거나 `Window > General > Test Runner`를 연다.
3. `EditMode` 탭을 선택한다.
4. `Murang.Multiplayer.Room.Tests` 어셈블리만 선택해서 실행한다.

## Troubleshooting

- 결과 XML이 없으면 먼저 `Logs/room-editmode-tests.log`를 본다.
- CLI 실행 시 "another Unity instance is running"이 나오면 Editor를 닫고 다시 실행하거나, 열린 Editor 안에서 메뉴 경로로 돌린다.
- 스크립트 컴파일 에러가 있으면 Unity가 `RunError` 코드로 종료한다.
- 테스트가 0개 실행되면 asmdef include platform, define constraint, test filter를 확인한다.
