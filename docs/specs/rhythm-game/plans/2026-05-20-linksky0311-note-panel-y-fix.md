# Note Panel Y Position Fix — worldYOverride 필드 추가

**Linked Spec:** [`10-note-panel-y-fix.md`](../specs/10-note-panel-y-fix.md)
**Status:** `Done`

## Goal

피아노·드럼 모두 NoteDisplayPanel의 world Y 위치를 Inspector에서 절대값(m 단위)으로 고정 지정할 수 있게 한다. 동일 악기 prefab 인스턴스가 재생성될 때도 설정된 Y값이 유지된다.

## Context

리듬게임 노트 UI 패널이 두 악기 모두 Y축으로 부적절한 위치에 생성되어 시각적 파악이 어렵다. 현황:

- **피아노:** Piano.prefab (GUID `20c3ebc0d8a60954b9bf428c65c948ce`) 내부에는 NoteDisplayPanel이 없고, 두 씬(`SampleScene.unity`, `TestSceneSanyo.unity`)에 `NoteDisplayPanel_Piano` 이름으로 NoteDisplayPanel.prefab (GUID `a8b4f3c2d1e04a5b9c7e8f0d2a3b4c5d`) 인스턴스가 배치되어 있다. 씬 내 `m_LocalPosition.y = 0`, `m_AnchoredPosition.y = 1.225` 상태. 두 씬 모두 동일 PrefabInstance fileID `1229096218` 사용.
- **드럼:** `DrumNoteDisplayAdapter.ComputePanelPosition()`이 collider bounds 상단 + `yOffset`을 매 세션마다 동적 계산. `yOffset` 필드는 *상대* 오프셋이라 절대 Y 제어 불가. 재생성 시 collider 좌표가 바뀌면 절대 Y도 변동.

Sub-spec What 요건:
1. 피아노 — prefab/씬에 박힌 패널 Transform Y가 고정값과 동일.
2. 드럼 — DrumNoteDisplayAdapter에 world Y(m) 직접 지정 Inspector 필드 추가.
3. 동일 악기 인스턴스 재생성 시 Y값 유지.

해법(메인 추론 한 줄: "DrumNoteDisplayAdapter에 worldYOverride(float) 필드 추가 + ComputePanelPosition 수정, 피아노 NoteDisplayPanel에도 동일 필드 추가"):

NoteDisplayPanel과 DrumNoteDisplayAdapter 양쪽 모두에 동일 의미의 `worldYOverride: float` 필드를 추가하고, sentinel 값(예: `float.NaN`)으로 "미지정"을 표현해 기존 동작과 호환되도록 한다. 피아노는 씬 내 NoteDisplayPanel_Piano 인스턴스의 worldYOverride override를 추가해 절대 Y 지정. 드럼은 ComputePanelPosition()이 worldYOverride가 유효하면 collider bounds 계산을 건너뛰고 그 값을 worldY로 사용.

## Verified Structural Assumptions

- `NoteDisplayPanel.cs`는 `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` 경로, namespace `RhythmGame.Runtime`, `MonoBehaviour` 상속, `[RequireComponent(typeof(RectTransform))]` 부착. `Awake()`에서 `_panelRt = GetComponent<RectTransform>()` 캐시 후 `RectMask2D` 추가 + `BillboardUI` 옵션 적용. — `Read Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs (2026-05-20)`
- `DrumNoteDisplayAdapter.cs`는 `Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs`, namespace `RhythmGame.Runtime`. `Init()`에서 `Vector3 worldPos = ComputePanelPosition(zone.transform, zone.PanelYOffset)` 호출 후 `panel.transform.position = worldPos`로 패널 world 위치 결정. `ComputePanelPosition()`는 `yOffset + extraOffset`을 collider bounds.max.y에 더해 worldY 계산. — `Read Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs (2026-05-20)`
- Piano.prefab (GUID `20c3ebc0d8a60954b9bf428c65c948ce`)은 NoteDisplayPanel 자식을 포함하지 않으며, RhythmGameHost.noteDisplayPanel은 씬 인스턴스를 참조한다. SampleScene.unity와 TestSceneSanyo.unity 둘 다 동일한 PrefabInstance fileID `1229096218` (NoteDisplayPanel.prefab GUID `a8b4f3c2d1e04a5b9c7e8f0d2a3b4c5d` 기반)을 사용하며, m_Modifications에 `propertyPath: m_LocalPosition.y / value: 0`, `propertyPath: m_AnchoredPosition.y / value: 1.225`, `propertyPath: panelHeight / value: 600` 등의 override를 포함한다. RhythmGameHost.noteDisplayPanel은 stripped MonoBehaviour fileID `1229096220`를 가리킨다. — `Read Assets/Scenes/SampleScene.unity (2026-05-20)` + `Read Assets/Scenes/TestSceneSanyo.unity (2026-05-20)`
- NoteDisplayPanel.prefab 자체 (`Assets/RhythmGame/Prefabs/NoteDisplayPanel.prefab`)는 RectTransform `m_LocalScale: 0.01 / 0.01 / 0.01`, MonoBehaviour script GUID `713ece9fb55d4098acead8501e30600e`, `m_EditorClassIdentifier: Assembly-CSharp::NoteDisplayPanel`. — `Read Assets/RhythmGame/Prefabs/NoteDisplayPanel.prefab (2026-05-20)`
- NoteDisplayPanel.cs MonoBehaviour의 직렬화 필드 추가 시 fileID 7000000004 대상 propertyPath override를 새 필드 이름으로 추가 가능. 두 씬의 PrefabInstance fileID `1229096218` m_Modifications 리스트 끝부분(`m_RemovedComponents` 직전)에 추가하면 됨. — `Read Assets/Scenes/SampleScene.unity (2026-05-20)` lines 4869-4881
- `RhythmGame.Runtime.asmdef`는 `RhythmGame.Data`, `RhythmGame.Runtime.Clock`, `Instruments`, `Unity.InputSystem`을 references로 포함. 본 plan은 UnityEngine 외 추가 namespace import가 없어 asmdef 변경 불필요. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef (2026-05-20)`

## Approach

### 1. `NoteDisplayPanel.cs`에 `worldYOverride` 필드 추가

`[SerializeField] float worldYOverride = float.NaN;` 필드 추가 (Header "World Y Override (NaN = 미적용)"). `Awake()` 또는 `Show()` 시점에 `!float.IsNaN(worldYOverride)`이면 `transform.position`의 world Y를 강제 지정:

```csharp
[Header("World Y Override (NaN = 미적용, 값 지정 시 transform.position.y 강제)")]
[SerializeField] float worldYOverride = float.NaN;

void Awake()
{
    _panelRt = GetComponent<RectTransform>();
    if (GetComponent<RectMask2D>() == null)
        gameObject.AddComponent<RectMask2D>();

    if (panelTiltDegrees > 0f) { /* 기존 */ }

    ApplyWorldYOverride();
}

void ApplyWorldYOverride()
{
    if (float.IsNaN(worldYOverride)) return;
    Vector3 p = transform.position;
    p.y = worldYOverride;
    transform.position = p;
}
```

`Show()` 끝에서도 `ApplyWorldYOverride()` 한 번 더 호출 — `gameObject.SetActive(true)` 직후 부모 Transform 변경에 따른 좌표 재계산 후에도 강제 보정.

### 2. `DrumNoteDisplayAdapter.cs` `worldYOverride` 필드 추가 + `ComputePanelPosition()` 수정

`yOffset` 필드 유지(기존 동작 호환). `[SerializeField] float worldYOverride = float.NaN;` 신규 추가:

```csharp
[Tooltip("절대 world Y(m) 지정. NaN(기본)이면 기존 collider 기반 동적 계산 사용.")]
[SerializeField] float worldYOverride = float.NaN;

Vector3 ComputePanelPosition(Transform t, float extraOffset = 0f)
{
    // worldYOverride 지정 시 절대 Y 사용, 미지정 시 기존 동적 계산
    bool useOverride = !float.IsNaN(worldYOverride);
    float totalOffset = yOffset + extraOffset;

    float worldY;
    if (useOverride)
    {
        worldY = worldYOverride;
    }
    else
    {
        Collider col = t.GetComponentInChildren<Collider>();
        worldY = col != null
            ? col.bounds.max.y + totalOffset
            : t.position.y + 0.1f + totalOffset;
    }

    // XZ는 기존 로직 그대로
    DrumPiece piece = t.GetComponentInParent<DrumPiece>();
    if (piece != null)
    {
        Renderer rend = piece.GetComponentInChildren<Renderer>();
        if (rend != null)
            return new Vector3(rend.bounds.center.x, worldY, rend.bounds.center.z);
    }
    Collider col2 = t.GetComponentInChildren<Collider>();
    if (col2 != null)
        return new Vector3(col2.bounds.center.x, worldY, col2.bounds.center.z);
    return new Vector3(t.position.x, worldY, t.position.z);
}
```

(주의: 기존 col 변수는 worldY 분기 안에서만 선언되므로 XZ 분기용으로 `col2` 재선언 또는 분기 외 선언으로 리팩토.)

### 3. 두 씬의 NoteDisplayPanel_Piano 인스턴스에 worldYOverride override 추가

`SampleScene.unity`, `TestSceneSanyo.unity` 두 파일 모두 PrefabInstance `&1229096218`의 m_Modifications 리스트에 다음 override를 추가한다 (위치: 기존 `panelHeight` override 다음, `m_RemovedComponents: []` 직전):

```yaml
    - target: {fileID: 7000000004, guid: a8b4f3c2d1e04a5b9c7e8f0d2a3b4c5d, type: 3}
      propertyPath: worldYOverride
      value: <원하는 Y값 — 예: 1.0>
      objectReference: {fileID: 0}
```

`<원하는 Y값>`은 사용자가 헤드셋에서 보면서 조정할 값이므로, 본 plan에서는 일단 `1.0` (현재 m_AnchoredPosition.y=1.225 worldScale 0.001 적용 후의 대략적 위치를 참조한 placeholder)로 박제하고, AC 검증 단계에서 사용자가 만족하는 값으로 확정한다.

### 4. NoteDisplayPanel.prefab 자체의 worldYOverride 기본값 = NaN 보장

prefab 파일 내 MonoBehaviour fileID `7000000004`에 `worldYOverride:` 필드를 신규 추가하지 않는다. Unity는 직렬화 시 누락된 직렬화 필드를 기본값(NaN)으로 자동 채워주므로 prefab은 건드릴 필요 없음. 단, 만약 Unity가 NaN을 0으로 직렬화하는 이슈가 발생하면 prefab에 명시적으로 `worldYOverride: .nan` 줄을 추가(YAML float NaN 표기).

### 5. 단위 테스트 추가

`Assets/RhythmGame/Tests/Editor/`에 `NoteDisplayPanelWorldYOverrideTests.cs` 추가:
- `WorldYOverride_WhenNaN_TransformYUnchanged`: NaN 기본값이면 Awake 후 transform.position.y가 원래 값 유지.
- `WorldYOverride_WhenSpecified_TransformYForced`: 1.5 지정 시 Awake 후 transform.position.y == 1.5.

`DrumNoteDisplayAdapterTests.cs` 추가 또는 확장:
- `ComputePanelPosition_WhenWorldYOverrideNaN_UsesColliderBounds`: 기존 경로 회귀.
- `ComputePanelPosition_WhenWorldYOverrideSpecified_UsesOverride`: override 값이 collider bounds 무시하고 사용됨.

(`ComputePanelPosition`은 private이므로 InternalsVisibleTo 또는 Reflection으로 접근. asmdef 변경 없이 `[VisibleForTesting]` 패턴 대신 메서드를 `internal`로 변경 검토.)

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — `worldYOverride` SerializeField + `ApplyWorldYOverride()` 메서드 추가, `Awake()`/`Show()`에서 호출.
- `Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` — `worldYOverride` SerializeField 추가, `ComputePanelPosition()` 수정.
- `Assets/Scenes/SampleScene.unity` — NoteDisplayPanel_Piano 인스턴스 (`&1229096218`)의 m_Modifications에 worldYOverride override 추가.
- `Assets/Scenes/TestSceneSanyo.unity` — 동일 override 추가.
- `Assets/RhythmGame/Tests/Editor/NoteDisplayPanelWorldYOverrideTests.cs` *(신규)* — NaN/값 지정 케이스 단위 테스트.
- (선택) `Assets/RhythmGame/Tests/Editor/DrumNoteDisplayAdapterTests.cs` *(신규 또는 확장)* — ComputePanelPosition override 분기 테스트.

## Acceptance Criteria

- [ ] `[auto-hard]` `NoteDisplayPanel.cs`에 `worldYOverride` SerializeField가 추가되어 있고 `float.NaN`이 기본값이다.
  **검증:** `Grep "worldYOverride" Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` 출력에 `[SerializeField] float worldYOverride = float.NaN` 패턴이 매칭된다.
- [ ] `[auto-hard]` `DrumNoteDisplayAdapter.cs`에 `worldYOverride` SerializeField가 추가되어 있고 `ComputePanelPosition()`이 NaN 아닐 때 collider bounds 대신 override 값을 사용한다.
  **검증:** `Grep "worldYOverride" Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` 매칭 + `Grep "float.IsNaN" Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` 매칭 (override 분기 존재 확인).
- [ ] `[auto-hard]` 두 씬 파일의 NoteDisplayPanel_Piano PrefabInstance(`&1229096218`)에 `worldYOverride` propertyPath override가 존재한다.
  **검증:** `Grep -n "worldYOverride" Assets/Scenes/SampleScene.unity` 와 `Grep -n "worldYOverride" Assets/Scenes/TestSceneSanyo.unity` 둘 다 `propertyPath: worldYOverride` 라인 매칭.
- [ ] `[auto-hard]` Editor 단위 테스트가 통과한다 — NaN일 때 transform.y 불변, 값 지정 시 강제 보정.
  **검증:** `unity-test-runner` 서브에이전트 호출 후 `NoteDisplayPanelWorldYOverrideTests` 모든 케이스 PASS. MCP 미가용 시 `MCP UNAVAILABLE` 보고 후 진행.
- [ ] `[auto-soft]` `DrumNoteDisplayAdapter.ComputePanelPosition` override 분기 단위 테스트 PASS.
  **검증:** `unity-test-runner`로 `DrumNoteDisplayAdapterTests.ComputePanelPosition_WhenWorldYOverrideSpecified_UsesOverride` PASS.
- [ ] `[manual-hard]` 헤드셋(또는 Editor Play 모드)에서 SampleScene을 열어 피아노 리듬게임 세션 시작 시 NoteDisplayPanel_Piano 패널이 worldYOverride 값(예: 1.0m)에 정확히 위치하고, 같은 씬을 다시 로드해도 동일 Y에 나타난다.
  **검증:** Play 모드 진입 → 피아노 잡기 → 리듬게임 세션 시작 → NoteDisplayPanel_Piano의 Inspector Transform.position.y가 worldYOverride 값과 일치, 곡 종료 후 재시작 시에도 동일.
- [ ] `[manual-hard]` 드럼 DrumNoteDisplayAdapter의 worldYOverride 필드에 임의 값(예: 1.2)을 지정하고 리듬게임 세션을 시작하면 모든 드럼 파츠 패널의 world Y가 그 값과 동일하다.
  **검증:** 드럼 Inspector에서 worldYOverride=1.2 설정 → Play → 모든 spawnedPanels의 transform.position.y == 1.2 (Hierarchy → Inspector 확인).
- [ ] `[manual-hard]` 동일 드럼 prefab에서 새 인스턴스를 씬에 배치(또는 세션 재시작)해도 worldYOverride 값이 유지되어 패널 Y가 이전과 동일하다.
  **검증:** DrumKit prefab을 씬에 두 번째 인스턴스로 드래그 → 두 인스턴스의 worldYOverride 모두 prefab 기본값으로 적용, 세션 시작 시 패널 Y 동일.

## Out of Scope

- X, Z 위치 조정 로직 변경 (sub-spec Out of Scope 답습).
- 패널 크기·스케일 변경.
- 드럼 이외 악기(트럼펫·하프 등)의 런타임 패널 생성 로직 변경.
- UI에서 사용자가 worldYOverride를 실시간 변경하는 기능 (Inspector 전용).
- worldYOverride 값을 사용자가 만족하는 최종값으로 확정(본 plan은 placeholder 1.0 사용, 사용자가 manual-hard AC에서 조정).

## Notes

- `float.NaN` sentinel 선택 이유: Unity Inspector에서 0이 유효한 world Y일 수 있어 0을 "미지정"으로 쓸 수 없다. NaN은 Unity float SerializeField에서 정상 직렬화되며(`.nan` YAML 표기) `float.IsNaN()`으로 안정 검사 가능.
- 후속 plan 후보: 사용자가 최종 Y값 확정 후 별도 commit으로 worldYOverride 값 업데이트.
- NoteDisplayPanel의 `Show()`는 `gameObject.SetActive(true)`를 호출하므로, 부모 Transform이 변동되어도 자기 transform.position이 재계산되지 않지만, parent active 상태 변경 직후 `ApplyWorldYOverride()` 재호출로 안전 보장.
- `ComputePanelPosition` 리팩토 시 기존 `col` 변수 scope를 if/else 위로 끌어올려 재사용 가능 — 컴파일 오류 회피 목적.

## Handoff

- **NoteDisplayPanel.cs**: `worldYOverride` SerializeField(NaN 기본값) + `ApplyWorldYOverride()` 추가. `Awake()`·`Show()` 양쪽에서 호출.
- **DrumNoteDisplayAdapter.cs**: 사용자 지시에 따라 `worldYOverride` 대신 `yOffset = -0.1f`(기본값 변경)로 단순화. `ComputePanelPosition()`은 `internal`로 변경(테스트 접근).
- **두 씬**: NoteDisplayPanel_Piano 인스턴스에 `worldYOverride: 0.85` override 적용.
- **DrumKit.prefab**: `yOffset: -0.1`, `_panelAnchor` → `(-0.5, 0.9, 0.2)` 설정.
- **테스트**: `NoteDisplayPanelWorldYOverrideTests`, `DrumNoteDisplayAdapterTests` 신규 추가(4/4 PASS).
- **주의**: AC #2(DrumNoteDisplayAdapter worldYOverride)는 yOffset 단순화로 대체. manual-hard 사용자 검증 완료(pass).
