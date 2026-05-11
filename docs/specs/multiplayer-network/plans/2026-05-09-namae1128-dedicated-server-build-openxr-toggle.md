# dedicated-server 빌드 시 Standalone OpenXR loader 임시 토글

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `In Progress`

## Goal

`Tools/Multiplayer/Build Dedicated Server (Windows|Linux)` 메뉴에서 호출되는 dedicated-server 빌드가 **Standalone XR Plug-in Management의 OpenXR loader 설정과 무관하게** 항상 통과하도록 만든다. Editor Play와 PC VR Player 빌드에는 영향 없이, dedicated-server 빌드 직전에만 OpenXR loader를 비우고 빌드 후 원상 복원한다.

## Context

선행 plan [`2026-05-01-namae1128-room-list-query.md`](../../_archive/multiplayer-network/plans/2026-05-01-namae1128-room-list-query.md) 검증 중에 다음 흐름이 발생했다.

1. `Tools/Multiplayer/Build Dedicated Server (Linux)` 호출 → `BuildFailedException: OpenXR Build Failed.` + `Error building Player: 3 errors`로 빌드 실패.
2. 회피책으로 사용자가 `Edit → Project Settings → XR Plug-in Management → Standalone(Linux)` 탭에서 OpenXR loader를 비웠다 (`Assets/XR/XRGeneralSettingsPerBuildTarget.asset`의 `Standalone Providers` MonoBehaviour `m_Loaders: []`로 변경).
3. 빌드는 통과했지만, Unity XR Plug-in Management UI는 **Standalone 탭이 Windows/macOS/Linux + Server/Player subtarget 전체를 한 그룹으로 관리**하므로 사실상 다음 영역도 OpenXR이 깨진다:
   - Editor Play 모드 (XR Origin·Hand Tracking 등 OpenXR 기반 컴포넌트 초기화 실패)
   - 향후 Windows/Linux/macOS Standalone Player 빌드 (PC VR 클라이언트 시나리오)
4. 이 회피책은 commit하지 않고 revert해 둔 상태(`Assets/XR/XRGeneralSettingsPerBuildTarget.asset`이 git HEAD 그대로) — 다음에 dedicated-server를 다시 빌드하면 동일 OpenXR Build Failed로 막힌다.

빌드 진입점은 [`Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs`](../../../../Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs)의 `BuildDedicatedServer(BuildTarget, string)` 단일 메서드다. 본 plan은 이 메서드의 `BuildPipeline.BuildPlayer(options)` 호출을 try/finally 가드로 감싸, 진입 시 Standalone 그룹의 XR loader를 백업·비우기, 종료 시 복원하는 작은 변경만 가한다.

### 핵심 결정

- **토글 대상**: `XRGeneralSettingsPerBuildTarget.AssetForBuildTarget(BuildTargetGroup.Standalone).Manager.activeLoaders` 리스트 한 곳. Android·iOS·visionOS 등 다른 그룹은 손대지 않는다.
- **클라이언트(`BuildRoomTestClient`)는 영향 범위 밖**. 본 plan의 가드는 dedicated-server 빌드 경로에만 들어간다. RoomClientSmokeTest는 헤드리스 자동화 클라이언트라 OpenXR이 없어도 동작하지만, "Server 빌드 OpenXR 충돌"이 본 plan의 트리거라 클라이언트 경로를 굳이 건드리지 않는다.
- **복원 보장**: try/finally로 빌드 실패·예외·취소 어떤 경로에서도 원래 loader 리스트를 되돌린다. AssetDatabase에 저장 누락이 일어나지 않도록 백업·비우기 시점, 복원 시점 모두 `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`을 호출한다.
- **subtarget 분리는 시도하지 않는다**. Unity 6의 XR Plug-in Management는 `BuildTargetGroup` 단위까지만 분리하고 `StandaloneBuildSubtarget` 단위 분리는 지원하지 않는다. 빌드 시점 토글이 가장 정확한 우회 경로다.
- **GUID 기반 백업**: loader 인스턴스 자체를 보관하면 도메인 리로드 사이에 참조가 깨질 수 있어 위험하다. 대신 `string[] backupGuids` 형태로 each loader의 `AssetDatabase.GUIDFromAssetPath(...)`를 보관하고, 복원 시 `AssetDatabase.LoadAssetAtPath<XRLoader>(...)`로 다시 로드해 리스트를 채운다.

## Approach

1. **헬퍼 추가** — `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs` 안에 `private static`로 두 메서드 추가:
   - `string[] BackupAndClearStandaloneXrLoaders()` — Standalone XRGeneralSettings의 `Manager.activeLoaders`에서 각 loader의 GUID 배열을 만든 뒤 `Manager.activeLoaders.Clear()` + dirty/save. 빈 배열 반환 시 변경 사항 없음.
   - `void RestoreStandaloneXrLoaders(string[] backupGuids)` — backup 배열이 비어 있으면 noop. 그렇지 않으면 각 GUID를 path로 변환 → `AssetDatabase.LoadAssetAtPath<XRLoader>(path)`로 인스턴스 로드 → `Manager.activeLoaders.Add(loader)`. dirty/save.
2. **`BuildDedicatedServer`에 try/finally 가드** — 기존 `BuildReport report = BuildPipeline.BuildPlayer(options)` 한 줄을 다음 패턴으로 감싼다:
   ```csharp
   string[] backup = BackupAndClearStandaloneXrLoaders();
   try
   {
       BuildReport report = BuildPipeline.BuildPlayer(options);
       ThrowIfBuildFailed(report, "dedicated server", outputPath);
   }
   finally
   {
       RestoreStandaloneXrLoaders(backup);
   }
   ```
3. **using 추가** — `UnityEngine.XR.Management`(XRManagerSettings/XRLoader), `UnityEditor.XR.Management.Metadata`(XRPackageMetadataStore — 필요 시), `UnityEditor.XR.Management`(XRGeneralSettingsPerBuildTarget). 정확한 어셈블리 참조는 필요 시 `Assets/Multiplayer/Scripts/Editor/`의 asmdef(있으면) 또는 동일 폴더 다른 Editor 스크립트에서 사용하는 패턴을 따라 맞춘다.
4. **`BuildRoomTestClient`는 가드 밖에 둔다** — 본 plan은 dedicated-server 빌드 경로만 다룬다. `BuildRoomAutomationWindowsArtifacts`는 두 빌드를 순차 호출하므로 자연스럽게 dedicated-server 빌드 부분만 가드 안에서 실행된다.
5. **검증 절차 메모** — 단순 컴파일 통과 + 두 시나리오 수동 검증.
   - 시나리오 A: HEAD에서 (즉 OpenXR loader가 Standalone에 활성인 상태에서) `Tools/Multiplayer/Build Dedicated Server (Linux)`를 호출 → 이전엔 OpenXR Build Failed로 막혔던 빌드가 통과해야 함. 빌드 후 `Edit → Project Settings → XR Plug-in Management → Standalone` 탭을 열어 OpenXR loader가 그대로 활성 상태인지 확인.
   - 시나리오 B: 같은 상태에서 SampleScene을 Editor Play로 실행 → OpenXR이 정상 초기화되고 XR Origin·Hand Tracking 컴포넌트가 동작해야 함 (가드가 영구 비활성화하지 않았음을 확인).

## Deliverables

- `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs` — `BuildDedicatedServer`에 try/finally 가드 추가 + `BackupAndClearStandaloneXrLoaders` / `RestoreStandaloneXrLoaders` 헬퍼 추가.

## Acceptance Criteria

- [x] `[auto-hard]` `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs` 변경분이 컴파일 에러 없이 빌드된다 (Unity 콘솔 에러 0건).
- [x] `[manual-hard]` Standalone XR Plug-in Management 탭에서 OpenXR loader가 **활성** 상태인 그대로 `Tools/Multiplayer/Build Dedicated Server (Linux)`를 호출하면 OpenXR Build Failed 없이 `Builds/RoomAutomation/LinuxServer/RoomServer.x86_64`가 산출되고, 빌드 직후 같은 탭을 다시 열었을 때 OpenXR loader가 여전히 활성 상태로 유지된다.
- [ ] `[manual-hard]` 위 빌드 직후 Editor에서 `SampleScene` 또는 임의의 OpenXR 사용 씬을 Play하면 OpenXR이 정상 초기화된다 (콘솔에 OpenXR 관련 init 실패 0건, XR Origin/Hand Tracking 동작 확인).

보류 사유 (2026-05-09):
- 현재 팀 장비 제약상 `Windows x64 + Meta Quest Link` 기반 Editor Play 실검증 경로가 없다.
- macOS에서의 Meta Quest Link Editor Play는 Unity 공식 지원 검증 경로가 아니므로 본 acceptance의 대체 근거로 쓰지 않는다.
- 따라서 마지막 항목은 추후 `Windows x64 Standalone` 타깃 + Quest Link 가능 환경이 준비되면 재검증 후 닫는다.

## Out of Scope

- Windows Dedicated Server 빌드 모듈 자동 설치(별 plan, 또는 사용자가 Hub로 직접 처리).
- PC VR Player 빌드(Windows/macOS/Linux Standalone with OpenXR) — 본 plan 가드는 dedicated-server 메뉴에서만 발동, Player 빌드는 사용자가 직접 호출하므로 그대로 OpenXR 사용.
- Android(Quest) 빌드 — XRGeneralSettings의 Android 그룹은 본 plan이 손대지 않는다.
- subtarget(Server vs Player) 단위로 XR 설정을 분리하는 작업 — Unity 6 XR Plug-in Management가 지원하지 않음.
- `RoomServerBuildMenu` 외부의 다른 빌드 진입점에 동일 가드를 자동 적용 — 본 plan은 메뉴 4개(`Build Dedicated Server (Windows)`, `(Linux)`, `Build Room Test Client (Windows)`, `BuildRoomAutomationWindowsArtifacts`) 중 dedicated-server 두 메뉴 + 자동화 메뉴의 dedicated-server 부분만 다룬다.

## Notes

- 백업/복원 시 `Manager.activeLoaders`의 직접 수정은 Unity Editor 내부 상태이므로 `EditorUtility.SetDirty(asset)` + `AssetDatabase.SaveAssets()` 호출이 빠지면 다음 도메인 리로드에서 변경이 사라질 수 있다. 두 시점 모두 명시적으로 호출한다.
- Standalone 그룹에 OpenXR 외 다른 loader(예: Mock HMD, Unity XR SDK Direct)도 동시에 있을 수 있다. 본 plan은 loader 종류를 가리지 않고 전체를 backup/clear/restore하므로 OpenXR 이외의 loader도 정확히 보존된다.
- `XRGeneralSettingsPerBuildTarget.AssetForBuildTarget(BuildTargetGroup.Standalone)`이 `null`을 반환할 수 있다(설정 파일이 없을 때). 이 경우 backup은 빈 배열, restore도 noop으로 흘러야 한다 — `BackupAndClearStandaloneXrLoaders`에서 null guard 한 줄 필수.

## Handoff

### 2026-05-09 진행 기록

- `Assets/Multiplayer/Scripts/Editor/Murang.Multiplayer.Editor.asmdef`에 `Unity.XR.Management`, `Unity.XR.Management.Editor` 참조를 추가해 `RoomServerBuildMenu.cs`의 XR Management API 컴파일 에러를 복구했다.
- `Assets/Multiplayer/Scripts/Editor/RoomServerBuildMenu.cs`에서 dedicated-server 빌드 경로만 Standalone XR loader를 `backup -> clear -> build -> restore` 순서로 감싸도록 구현했다.
- Unity MCP로 `Tools/Multiplayer/Build Dedicated Server (Linux)`를 실행해 `Built dedicated server at D:\2026-capstone-24\Builds\RoomAutomation\LinuxServer\RoomServer.x86_64` 로그를 확인했고, 산출물 타임스탬프도 갱신됐다.
- 빌드 후 `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`를 확인했을 때 Standalone `m_Loaders`에 OpenXR loader GUID(`cdf7e1d665a9ccb4eb23295a8cd09435`)가 다시 들어와 있어 복원이 유지됨을 확인했다.
- 남은 검증은 `Windows x64 Standalone` 타깃으로 되돌린 뒤 `Assets/Scenes/SampleScene.unity`를 Editor Play + Meta Quest Link 환경에서 다시 확인하는 것이다. 이번 세션에서 본 OpenXR Play 에러는 Linux Standalone 타깃이 활성인 상태에서 발생한 로그라 최종 acceptance로 닫지 않았다.
- Unity 검증 중 `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`와 일부 ProjectSettings/URP 자산이 touched 상태가 되었지만, 이번 변경 세트에는 semantic diff가 있는 코드/문서만 포함한다.

후속 plan이 알아야 할 공개 동작:
- `BuildDedicatedServer` 호출은 진입 시점의 Standalone XR loader 상태를 보존한다.
- 동일 가드를 다른 빌드 진입점(예: 미래의 `RoomServerBuildMenu` 추가 메뉴)에 확장하려면 `BackupAndClearStandaloneXrLoaders` / `RestoreStandaloneXrLoaders` 헬퍼를 같은 try/finally 패턴으로 재사용한다.
