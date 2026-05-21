# Drum Note Panel Rotation Fix — PanelAnchor 기반 고정 회전 1회 계산

**Linked Spec:** [`11-drum-note-panel-rotation-fix.md`](../specs/11-drum-note-panel-rotation-fix.md)
**Status:** `Done`

## Goal

`DrumNoteDisplayAdapter.Init()`에서 각 드럼 파츠 노트 패널 생성 시 `BillboardUI` 컴포넌트 부착을 제거하고, 그 자리에 `host.PanelAnchor`의 수평 forward 방향을 기준으로 `Quaternion`을 1회 계산해 `panel.transform.rotation`에 직접 설정한다. 결과적으로 세션 진행 중 사용자(카메라)가 이동·회전해도 드럼 노트 패널의 방향은 변하지 않는다.

## Context

Sub-spec 11의 What 요건은 두 가지:

1. 드럼 노트 UI 패널의 회전은 세션 시작 시 DrumKit의 `_panelAnchor` 방향을 기준으로 1회만 계산되어 고정된다.
2. 세션 진행 중 사용자(카메라)가 이동·회전해도 각 드럼 파츠의 노트 패널은 회전하지 않는다.

현재 `DrumNoteDisplayAdapter.Init()`은 패널 생성 직후 `panel.gameObject.AddComponent<BillboardUI>().tiltDegrees = panelTiltDegrees;`로 `BillboardUI`를 부착하고 있다. `BillboardUI.LateUpdate()`는 매 프레임 `Camera.main`의 수평 방향(`dir.y = 0f`)을 향해 `transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f)`로 회전을 재설정한다. 따라서 사용자가 움직이면 패널이 따라 회전한다.

해법: `InstrumentBase.PanelAnchor`가 이미 DrumKit prefab의 `PanelAnchor` GameObject(localPosition `(-0.5, 0.9, 0.2)`)에 wiring되어 있으므로, 그 Transform의 forward를 수평 성분(`dir.y = 0`)으로 평탄화해 `Quaternion.LookRotation(...) * Quaternion.Euler(-panelTiltDegrees, 0f, 0f)`을 1회 계산하고 `panel.transform.rotation`에 직접 부여한다. `BillboardUI` 컴포넌트는 부착하지 않으므로 LateUpdate 회전 갱신이 발생하지 않고, 패널 방향은 세션 종료까지 불변이다.

`BillboardUI` 컴포넌트 자체는 피아노 측 `NoteDisplayPanel.Awake()`에서 `panelTiltDegrees > 0f`일 때 부착하는 경로가 별도로 존재하므로(sub-spec 10 handoff·Read 확인), 본 plan은 BillboardUI 클래스 자체는 건드리지 않는다. Out of Scope 룰 3번("BillboardUI 자체 다른 목적 재사용 동작 변경") 답습.

## Verified Structural Assumptions

- `DrumNoteDisplayAdapter.cs`는 `Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs`, namespace `RhythmGame.Runtime`. `Init()` 루프 안 line 73에서 `panel.gameObject.AddComponent<BillboardUI>().tiltDegrees = panelTiltDegrees;`로 BillboardUI 부착. `Begin()`에서 `InstrumentBase host = GetComponent<InstrumentBase>() ?? GetComponentInParent<InstrumentBase>()`로 host 획득. — `Read Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs (2026-05-20)`
- `BillboardUI.cs`는 `Assets/RhythmGame/Scripts/Runtime/Display/BillboardUI.cs`, namespace `RhythmGame.Runtime`. `LateUpdate()`마다 `Camera.main` 방향으로 `transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f)`로 회전을 재설정. **사용자 이동 시 매 프레임 패널이 회전**. `tiltDegrees` public 필드 기본값 30f, `dir.sqrMagnitude < 0.0001f`이면 회전 갱신 skip. 다른 호출처: `NoteDisplayPanel.cs:75-80`(피아노)에서만 `panelTiltDegrees > 0f`일 때 부착 — 본 plan은 피아노 측 경로 무관. — `Read Assets/RhythmGame/Scripts/Runtime/Display/BillboardUI.cs (2026-05-20)` + `Grep BillboardUI` (사용처 = DrumNoteDisplayAdapter, NoteDisplayPanel 두 곳)
- `InstrumentBase.PanelAnchor`는 `Assets/Instruments/_Core/Scripts/InstrumentBase.cs:56` `public Transform PanelAnchor => _panelAnchor != null ? _panelAnchor : transform;`. `_panelAnchor` 미설정 fallback은 InstrumentBase가 부착된 root transform 자체. — `Read Assets/Instruments/_Core/Scripts/InstrumentBase.cs (2026-05-20)`
- `DrumKit.prefab` (`Assets/Instruments/Drum/Prefabs/DrumKit.prefab`)의 `_panelAnchor`는 fileID `1479758216802493830`(GameObject 이름 `PanelAnchor`, sub-spec 10 plan에서 설정됨)에 연결되어 있고 Transform `m_LocalPosition: (-0.5, 0.9, 0.2)`, `m_LocalRotation: identity`, `m_LocalScale: (0.6667, 0.6667, 0.6667)`, m_Father는 DrumKit root. PanelAnchor의 local forward는 root local 좌표계에서 `(0, 0, 1)`이며 root rotation에 의해 world forward가 결정. — `Read Assets/Instruments/Drum/Prefabs/DrumKit.prefab (2026-05-20)` lines 288-302
- 신규 `using` import 없음(이미 `Instruments` namespace 사용 중, `UnityEngine.Quaternion`도 기존 import). `Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef`는 `Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime.Clock`, `Unity.InputSystem` references 포함 — 추가 reference 불필요. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef (2026-05-20)`
- BillboardUI 호출 외부 API side effect: `LateUpdate()`만 가지며 `OnEnable/OnDisable/Awake/Start` 없음, frame-level loop 영향은 LateUpdate 회전 갱신뿐. `tiltDegrees` 공개 필드 외 상태 없음. **즉, `AddComponent<BillboardUI>()`를 제거해도 `BillboardUI`측에서 다른 transform/world pose/event side effect는 없음**. — `Read Assets/RhythmGame/Scripts/Runtime/Display/BillboardUI.cs (2026-05-20)` 전체

## Approach

### 1. `DrumNoteDisplayAdapter.Init()` 회전 로직 교체

`Init()` 진입 직후, hitZone 루프 시작 전에 `host`의 `PanelAnchor`를 한 번 읽어 고정 회전 `panelRotation: Quaternion`을 계산한다. (host는 `Begin()`이 `Init()`을 호출하므로, `Init()` 내부에서도 동일 방식으로 재획득. `Init()`이 외부에서 직접 호출될 가능성 보존.)

```csharp
public void Init(InstrumentLaneConfig config, VmSongChart chart, int judgedChannel, IRhythmClock clock)
{
    Hide();
    if (config == null || noteDisplayPanelPrefab == null) return;

    // 고정 회전 1회 계산 (사용자 이동 무관)
    InstrumentBase host = GetComponent<InstrumentBase>() ?? GetComponentInParent<InstrumentBase>();
    Quaternion panelRotation = ComputePanelRotation(host);

    DrumHitZone[] hitZones = GetComponentsInChildren<DrumHitZone>(includeInactive: true);
    HashSet<byte> processedNotes = new HashSet<byte>();

    foreach (DrumHitZone zone in hitZones)
    {
        // ... 기존 매칭 로직 ...

        Vector3 worldPos = ComputePanelPosition(zone.transform, zone.PanelYOffset);
        NoteDisplayPanel panel = Instantiate(noteDisplayPanelPrefab);
        panel.transform.position = worldPos;
        panel.transform.rotation = panelRotation;   // ★ BillboardUI 제거, 회전 직접 부여

        panel.SetLaneConfig(singleConfig);
        panel.Show(chart, judgedChannel, clock);
        spawnedPanels.Add(panel);
        noteToPanel[note] = panel;
    }
    // ... 나머지 _pendingPanelCount 로직 기존 그대로 ...
}
```

`AddComponent<BillboardUI>` 호출 1줄 삭제. `panel.transform.rotation = panelRotation;` 1줄 추가.

### 2. `ComputePanelRotation(InstrumentBase host)` 신규 private 메서드

`PanelAnchor.forward`를 가져와 y성분 0으로 평탄화한 다음 `LookRotation`으로 base rotation을 만들고, 거기에 `Euler(-panelTiltDegrees, 0f, 0f)`을 곱해 BillboardUI와 동일한 틸트 방식을 유지한다.

```csharp
internal Quaternion ComputePanelRotation(InstrumentBase host)
{
    // host가 없거나 PanelAnchor 방향이 수평 0벡터면 identity로 폴백(기존 prefab world rotation 유지)
    if (host == null) return Quaternion.identity;
    Transform anchor = host.PanelAnchor;
    if (anchor == null) return Quaternion.identity;

    Vector3 forward = anchor.forward;
    forward.y = 0f;
    if (forward.sqrMagnitude < 0.0001f) return Quaternion.identity;

    return Quaternion.LookRotation(forward.normalized, Vector3.up)
         * Quaternion.Euler(-panelTiltDegrees, 0f, 0f);
}
```

`internal` 가시성을 부여해 sub-spec 10 plan의 `ComputePanelPosition`과 동일 패턴으로 Editor 테스트에서 접근 가능.

**의미상 PanelAnchor.forward를 쓰는 이유:** DrumKit prefab의 `PanelAnchor`는 `_panelAnchor`로 박제되어 있고 localRotation identity·local Z+가 곧 "사용자가 드럼 앞에 섰을 때 바라보는 방향"이라는 의미를 가진다(sub-spec 11 Why 의 "기준 앵커"). PanelAnchor를 회전시키면 패널 회전도 따라 갱신되므로 디자이너가 Inspector에서 통제 가능. PanelAnchor 미설정 시 `InstrumentBase.PanelAnchor`가 root transform으로 fallback하므로 root forward를 그대로 사용.

### 3. `panelTiltDegrees` 필드 보존

`panelTiltDegrees` SerializeField는 그대로 유지(기존 prefab 직렬화 호환). BillboardUI에 넘기는 게 아니라 `ComputePanelRotation` 내부 `Quaternion.Euler(-panelTiltDegrees, 0f, 0f)`에서 직접 사용.

### 4. 단위 테스트 추가

`Assets/RhythmGame/Tests/Editor/DrumNoteDisplayAdapterTests.cs`(sub-spec 10에서 신규 생성됨)에 케이스 추가:

- `ComputePanelRotation_WhenHostNull_ReturnsIdentity`: host=null → `Quaternion.identity` 반환.
- `ComputePanelRotation_WhenAnchorForwardHorizontal_ReturnsLookRotationWithTilt`: 임의 PanelAnchor를 만들어 forward를 `(1, 0, 0)`으로 회전시킨 뒤 ComputePanelRotation 호출 → 결과의 forward가 `(1, 0, 0)`을 기준으로 한 LookRotation+tilt 결과와 일치(Quaternion.Angle < 0.01f).
- `ComputePanelRotation_WhenAnchorForwardVerticalOnly_ReturnsIdentity`: PanelAnchor를 90도 위로 회전시켜 forward를 `(0, 1, 0)`로 만든 경우(평탄화 후 0벡터) → `Quaternion.identity` 반환.
- `Init_WhenCalled_PanelsDoNotHaveBillboardUI`: NoteDisplayPanel prefab을 임시 GameObject로 mock하고 Init 호출 → `panel.GetComponent<BillboardUI>() == null` 확인. 또한 회귀 안전망으로 spawn된 패널이 `panel.transform.rotation`을 1회만 받았다는 사실은 LateUpdate 미발생 의미로 충분(BillboardUI 컴포넌트 부재 = LateUpdate 회전 없음).

테스트가 NoteDisplayPanel·DrumHitZone·InstrumentBase에 의존하므로 본격적인 Init 통합 테스트는 어려움 — 위 4개 중 첫 3개(ComputePanelRotation 순수 함수)만 단위 테스트로 작성하고, 마지막 1개는 manual-hard로 위임.

### 5. 컴파일 후 Unity 회귀

`unity-test-runner` 서브에이전트를 1회 호출해 `RhythmGame.Tests.Editor` 어셈블리 전체 PASS 확인. MCP 미가용 시 `MCP UNAVAILABLE` 보고 후 진행(`reviewer` 진행 룰 답습).

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` — `Init()`에서 `AddComponent<BillboardUI>` 제거 + `panel.transform.rotation = panelRotation;` 1줄 추가, `ComputePanelRotation(InstrumentBase)` internal 메서드 신규.
- `Assets/RhythmGame/Tests/Editor/DrumNoteDisplayAdapterTests.cs` *(확장)* — `ComputePanelRotation` 3개 케이스 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` `DrumNoteDisplayAdapter.cs`에서 `AddComponent<BillboardUI>` 호출이 제거되었다.
  **검증:** `Grep "AddComponent<BillboardUI>" Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` 결과가 0건이다.
- [ ] `[auto-hard]` `DrumNoteDisplayAdapter.cs`에 `ComputePanelRotation` 메서드가 추가되어 있고, `Init()`에서 그 결과를 `panel.transform.rotation`에 할당한다.
  **검증:** `Grep "ComputePanelRotation" Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` 매칭 + `Grep "panel.transform.rotation" Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs` 매칭 (`Init()` 안에 1건 이상).
- [ ] `[auto-hard]` `ComputePanelRotation`이 `PanelAnchor.forward`의 수평 성분 + `panelTiltDegrees` Euler로 합성된다 (런타임 정합).
  **검증:** Editor 단위 테스트 `ComputePanelRotation_WhenAnchorForwardHorizontal_ReturnsLookRotationWithTilt` PASS — 임의 forward `(1, 0, 0)` 입력에 대해 결과 Quaternion이 `LookRotation((1,0,0), Vector3.up) * Euler(-panelTiltDegrees, 0, 0)`과 `Quaternion.Angle < 0.01f`.
- [ ] `[auto-hard]` `ComputePanelRotation`이 host=null 또는 PanelAnchor.forward 수평 성분이 0인 경우 `Quaternion.identity`로 폴백한다.
  **검증:** Editor 단위 테스트 `ComputePanelRotation_WhenHostNull_ReturnsIdentity`, `ComputePanelRotation_WhenAnchorForwardVerticalOnly_ReturnsIdentity` 모두 PASS.
- [ ] `[auto-soft]` `unity-test-runner` 회귀에서 `RhythmGame.Tests.Editor` 어셈블리 모든 케이스 PASS (기존 sub-spec 10 테스트 포함 회귀 없음).
  **검증:** `unity-test-runner` 서브에이전트 호출 결과 보고 — PASS / MCP UNAVAILABLE 중 하나.
- [ ] `[manual-hard]` Play 모드에서 SampleScene을 열어 드럼을 잡고 리듬게임 세션을 시작한 뒤 사용자(헤드셋 또는 Editor 카메라)를 좌우로 이동·회전시켜도 모든 드럼 파츠 노트 패널의 world rotation이 변하지 않는다.
  **검증:** Play 진입 → 드럼 anchor 텔레포트 → 세션 시작 → spawnedPanels 중 하나의 Inspector Transform.rotation을 기록 → 사용자 위치/회전 변경 → 같은 패널의 Transform.rotation이 동일(Quaternion 4성분 모두 ε 이내).
- [ ] `[manual-hard]` 세션 시작 시 드럼 노트 패널이 `_panelAnchor`의 forward 방향을 향하고 `panelTiltDegrees`만큼 상단이 뒤로 기울어진 상태로 표시된다 (기존 BillboardUI 시각과 동등하되 카메라 의존 없음).
  **검증:** 드럼 세션 시작 직후 헤드셋 시점에서 패널이 `_panelAnchor` 정면 + 기존과 동일한 틸트 각도로 보이는지 시각 확인.

## Out of Scope

- 피아노 `NoteDisplayPanel.Awake()`의 `BillboardUI` 부착 경로 변경 (sub-spec 11 Out of Scope 1번 답습).
- 드럼 판정선·노트 레이아웃 자체 변경 (sub-spec 11 Out of Scope 2번 답습).
- `BillboardUI.cs` 클래스 자체 삭제·수정 (sub-spec 11 Out of Scope 3번 — 피아노 측이 여전히 사용).
- `_panelAnchor`의 localRotation 변경(현재 identity 유지). 디자이너가 PanelAnchor를 회전시키면 자동으로 패널 방향에 반영됨 — 별도 plan 불필요.
- `panelTiltDegrees`를 0으로 설정해 완전 수직 패널을 만드는 시나리오 — 동작은 자동 지원되나 사용자 검증 대상 아님.

## Notes

- `ComputePanelRotation`을 `internal`로 선언하는 이유: sub-spec 10 plan에서 `ComputePanelPosition`도 동일하게 `internal`로 변경한 전례. Editor 어셈블리에 `InternalsVisibleTo` 또는 직접 internal 접근으로 단위 테스트 작성.
- `PanelAnchor.forward`를 평탄화한 뒤 `sqrMagnitude < 0.0001f`이면 identity로 폴백하는 가드는 `BillboardUI.LateUpdate()`의 동일 가드(`dir.sqrMagnitude < 0.0001f`)와 일치시켜 동작 동등성 유지.
- 후속 plan 후보: 사용자 시각 검증 후 `panelTiltDegrees` 기본값(50f) 조정이 필요하면 별도 commit.

## Handoff

- DrumNoteDisplayAdapter.Init()에서 AddComponent<BillboardUI> 제거 완료. 대신 ComputePanelRotation(Transform anchor)을 1회 호출해 panelRotation을 계산하고 panel.transform.rotation에 직접 설정.
- ComputePanelRotation은 `-anchor.forward`(DrumKit 앞쪽 방향을 플레이어 방향으로 반전)를 기반으로 `LookRotation * Euler(-panelTiltDegrees, 0, 0)` 산출.
- DrumKit prefab의 panelTiltDegrees를 50→0으로 조정 (수직 패널, 사용자 요청).
- DrumNoteDisplayAdapterTests에 ComputePanelRotation 3개 케이스(null/수평/수직) 추가. 기존 2개 ComputePanelPosition 케이스도 _computePositionMethod 이름으로 정리.
