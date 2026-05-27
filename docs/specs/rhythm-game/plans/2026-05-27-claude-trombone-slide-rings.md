# Trombone 슬라이드 포지션 7링 — attach 게이팅 + HDR Grip 강조

**Linked Spec:** [`15-trombone-slide-position-rings.md`](../specs/15-trombone-slide-position-rings.md)
**Status:** `Done`

## Goal

기존 `SlidePositionMarkers`(이미 prefab에 부착되어 7개 색 링을 spawn 중)에 (a) `TromboneAnchor.IsAttached` 폴링 기반 show/hide 게이팅과 (b) `TromboneSlideController.IsGripHeld` + `SlideIndex` 폴링 기반 현재 슬라이드 링 HDR 강조 로직을 더한다. 이를 위해 `TromboneSlideController`에 `public bool IsGripHeld` 한 줄을 노출한다. sub-spec 15의 6개 Behavior(트롬본 잡으면 링 표시, 슬라이드 이동해도 링 고정, 오른손 Grip 동안 현재 링 HDR 강조, Grip 변경 시 강조 이동, Grip 해제 시 기본 밝기 복원, 트롬본 놓으면 링 제거)를 한 plan에서 모두 회수.

## Context

sub-spec 15는 트롬본을 잡는 순간 슬라이드 7포지션에 색 링을 띄우고, 오른손 Grip을 누르는 동안 현재 `SlideIndex` 링만 HDR 밝기로 강조하라고 요구한다. 색은 `TromboneSlideColors.Palette`(이미 존재, 보라→빨강 7색)를 그대로 사용 — Tech Spec Invariant: "기본 링 색상은 `TromboneSlideColors.Palette[i]`와 동일".

현재 상태 (2026-05-27, prefab MCP 검증):
- `Trombone/Rig/SlidePositionMarkers` GameObject가 이미 prefab에 있고 `Instruments.SlidePositionMarkers` 컴포넌트가 부착돼 있다. 자식으로 `SlideMarker_0`~`SlideMarker_6` 7개(MeshFilter+MeshRenderer)가 이미 spawn돼 있다 — torus 메쉬 + 각 슬라이드 색이 박힌 상태.
- 그러나 `SlidePositionMarkers`는 `OnEnable`에서 한 번 `Rebuild()` 후 별도 attach 게이팅 없이 항상 보이고, `LateUpdate`도 없어 grip 기반 HDR 강조도 없다. 즉 sub-spec 15의 Behavior 6건 중 (a) 색·위치 spawn은 만족, (b) attach 게이팅·HDR 강조 4건은 미충족.
- `TromboneSlideController`의 `m_IsGripHeld`는 private 필드라 외부에서 폴링할 방법이 없다.

Tech Spec(`tech-specs/15-...md`) 결정:
- Components: SlidePositionMarkers(기존, HDR 강조 로직 추가) + TromboneSlideController(API 추가, `IsGripHeld` public property) + TromboneAnchor(기존, `IsAttached` 그대로).
- Data/Control Flow: LateUpdate 매 프레임 `IsAttached` 변화 감지 → 링 SetActive 토글; `IsGripHeld + SlideIndex` 폴링 → activeIndex 변경 시 해당 링 `MeshRenderer.material._BaseColor`를 HDR 값으로 갱신, 이전 활성 링·Grip 해제 시 기본 색으로 복원.
- Boundaries: 건드림 = `SlidePositionMarkers.cs`(HDR 강조 추가) + `TromboneSlideController.cs`(`IsGripHeld` 추가). 건드리지 않음 = 링 Mesh/Torus 생성 로직 / TromboneAnchor / RhythmGame 판정 / NoteDisplayPanel / TromboneNoteDisplayAdapter.
- Invariants: 링 수 = 7 = `SlidePositionCount`; 기본 색 = `Palette[i]`; 강조 링 동시 최대 1개; `IsGripHeld == false`이면 모든 링 기본 밝기.

ARD `decisions/03-slide-ring-grip-detection.md` (Accepted): "IsGripHeld 프로퍼티 폴링" 채택 (이벤트 기반 옵션 기각). `TromboneBodyVisualSnap`가 `partialController.PartialIndex`를 폴링하는 패턴과 동일. Consequences: (i) `TromboneSlideController`에 `public bool IsGripHeld => m_IsGripHeld;` 추가, (ii) `SlidePositionMarkers`가 `TromboneSlideController` 참조를 획득해 매 프레임 `IsGripHeld` + `SlideIndex`를 폴링.

`Rebuild()` 동작 박제(주의): 현 구현은 각 마커 spawn 시 `Material mat = new Material(template);`로 마커별 material 인스턴스를 만든다 — 즉 마커마다 독립 material이라 한 마커의 `_BaseColor`를 바꿔도 다른 마커에 영향이 없다. HDR 강조 로직은 마커별 material 인스턴스의 `_BaseColor`를 직접 SetColor(name, hdrColor)하면 된다. 단, `markerMaterialTemplate`이 인스펙터에 미할당이면 `Shader.Find("Universal Render Pipeline/Unlit")` fallback이 만들어진다 — URP/Unlit의 `_BaseColor`는 HDR 값(>1f 채널)을 받아들이지만 emission이 따로 없어 "밝게 보이는" 시각 효과는 채널 값 자체에 의존한다. 본 plan은 강조 시 색 채널에 `baseColor * intensityMultiplier` (예: 4.0배)를 적용 — URP/Unlit이라도 channel 값이 1을 넘으면 bloom/Tonemap 후 더 밝게 시현된다. URP Bloom이 씬에 설정돼 있다면 가시적 효과가 두드러진다 (씬 post 설정은 본 plan 범위 밖).

`ClearSpawned` + `Rebuild`가 `_spawned` 리스트를 매번 비우고 다시 만든다. 본 plan의 HDR 강조 로직은 _spawned 리스트의 각 GameObject에서 MeshRenderer를 캐시하고, 각 인덱스의 "기본 material 색"(=`Palette[i]`)을 별도 기록해 toggle 시 복원에 사용한다. Rebuild가 다시 호출되면 캐시도 무효화 → `OnEnable`/Rebuild 끝에서 캐시 재생성.

attach 게이팅 결정: Tech Spec은 "LateUpdate 매 프레임 IsAttached 변화 감지 → 링 SetActive 토글"이라고 명시. 본 plan은 `SlidePositionMarkers` GameObject(부모) 자체가 항상 활성이되, 각 자식(`SlideMarker_i`)을 `IsAttached`에 따라 SetActive(true/false)한다. 부모 자체를 비활성하면 `SlidePositionMarkers.LateUpdate`가 안 돌아 attach 시 다시 켤 트리거가 사라지므로 자식만 토글하는 게 안전. `IsAttached`가 변화한 프레임에만 토글하도록 `m_LastIsAttached`로 디바운스해 매 프레임 SetActive 호출을 피한다.

기존 `[ExecuteAlways]` 어트리뷰트 박제: 현 컴포넌트는 `[ExecuteAlways]`로 에디터에서도 Rebuild가 돌아 prefab inspector에서 링이 미리 보이게 돼 있다. 본 plan의 LateUpdate 게이팅 로직은 `Application.isPlaying`일 때만 동작해야 한다 — 에디터 모드에서 `TromboneAnchor.IsAttached`는 항상 false라 링이 다 사라지면 prefab/scene 작업이 어렵다. `if (!Application.isPlaying) return;` 가드 1줄.

호출 외부 API side effect(`TromboneSlideController.IsGripHeld` 신설):
- `Read Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs (2026-05-27)` 전체. `m_IsGripHeld`는 `LateUpdate`에서 attach 해제 시 false로 즉시 강제 셋 / grip rising/falling edge에서 토글. 외부 게터 추가만으로는 frame 동작·write timing 변경이 0이므로 `Trombone.cs` 등 다른 호출자에게 부작용 없음 (현재 호출자도 없음).
- `SlideIndexChanged` 이벤트가 이미 public이지만 본 plan은 폴링 방식을 채택(ARD 03) → 이벤트는 미사용. 사이드이펙트 0.

## Verified Structural Assumptions

- **Trombone.prefab 구조** — `Trombone/Rig/SlidePositionMarkers` GameObject가 prefab 내 존재. `Instruments.SlidePositionMarkers` 컴포넌트 부착. 자식 `SlideMarker_0`~`SlideMarker_6` 7개가 각각 `Transform + MeshFilter + MeshRenderer` 구성. 자식 component types에 별도 스크립트 없음(렌더만). 직렬화 위치: `m_Name: SlidePositionMarkers` (line 1664), `MonoBehaviour m_EditorClassIdentifier: Instruments::Instruments.SlidePositionMarkers` (line 1696), `slideController: {fileID: 4069711244863020258}`(line 1697)로 이미 `TromboneSlideController` 참조 박혀 있음. localPosition `(0.397, -0.067, 0.056)`, localScale `(1, 0.3, 0.3)` (line 1679-1680). — `Unity MCP manage_prefabs.get_hierarchy prefab_path="Assets/Instruments/Trombone/Prefabs/Trombone.prefab" (2026-05-27)` + `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-27) lines 1655-1703`
- **`SlidePositionMarkers.cs` 현 상태** — `OnEnable→Rebuild()`로 7개 자식 GameObject를 `hideFlags: DontSave`로 spawn, 마커별 `new Material(template)` 인스턴스 + `Palette[i]` 색 적용 후 `_spawned` 리스트에 push. `LateUpdate` 없음 — 즉 attach 게이팅·강조 로직 부재. `SetMaterialColor`(static)는 `_BaseColor`(URP/Unlit) / `_Color`(Built-in) / `mat.color` 3중 set. `[ExecuteAlways]` + `OnValidate→EditorApplication.delayCall→Rebuild`로 에디터에서도 회전 가능. — `Read Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs (2026-05-27)` 전체
- **`TromboneSlideController.cs` 박제** — `m_IsGripHeld` private bool 필드(line 30). `LateUpdate`에서 (i) `tromboneAnchor.IsAttached == false`면 즉시 `m_IsGripHeld = false` 강제(line 60), (ii) attach 시 grip rising edge(`grip && !m_IsGripHeld`)·falling edge(`!grip && m_IsGripHeld`)에서 토글(line 69-78). `SlideIndex` public (line 36). `SlideIndexChanged` public event 이미 존재(line 39). 외부에 IsGripHeld 노출 = read-only property 1줄 추가, write timing 변경 0. — `Read Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs (2026-05-27)` lines 30, 36, 39, 60, 69-78
- **`TromboneAnchor.IsAttached` 박제** — public bool getter (line 35), `AttachTromboneToMouth` 끝에서 `m_IsAttached = true`(line 165), `Detach` 끝에서 `m_IsAttached = false`(line 217). attach/detach는 locomotion 이벤트 기반이라 frame 단위 토글이 일반적. SlidePositionMarkers의 LateUpdate가 같은 프레임 안에 변화를 감지(execution order 10004 < TromboneAnchor 10005). — `Read Assets/Instruments/Trombone/Scripts/TromboneAnchor.cs (2026-05-27)` lines 35, 148-166, 205-218
- **`TromboneSlideColors.Palette`** — `static readonly Color[7]`, public. 인덱스 0=보라 `(0.55, 0, 0.85)` → 6=빨강 `(1, 0, 0)`. 본 plan의 HDR 강조는 `Palette[i]` × intensityMultiplier(>1)을 마커 material `_BaseColor`에 설정. URP/Unlit `_BaseColor`는 HDR 채널을 받음. — `Read Assets/Instruments/Trombone/Scripts/TromboneSlideColors.cs (2026-05-27)` 전체
- **`TromboneBodyVisualSnap` 폴링 패턴** — 동일 도메인 sibling. `LateUpdate`에서 `partialController.PartialIndex` 폴링 + `m_LastPartialIndex` 캐시로 변화 감지. ARD 03이 가리키는 "동일 폴링 패턴" 참조. — `Read Assets/Instruments/Trombone/Scripts/TromboneBodyVisualSnap.cs (2026-05-27)` lines 24, 50-62
- **asmdef 의존** — `SlidePositionMarkers.cs`/`TromboneSlideController.cs`/`TromboneAnchor.cs` 모두 `Assets/Instruments/Trombone/Scripts/` 아래 → `Instruments` 어셈블리. `Instruments.asmdef`는 `Unity.InputSystem` / `Hands` / `Unity.XR.Interaction.Toolkit` 참조. 본 plan은 (a) 기존 컴포넌트 1개에 public 속성 1줄, (b) 기존 컴포넌트 1개에 LateUpdate 로직 추가만 — 신규 namespace import 0건 → asmdef reference 추가 불필요. — `Read Assets/Instruments/Instruments.asmdef (2026-05-27)`

## Approach

1. **`TromboneSlideController`에 `IsGripHeld` public property 추가** — `Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs`. `public int SlideIndex => m_SlideIndex;` (line 36) 근처에 한 줄 추가:
   ```csharp
   public bool IsGripHeld => m_IsGripHeld;
   ```
   기존 `m_IsGripHeld` 필드의 write 시점 변경 없음 — 단순 read-only getter.

2. **`SlidePositionMarkers`에 HDR 강조 + attach 게이팅 로직 추가** — `Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs`:
   - **신규 SerializeField 2건**:
     ```csharp
     [SerializeField] TromboneAnchor tromboneAnchor;
     [Tooltip("HDR 강조 시 기본 색에 곱할 강도. URP Bloom과 결합해 발광 효과를 낸다.")]
     [SerializeField, Min(1f)] float highlightIntensityMultiplier = 4f;
     ```
     `tromboneAnchor`는 prefab inspector wiring (또는 `GetComponentInParent<TromboneAnchor>()` null 폴백). `slideController`는 이미 wired됨(line 1697).
   - **신규 캐시 필드**:
     ```csharp
     readonly List<MeshRenderer> _spawnedRenderers = new List<MeshRenderer>();
     readonly List<Color> _baseColors = new List<Color>();  // _spawned 와 1:1 인덱스
     bool _lastIsAttached;
     int _lastHighlightedIndex = -1;
     ```
   - **`Rebuild()` 갱신**: 마커 spawn 시 `_spawnedRenderers.Add(mr)` + `_baseColors.Add(c)` 캐시. `ClearSpawned()` 시 두 리스트도 `Clear()`. 캐시 갱신 외 기존 spawn 로직 무변경.
   - **신규 `LateUpdate` 추가**:
     ```csharp
     void LateUpdate()
     {
         if (!Application.isPlaying) return;   // 에디터 모드에서는 게이팅 비활성 — prefab/scene 작업 가시성 유지

         TromboneAnchor anchor = tromboneAnchor != null ? tromboneAnchor : GetComponentInParent<TromboneAnchor>();
         bool attached = anchor != null && anchor.IsAttached;

         // attach 변화 시에만 자식 SetActive 토글 (매 프레임 호출 회피).
         if (attached != _lastIsAttached)
         {
             for (int i = 0; i < _spawned.Count; i++)
                 if (_spawned[i] != null) _spawned[i].SetActive(attached);
             _lastIsAttached = attached;

             // 트롬본 놓을 때 강조 상태도 초기화
             if (!attached)
             {
                 RestoreHighlight(_lastHighlightedIndex);
                 _lastHighlightedIndex = -1;
             }
         }

         if (!attached) return;

         // grip 폴링 → desired highlight index 산출
         TromboneSlideController controller = slideController != null
             ? slideController
             : GetComponentInParent<TromboneSlideController>();
         int desiredIdx = (controller != null && controller.IsGripHeld) ? controller.SlideIndex : -1;

         if (desiredIdx != _lastHighlightedIndex)
         {
             RestoreHighlight(_lastHighlightedIndex);
             ApplyHighlight(desiredIdx);
             _lastHighlightedIndex = desiredIdx;
         }
     }

     void ApplyHighlight(int idx)
     {
         if (idx < 0 || idx >= _spawnedRenderers.Count) return;
         MeshRenderer mr = _spawnedRenderers[idx];
         if (mr == null) return;
         Color baseC = _baseColors[idx];
         Color hdr = baseC * highlightIntensityMultiplier;
         hdr.a = baseC.a;  // 알파는 유지
         SetMaterialColor(mr.sharedMaterial, hdr);
     }

     void RestoreHighlight(int idx)
     {
         if (idx < 0 || idx >= _spawnedRenderers.Count) return;
         MeshRenderer mr = _spawnedRenderers[idx];
         if (mr == null) return;
         SetMaterialColor(mr.sharedMaterial, _baseColors[idx]);
     }
     ```
   - **`OnEnable` 보정**: `_lastIsAttached = false; _lastHighlightedIndex = -1;` 초기화 후 `Rebuild()`. attach 게이팅 첫 프레임에 자동으로 SetActive(false)로 시작되도록.
   - **`OnDisable` 보정**: 강조 인덱스 복원(`RestoreHighlight(_lastHighlightedIndex)`) 후 `ClearSpawned()`. domain reload·prefab 닫기 시 잔여 HDR 색이 다음 spawn 시 base color로 들어가지 않게 안전 복원.
   - **`Rebuild()` 끝에서 attach 게이팅 초기 상태 적용**: spawn 직후 `Application.isPlaying`이고 attach != true면 모든 자식 `SetActive(false)`. `[ExecuteAlways]`로 에디터 Rebuild가 돌 때는 SetActive(true) 그대로(에디터 가시성 유지).

3. **prefab `tromboneAnchor` slot wiring** — Unity MCP `manage_prefabs.modify_contents` 또는 텍스트 Edit으로 `SlidePositionMarkers` MonoBehaviour 블록(line 1685-1703)에 `tromboneAnchor: {fileID: <TromboneAnchor 컴포넌트 fileID>}` 추가. TromboneAnchor 컴포넌트의 fileID는 `Grep -n "Instruments.TromboneAnchor" Assets/Instruments/Trombone/Prefabs/Trombone.prefab`로 확인 후 박제. MCP 사용 우선, fallback은 텍스트 Edit. `highlightIntensityMultiplier`는 코드 기본값 `4f`로 자동 직렬화 (Unity SerializeField 신규 필드 초기 직렬화 규칙).

4. **컴파일 + 회귀 검증** — `unity-mcp-workflow` skill의 컴파일 대기·`read_console types=["error"]` 절차로 errors 0 확인. 본 plan은 API surface 추가가 read-only property 1개 + 신규 SerializeField 2개 + `LateUpdate` 1개로 회귀 비용 최소. `unity-test-runner` sub-agent 1회 호출해 EditMode 회귀 PASS 확인. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 진행.

5. **수동 재현 시나리오 박제** — `TestSceneSanyo` Play Mode에서:
   - (a) 트롬본 anchor 진입 전: 링 7개가 보이지 않음 (자식 SetActive false).
   - (b) 트롬본 anchor 진입(attach) 직후: 링 7개가 trombone Rig를 따라 등장, `Palette[0..6]` 색 그대로.
   - (c) 슬라이드 이동(왼손이 트롬본 잡은 상태에서 오른손으로 Slide 끌어당김)해도 링 7개 world 위치 변화 없음 (링은 SlidePositionMarkers `transform` 자식이고, SlidePositionMarkers는 Rig의 자식 — Rig 회전/이동에는 따라가지만 `Slide` 본체 이동과는 독립).
   - (d) 오른손 Grip 누르는 동안 현재 SlideIndex 링이 다른 링보다 시각적으로 밝게(HDR 채널 4배). 슬라이드 이동으로 `SlideIndex`가 바뀌면 새 위치 링이 강조되고 이전 링은 기본 색.
   - (e) 오른손 Grip 해제: 모든 링이 기본 색으로 복원.
   - (f) 트롬본 detach(텔레포트로 anchor 떠남): 링 7개 모두 사라짐.

## Deliverables

- `Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` — `public bool IsGripHeld => m_IsGripHeld;` 한 줄 추가 (기존 `SlideIndex` getter 근처).
- `Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` — `tromboneAnchor` / `highlightIntensityMultiplier` 신규 SerializeField, `_spawnedRenderers` / `_baseColors` / `_lastIsAttached` / `_lastHighlightedIndex` 캐시, `LateUpdate` + `ApplyHighlight` + `RestoreHighlight` 메서드, `Rebuild()`/`ClearSpawned()`/`OnEnable`/`OnDisable` 보정.
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — `SlidePositionMarkers` MonoBehaviour 블록에 `tromboneAnchor` fileID wiring 추가. `highlightIntensityMultiplier`는 코드 기본값 자동 직렬화.

## Acceptance Criteria

- [ ] `[auto-hard]` `TromboneSlideController.cs`에 `public bool IsGripHeld => m_IsGripHeld;` (또는 동등 시그니처)가 존재한다.
  **검증:** `Grep -n "public bool IsGripHeld" Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs` 결과 1줄.
- [ ] `[auto-hard]` `SlidePositionMarkers.cs`에 `tromboneAnchor` SerializeField, `LateUpdate` 메서드, `ApplyHighlight`/`RestoreHighlight` 헬퍼가 모두 존재한다.
  **검증:** `Grep -n "SerializeField\] TromboneAnchor tromboneAnchor\|void LateUpdate\|ApplyHighlight\|RestoreHighlight" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 4건 이상.
- [ ] `[auto-hard]` `SlidePositionMarkers.cs`의 `LateUpdate`에 `Application.isPlaying` 가드와 `IsAttached` 폴링 + 자식 `SetActive` 토글 + `controller.IsGripHeld` 폴링이 모두 포함된다.
  **검증:** `Grep -n "Application\.isPlaying\|\.IsAttached\|SetActive\|\.IsGripHeld" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 4건 이상이고, 같은 파일에 `LateUpdate` 블록이 있음.
- [ ] `[auto-hard]` `Trombone.prefab`의 `SlidePositionMarkers` MonoBehaviour 블록에 `tromboneAnchor:` 라인이 추가되었으며 비-zero fileID를 가진다 (prefab wiring 박제).
  **검증:** `Grep -n -A 12 "m_EditorClassIdentifier: Instruments::Instruments.SlidePositionMarkers" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과 안에 `tromboneAnchor: {fileID: <NON_ZERO>}` 라인이 존재. (fileID 0이면 wiring 실패.)
- [ ] `[auto-hard]` `SlidePositionMarkers.cs`의 색 강조 식에 `* highlightIntensityMultiplier` (또는 동등 곱셈)이 적용되어 `Palette` 기본 색이 HDR 채널로 증폭된다.
  **검증:** `Grep -n "highlightIntensityMultiplier" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 2건 이상(`SerializeField` + `ApplyHighlight` 내부 사용).
- [ ] `[auto-hard]` EditMode 테스트 스위트가 컴파일 후 errors 0이고, 기존 `TromboneSlideController` / `SlidePositionMarkers` 관련 회귀(있다면)가 PASS다.
  **검증:** `unity-test-runner` sub-agent 호출 결과 EditMode pass + Unity MCP `read_console types=["error"]` 0건. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC를 `pass(skip)` 처리하고 Notes에 사유 기록.
- [ ] `[auto-soft]` Unity Editor 콘솔에 `SlidePositionMarkers` / `TromboneSlideController` 관련 NullReferenceException / MissingComponent 에러가 0건이다.
  **검증:** Unity MCP `read_console types=["error","warning"] filter_text="SlidePositionMarkers\|TromboneSlideController"` → 0건.
- [ ] `[manual-hard]` Editor Play Mode 시작 직후(트롬본 anchor 진입 전) `Trombone/Rig/SlidePositionMarkers` 자식 `SlideMarker_0~6` 7개가 모두 비활성(Hierarchy에서 회색 표시) 상태다.
  **검증:** Editor Play → Hierarchy 펼치기 → `Trombone/Rig/SlidePositionMarkers` 자식 7개의 active 토글이 모두 꺼진 상태(또는 Scene 뷰에서 링 안 보임)를 시각 확인. MCP `find_gameobjects search_term="SlideMarker_" include_inactive=true`로 결과 7건 + 각 `activeSelf=false` 확인 가능.
- [ ] `[manual-hard]` 트롬본 anchor 진입(attach) 직후 7개 색 링이 트롬본 슬라이드 축선을 따라 등장하며, 각 링 색은 `Palette[i]`(보라→파랑→하늘→초록→노랑→주황→빨강) 순서다.
  **검증:** Editor Play → 트롬본 anchor 텔레포트 → Scene/Game 뷰에서 7개 링이 가시화되고 색 순서가 사양과 일치하는지 확인.
- [ ] `[manual-hard]` 트롬본을 잡은 상태에서 슬라이드를 끌어당겨 위치가 변해도 7개 링의 world position이 변하지 않는다 (링은 SlidePositionMarkers의 자식이며 Slide 본체와 sibling 관계).
  **검증:** Editor Play → 트롬본 attach → 오른손 grip + 끌어당김으로 `Slide` 본체를 이동 → 링 7개 world 위치는 그대로(Scene Transform inspector 확인 또는 시각으로 링과 슬라이드가 분리되어 보임).
- [ ] `[manual-hard]` 오른손 Grip을 누르는 동안 현재 `SlideIndex` 위치의 링이 다른 링보다 시각적으로 밝게 보이고(URP Bloom 환경에서 발광 효과), 슬라이드 이동으로 인덱스가 바뀌면 강조가 새 링으로 즉시 이동하며 이전 링은 기본 색으로 돌아간다.
  **검증:** Editor Play → 트롬본 attach → 오른손 grip 누름 → 처음에 SlideMarker_0가 밝게 강조됨 시각 확인 → 오른손으로 slide 끌어당겨 인덱스 1~6로 이동 → 매번 새 인덱스 링이 강조되고 이전 인덱스 링은 기본 색으로 돌아가는지 시각 확인.
- [ ] `[manual-hard]` 오른손 Grip을 떼면 모든 링이 기본 색으로 돌아간다 (강조 링 0개).
  **검증:** Editor Play → 트롬본 attach + 오른손 grip 강조 상태 → 오른손 grip 떼기 → 모든 7개 링이 동일한 기본 밝기로 표시되는지 시각 확인.
- [ ] `[manual-hard]` 트롬본 detach(텔레포트로 anchor 떠남 → `TromboneAnchor.IsAttached == false`) 시 링 7개가 모두 사라진다.
  **검증:** Editor Play → 트롬본 attach 후 다른 anchor로 텔레포트해 detach → Scene/Game 뷰에서 링 7개가 보이지 않고 Hierarchy에서 자식 7개가 모두 비활성으로 돌아가는지 시각 확인.

## Out of Scope

- 자유 연주 중 판정 피드백 / 노트 접근 시 링 강조 (sub-spec 15 Out of Scope 답습).
- 링 크기/두께(`majorRadius`/`minorRadius`) 또는 위치(`lateralOffset`) 사용자 튜닝 — 현재 prefab 값 그대로 유지. 시각 회귀에서 부자연스러우면 별도 후속 plan.
- HDR 강조 강도(`highlightIntensityMultiplier`) 미세 튜닝 — 기본 4f로 제출, 시각 회귀에서 너무 약하거나 강하면 inspector에서 조정 후 prefab 저장. URP Bloom 강도 변경은 본 plan 범위 밖.
- 링 표시/숨김 토글 설정 UI (sub-spec 15 Out of Scope 답습).
- 이벤트 기반 `SlideIndexChanged` 구독 패턴(ARD 03 기각 옵션) — 폴링 채택 유지.
- `markerMaterialTemplate`을 별도 URP Lit/Emission 머티리얼로 교체 — fallback URP/Unlit `_BaseColor` HDR 증폭 방식 유지. 발광 효과가 명백히 부족하면 후속 plan에서 Emission 머티리얼 교체 후보.
- TromboneNoteDisplayAdapter / NoteDisplayPanel 변경 (Tech Spec Boundaries 답습).
- `Slide` 본체 / `Rig` / `SlidePositionMarkers` GameObject의 prefab 자체 추가·이동·이름 변경 (이미 prefab에 있음).

## Notes

- **시각 출처 단일화**: 본 plan은 `TromboneSlideColors.Palette`를 base color의 단일 진실원으로 유지. NoteDisplayPanel(TromboneNoteDisplayAdapter 라인 118)도 같은 Palette를 참조하므로 노트 색과 링 색이 동일 인덱스에서 정확히 매칭 — sub-spec 15 What "각 링의 색은 기존 노트 색 매핑(spec 13)과 동일하다"를 자동 만족.
- **HDR 채널 곱셈 결정 박제**: URP/Unlit shader fallback이라 emission 슬롯이 없다. `_BaseColor` 채널 자체에 >1 값을 넣어 HDR/Bloom으로 발광 표현. URP Bloom 미설정 환경에서는 단순히 색이 채도/명도 클램프 부근까지 밝아질 뿐 "발광" 효과가 약할 수 있음. 시각 회귀에서 부족하면 후속 plan에서 `markerMaterialTemplate`을 URP Lit + Emission 활성 머티리얼로 교체.
- **에디터 모드 가시성 박제**: `[ExecuteAlways]`는 그대로 유지하되 `LateUpdate`에서 `Application.isPlaying` 가드로 게이팅 비활성. 에디터에서 prefab 열어보면 링이 항상 보여 디자인 작업이 가능하다. Play 모드에서만 attach/grip 게이팅이 동작.
- **HDR 강조 동시 1개 invariant 보장**: `_lastHighlightedIndex`로 직전 강조 인덱스를 기억 → 변경 시 `RestoreHighlight(_last) → ApplyHighlight(_desired)` 2단계. Tech Spec Invariant "강조 링은 동시에 최대 1개" 자동 만족. `desiredIdx == -1`(grip 해제 또는 detach)이면 ApplyHighlight 스킵 + 직전 강조만 복원.
- **자식 SetActive 토글 디바운스**: `_lastIsAttached`로 변화 프레임에만 7회 SetActive 호출. 매 프레임 14건(7×set) 호출 회피.
- **재진입 시퀀스**: 트롬본 detach → 모든 자식 SetActive(false) + 강조 복원 → 재attach → 자식 SetActive(true), 강조는 grip 다음 누름 시 다시 적용 (자연스러움).
- **후속 plan 후보**: (a) `markerMaterialTemplate`을 URP Lit Emission 머티리얼로 교체하여 발광 효과 강화, (b) Bloom 강도 / `highlightIntensityMultiplier` 사용자 피드백 반영 미세 튜닝, (c) 노트 접근 시 링 강조 (slide guide 효과 — sub-spec 15 Out of Scope 였으나 후속에서 다룰 가치 있음), (d) 이벤트 기반 SlideIndexChanged 구독으로 폴링 비용 제거 (LateUpdate 빈도가 문제될 때만 — 현재는 무시 가능).

## Handoff

<완료 시 메인 세션이 갱신>

---

- 2026-05-27: 사용자 시각 회귀 피드백 — HDR 증폭이 헤드셋에서 가시성 부족. 후속 plan [`2026-05-27-claude-trombone-slide-rings-dim.md`](./2026-05-27-claude-trombone-slide-rings-dim.md)에서 `highlightIntensityMultiplier` 제거 + `dimFactor`(기본 0.3)로 비활성 링을 어둡게 처리하는 방향 반전 적용 예정.
