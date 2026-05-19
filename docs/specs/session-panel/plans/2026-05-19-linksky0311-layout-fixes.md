# Layout Fixes (sub-spec 07 단일 plan)

**Linked Spec:** [`07-layout-fixes.md`](../specs/07-layout-fixes.md)
**Status:** `Ready`

## Goal

(a) `InstrumentToggleButton.prefab`을 수직 2단 레이아웃으로 재구성해 상단에 악기명 라벨, 하단 중앙에 소형 토글 인디케이터(Background+Checkmark)가 배치되도록 한다. (b) `SessionPanel.prefab`의 `VolumePanel`에 `RectMask2D`를 부착하고 자식 Content 컨테이너의 `LayoutElement.flexibleHeight=0` + `preferredHeight` 상한을 박제해 슬라이더가 패널 시각 경계(400×300의 안쪽 400×260 영역) 안에 항상 표시되도록 한다.

## Context

sub-spec 06(스타일) + 후속 fix plan으로 5종 인터랙티브 컨트롤의 색상/스프라이트/펀치 애니메이션은 정합됐다. 그러나 sub-spec 07 What에서 박제된 두 시각 회귀가 잔존한다.

1. **InstrumentToggleButton 레이아웃**: 현재 100×100 루트 안에서 `Label`(자식 `Background`와 동일하게 full-stretch sizeDelta(0,0))과 `Background`(full-stretch sizeDelta(0,0))가 같은 영역을 겹쳐 차지한다. 라벨이 버튼 전체를 덮어 토글 상태(`Background` 색)가 한눈에 안 보이고, 글씨는 100×100 정사각형 한가운데 정렬돼 사용자가 "버튼처럼" 인지하지 못한다. 단순 컨테이너 RectTransform 2개를 상하 분할하면 spec What의 "악기명 위 / 토글 인디케이터 아래" 시각 모형을 충족하면서 sub-spec 06 fix에서 박제된 `backgroundImage` 직렬화/`UIScalePunch` 부착/`InstrumentToggleButtonUI.UpdateVisual(bool)` 시각 동기 로직을 모두 그대로 보존할 수 있다.

2. **VolumeSection 경계 초과**: SessionPanel 루트 RectTransform sizeDelta=(400,300)·localScale=(0.001,0.001,1)의 직접 자식으로 VolumePanel(fileID 5751264090231694390)이 anchor stretch + sizeDelta(0,-40)·anchoredPosition(0,-20)으로 400×260 영역을 점유한다. VolumePanel 자체에는 `VerticalLayoutGroup`만 부착돼 있고 `ContentSizeFitter`는 없으나, 자식 슬라이더가 동적 Instantiate되며 `VolumeSectionController.Awake`/`OnActiveInstrumentChanged`에서 `LayoutElement.preferredHeight=60f`를 명시적으로 부여하므로 RhythmGame Section과 충돌하거나 부모 컨테이너 vs Canvas Scaler가 어긋날 때 패널 시각 경계 밖으로 흘러보일 수 있다. 가장 단순·결정적 수정은 (i) `VolumePanel`에 `RectMask2D`를 부착해 시각적으로 영역 밖으로 흘러나가는 자식을 마스킹하고, (ii) `VerticalLayoutGroup` 자체 설정은 보존(슬라이더 polish 동작 유지)하되 `padding.bottom`을 8 → 12로 미세 조정해 마지막 슬라이더 baseline이 안쪽으로 들어오게 만드는 것. `ContentSizeFitter`는 현행 prefab에 없으므로 추가/제거 변경 없음 — sub-spec 07 What의 "단순한 수정"에 부합하고 슬라이더 인터랙션 코드(`VolumeSectionController.cs`)는 1줄도 건드리지 않는다.

본 plan은 한 세션 분량으로 이 2건만 처리한다. spec Out of Scope(색상/스프라이트 변경, sizeDelta 변경, 슬라이더 추가, 인터랙션 로직 변경)는 모두 회피한다.

## Verified Structural Assumptions

- `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 현재 GameObject 구조(2026-05-19, `Read Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab`):
  - 루트 `InstrumentToggleButton` (GameObject fileID 4462605354314844753, RectTransform fileID 3989874016961351244, sizeDelta(100,100), anchor (0.5,0.5)~(0.5,0.5)) — Toggle(`m_TargetGraphic: {fileID: 2672284072590456740}`, m_Colors 모두 중립값) + `InstrumentToggleButtonUI`(toggle/label/backgroundImage 모두 직렬화 연결) + `UIScalePunch`(peakScale=1.08, duration=0.12).
  - 자식 `Label` (GameObject fileID 5308917968404095884, RectTransform fileID 3280769272335923282) — 현재 anchor (0,0)~(1,1) sizeDelta(0,0) full-stretch. TMP_Text(font auto-sizing 1, min 8 max 36, alignment H=1 V=256(Middle)).
  - 자식 `Background` (GameObject fileID 1753484143399361388, RectTransform fileID 3807814552032250616) — 현재 anchor (0,0)~(1,1) sizeDelta(0,0) full-stretch. Image(sprite GUID `371cc7f19be24cc8944b77449df386f1` = PanelRoundedRect, Sliced(m_Type=1), color (1,1,1,1)).
  - 손자 `Checkmark` (Image fileID 6186591118504316445, RectTransform fileID 2095936687651647927, anchor (0.5,0.5)~(0.5,0.5) sizeDelta(20,20) center) — Toggle.graphic 참조.

- `Assets/SessionPanel/Prefabs/SessionPanel.prefab`의 SessionPanel 구조(2026-05-19, `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab` 오프셋 222~245 / 1650~1727):
  - 루트 RectTransform(fileID 4247339568228231875) sizeDelta=(400,300), localScale=(0.001,0.001,1). 자식 4개: fileID 2026847926404610461(패널 배경 Image, full-stretch) + 4251547738239070682(Tab area) + **5751264090231694390(VolumePanel)** + 7318693547341281361(StartMenu 섹션, anchor stretch sizeDelta(0,-40)).
  - VolumePanel(GameObject fileID 7180896559405363134, RectTransform fileID 5751264090231694390): anchor (0,0)~(1,1) sizeDelta(0,-40) anchoredPosition(0,-20) → 실제 영역 폭 400, 높이 260, 패널 상단 20px·하단 20px 마진. VerticalLayoutGroup(padding L/R/T/B=8/8/8/8, spacing=8, ChildControlW=1 ChildControlH=1, ChildForceExpandW=1 ChildForceExpandH=0). **`ContentSizeFitter` 없음.** `VolumeSectionController`(volumeSliderPrefab GUID `1492b1d8b5ac4f1d86cba2c033ee988b`, sliderParent self).
  - 본 plan은 VolumePanel에 `RectMask2D` 1개 신규 부착 + VerticalLayoutGroup `m_Padding.m_Bottom` 8 → 12 미세 조정만 수행. sizeDelta·anchor·VerticalLayoutGroup의 ChildControl 설정은 변경하지 않는다.

- `Assets/SessionPanel/Scripts/VolumeSectionController.cs`는 `Awake`/`OnActiveInstrumentChanged`에서 슬라이더 prefab을 Instantiate한 직후 `LayoutElement.preferredHeight=60f`를 부여한다. — `Read Assets/SessionPanel/Scripts/VolumeSectionController.cs (2026-05-19)`. 본 plan은 이 컨트롤러를 변경하지 않으며, RectMask2D 부착은 컨트롤러의 Slider 콜백/이벤트 동작에 영향이 없다(RectMask2D는 자식 Graphic의 사각 영역 클리핑만 수행).

- `UnityEngine.UI.RectMask2D` API side-effect 박제(Unity UGUI 공식 컴포넌트):
  - 자식 Graphic의 렌더 영역을 **RectMask2D를 가진 RectTransform의 사각 영역으로 클리핑**한다(Stencil Mask와 달리 사각 클리핑만, 추가 draw call 없음).
  - 자식의 `IClippable` 인터페이스를 통해 동작하므로 `UnityEngine.UI.Image`/`UnityEngine.UI.Slider`(자식 Image들)/`TMP_Text`(IClippable 구현) 모두 정상 클리핑된다.
  - Raycast에는 영향 없음(클리핑된 영역 밖에서도 raycastTarget=true면 클릭됨). 시각 회귀 수정에 한정해 사용한다.
  - 자식 RectTransform 자체의 sizeDelta/position에는 영향 없음(시각 마스킹만). — Unity UGUI 공개 사양 박제(2026-05-19).

- `UnityEngine.UI.Toggle.targetGraphic` / `graphic` / `m_Colors`는 본 plan에서 변경하지 않는다. 즉 sub-spec 06 fix plan에서 박제된 ColorBlock 중립값 + `InstrumentToggleButtonUI.UpdateVisual(bool)` 단일 색상 제어 책임은 그대로 유지된다. Label/Background RectTransform anchor 재배치는 Toggle/Graphic 동작에 영향이 없다(Toggle은 graphic alpha만 토글하지 RectTransform을 사용하지 않음). — `Read Library/PackageCache/com.unity.ugui@*/Runtime/UI/Core/Toggle.cs` 공개 사양 박제(2026-05-19).

- 본 plan은 신규 C# 파일을 추가하지 않으므로 `SessionPanel.Runtime.asmdef` 변경 불필요. 두 변경 모두 prefab YAML 패치 단일 종류. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-19)`.

- 본 plan 의도 신규 Label RectTransform: anchor (0, 0.5)~(1, 1), anchoredPosition (0, 0), sizeDelta (0, 0), pivot (0.5, 0.5) → 루트 100×100에서 위쪽 절반(폭 100, 높이 50, y center=25)을 점유. TMP_Text auto-sizing(min 8, max 36)은 새 영역 높이 50에서 안정 폰트 크기를 자동 결정.

- 본 plan 의도 신규 Background RectTransform: anchor (0.25, 0)~(0.75, 0.5), anchoredPosition (0, 0), sizeDelta (0, 0), pivot (0.5, 0.5) → 루트 100×100에서 아래쪽 절반(폭 50, 높이 50, 중앙정렬, y center=-25)을 점유. 9-slice `PanelRoundedRect`(spriteBorder 32, 128×128)는 본 50×50 영역에서 중앙폭이 50-32-32 = -14(<0)로 음수가 되어 압축 변형 가능성이 있다 — sub-spec 06 fix plan 회귀 사유와 동일.
  - **대응:** Background Image sprite를 fix plan에서 박제된 `ButtonRoundedRect.png`(GUID `30dbb1d077de4bb7b01b367491dbae50`, spriteBorder 14/14/14/14)로 교체. 50×50에서 중앙폭 50-14-14=22(>0)로 안전.
  - 본 sprite 교체는 sub-spec 07 Out of Scope("색상·스프라이트·애니메이션 변경 — sub-spec 06에서 완료")에 형식적으로 걸릴 수 있으나, **새 50×50 인디케이터 영역은 sub-spec 07이 새로 정의한 신규 시각 컨텍스트**이며 9-slice 압축 회귀가 plan 본 What(레이아웃 재구성)의 직접 부작용이므로 그 부작용을 함께 처리하는 것이 단일 세션 plan의 자연 경계다. (spec Out of Scope의 의도는 "색상 톤·애니메이션 톤 재디자인 금지"이지 회귀 방지 sprite 재참조 금지는 아님 — 본 plan은 fix plan에서 이미 박제된 sprite를 재사용할 뿐 신규 sprite 디자인을 추가하지 않는다.)

## Approach

1. **InstrumentToggleButton.prefab Label RectTransform 상단 절반으로 재배치**
   - RectTransform fileID 3280769272335923282의 `m_AnchorMin: {x: 0, y: 0.5}`, `m_AnchorMax: {x: 1, y: 1}`로 변경.
   - `m_AnchoredPosition: {x: 0, y: 0}`, `m_SizeDelta: {x: 0, y: 0}`, `m_Pivot: {x: 0.5, y: 0.5}` 유지.
   - TMP 컴포넌트(fileID 829056884769735698)는 변경 없음(auto-sizing min:8 max:36 그대로) — 새 영역 100×50에서 자동 조정.
   - 수정 수단: Unity MCP `manage_asset` 또는 `manage_gameobject` 우선. MCP 미가용 시 prefab 텍스트 패치(`.claude/skills/unity-asset-edit/SKILL.md` 절차).

2. **InstrumentToggleButton.prefab Background RectTransform 하단 중앙으로 재배치**
   - RectTransform fileID 3807814552032250616의 `m_AnchorMin: {x: 0.25, y: 0}`, `m_AnchorMax: {x: 0.75, y: 0.5}`로 변경.
   - `m_AnchoredPosition: {x: 0, y: 0}`, `m_SizeDelta: {x: 0, y: 0}`, `m_Pivot: {x: 0.5, y: 0.5}` 유지.

3. **InstrumentToggleButton.prefab Background Image sprite를 ButtonRoundedRect로 교체**
   - Image MonoBehaviour fileID 2672284072590456740의 `m_Sprite: {fileID: 21300000, guid: 371cc7f19be24cc8944b77449df386f1, type: 3}` → `{fileID: 21300000, guid: 30dbb1d077de4bb7b01b367491dbae50, type: 3}`로 교체.
   - `m_Type: 1`(Sliced) 유지. `m_FillCenter: 1` 유지. color/raycast/maskable 모두 유지.
   - Checkmark(fileID 6186591118504316445)는 sizeDelta(20,20) center 그대로 — 50×50 인디케이터 중앙에 그대로 표시되어 시각적으로 정합.

4. **SessionPanel.prefab VolumePanel에 RectMask2D 부착**
   - VolumePanel GameObject(fileID 7180896559405363134)의 `m_Component` 리스트에 신규 RectMask2D MonoBehaviour 1개 추가.
   - 신규 RectMask2D 직렬화 블록 표준값: `m_Enabled=1`, `m_Padding={x:0,y:0,z:0,w:0}`, `m_Softness={x:0,y:0}`.
   - 수정 수단: Unity MCP `manage_gameobject component=AddComponent type=RectMask2D` 우선. MCP 미가용 시 prefab YAML 텍스트 패치(`m_Component` 리스트에 component 항목 추가 + 새 MonoBehaviour block 작성).

5. **SessionPanel.prefab VolumePanel VerticalLayoutGroup padding bottom 미세 조정**
   - VerticalLayoutGroup MonoBehaviour fileID 8332093077203262167의 `m_Padding.m_Bottom: 8` → `12`로 변경.
   - 그 외 padding L/R/T, spacing, ChildControl*/ChildForceExpand* 모든 필드는 변경 없음.

6. **컴파일 대기 & 콘솔 검증** — [`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md)의 도메인 리로드 대기(`editor_state.isCompiling==false` 폴링) + `read_console types=["error","warning"]` 0건 절차. C# 변경이 없으므로 컴파일 자체는 No-op이지만 prefab 직렬화 변경 후 Unity Editor가 prefab을 재로드해 직렬화 누락 경고가 0건임을 확인한다.

7. **screenshot 시각 검증** — TestSceneSanyo 또는 SampleScene에 SessionPanel을 띄우고, (a) InstrumentToggleButton에서 라벨(악기명)이 상단·인디케이터(50×50 둥근 사각형)가 하단 중앙에 표시되는지, (b) `isOn=true` 인디케이터가 액센트색 / `isOn=false` 인디케이터가 반투명 흰색으로 표시되는지, (c) VolumePanel 슬라이더들이 SessionPanel 시각 경계(외곽 둥근 모서리 안쪽) 안에 표시되며 RectMask2D로 경계 밖에 어떤 자식도 흘러나가지 않는지를 사용자 시각 검증으로 확인.

## Deliverables

- `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` — Label RectTransform 상단 절반 / Background RectTransform 하단 중앙 50% 너비 / Background Image sprite를 `ButtonRoundedRect`(GUID `30dbb1d077de4bb7b01b367491dbae50`)로 교체(수정).
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` — VolumePanel(fileID 7180896559405363134)에 `RectMask2D` 컴포넌트 1개 추가 + VerticalLayoutGroup(fileID 8332093077203262167) padding.bottom 8→12(수정).

## Acceptance Criteria

- [ ] `[auto-hard]` `InstrumentToggleButton.prefab`의 Label RectTransform(fileID 3280769272335923282) anchor가 `m_AnchorMin: {x: 0, y: 0.5}` / `m_AnchorMax: {x: 1, y: 1}` 으로 박제되고, 기존 full-stretch anchor `m_AnchorMin: {x: 0, y: 0}` 시그니처가 같은 RectTransform 블록에 등장하지 않는다.
  **검증:** `Grep -n "m_AnchorMin: \{x: 0, y: 0\.5\}" Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 1건 이상 + 해당 RectTransform 블록 안에서 `m_AnchorMax: \{x: 1, y: 1\}` 동반 + 같은 RectTransform fileID 3280769272335923282 블록 안에 `m_AnchorMin: \{x: 0, y: 0\}` 부재.
- [ ] `[auto-hard]` `InstrumentToggleButton.prefab`의 Background RectTransform(fileID 3807814552032250616) anchor가 `m_AnchorMin: {x: 0.25, y: 0}` / `m_AnchorMax: {x: 0.75, y: 0.5}` 으로 박제된다.
  **검증:** `Grep -n "m_AnchorMin: \{x: 0\.25, y: 0\}" Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 1건 이상 + `Grep -n "m_AnchorMax: \{x: 0\.75, y: 0\.5\}" Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 1건 이상.
- [ ] `[auto-hard]` `InstrumentToggleButton.prefab`의 Background Image(fileID 2672284072590456740)가 `ButtonRoundedRect.png` GUID(`30dbb1d077de4bb7b01b367491dbae50`)를 참조하고, 기존 `PanelRoundedRect` GUID(`371cc7f19be24cc8944b77449df386f1`)는 같은 prefab의 Background Image 블록에서 더 이상 등장하지 않는다.
  **검증:** `Grep -n "guid: 30dbb1d077de4bb7b01b367491dbae50" Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 1건 이상 매치 + `Grep -n "guid: 371cc7f19be24cc8944b77449df386f1" Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 0건 매치(이 prefab은 Background 외에 PanelRoundedRect 사용 사이트가 없다).
- [ ] `[auto-hard]` `SessionPanel.prefab` VolumePanel GameObject(fileID 7180896559405363134)의 `m_Component` 리스트에 RectMask2D MonoBehaviour 항목이 추가되어 있고, 새 MonoBehaviour 직렬화 블록의 `m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.RectMask2D`가 존재한다.
  **검증:** `Grep -n "UnityEngine.UI::UnityEngine.UI.RectMask2D" Assets/SessionPanel/Prefabs/SessionPanel.prefab` 1건 이상 매치 + MCP `find_gameobjects search_method=by_component search_term=RectMask2D` 결과에 SessionPanel.prefab의 VolumePanel 인스턴스 instanceId 등장.
- [ ] `[auto-hard]` `SessionPanel.prefab` VolumePanel VerticalLayoutGroup(fileID 8332093077203262167)의 `m_Padding.m_Bottom`이 `12`로 박제된다.
  **검증:** VerticalLayoutGroup fileID 8332093077203262167 블록을 Read해 `m_Padding: \n    m_Left: 8\n    m_Right: 8\n    m_Top: 8\n    m_Bottom: 12` 패턴 매치(혹은 `Grep` 멀티라인 모드로 동등 검증). 같은 블록의 다른 padding/spacing/ChildControl 값은 기존값 그대로(`m_Spacing: 8`, `m_ChildControlWidth: 1`).
- [ ] `[auto-hard]` Unity 도메인 리로드(prefab 재로드) 완료 후 `read_console types=["error","warning"]` 결과 0건. 특히 RectMask2D 신규 부착 후 직렬화 누락 경고나 missing reference 경고가 발생하지 않는다.
  **검증:** MCP `read_console action=get types=["error","warning"]` 호출 → count==0. `editor_state.isCompiling`이 false로 떨어진 직후 측정. 또한 SessionPanel을 띄우는 씬을 1회 로드(`manage_scene action=open path=Assets/Scenes/SampleScene.unity` 또는 TestSceneSanyo)해 prefab override 경고/직렬화 누락 0건 확인.
- [ ] `[auto-soft]` `SessionPanel.prefab` VolumePanel의 `VerticalLayoutGroup` 다른 필드(spacing=8, ChildControlWidth=1, ChildControlHeight=1, ChildForceExpandWidth=1, ChildForceExpandHeight=0)와 `RectTransform` 필드(sizeDelta=(0,-40), anchoredPosition=(0,-20), anchor (0,0)~(1,1))가 본 plan 범위 외이므로 변경 없음.
  **검증:** VolumePanel RectTransform fileID 5751264090231694390 / VerticalLayoutGroup fileID 8332093077203262167 블록을 Read해 박제값과 일치. 차이 발견 시 노트에 기록 후 후속 plan 분기.
- [ ] `[manual-hard]` Unity Editor에서 SessionPanel을 띄우고 InstrumentToggleButton(악기 잡힌 상태에서 노출) 시 (a) 악기명 텍스트가 버튼 상단 절반에 표시되고, (b) 둥근 사각형 인디케이터가 하단 중앙에 가로 50% 너비로 표시되며, (c) `isOn=true`일 때 액센트색(#4A9EFF) / `isOn=false`일 때 반투명 흰색으로 점등되고, (d) 인디케이터 내부에 Checkmark(흰색)가 isOn=true 시 표시된다.
  **검증:** TestSceneSanyo 또는 SampleScene 로드 → 악기 잡힘 시뮬레이션(또는 Editor에서 InstrumentToggleButton 인스턴스 직접 활성) → isOn=true/false 토글 1회씩 → 스크린샷 2장 캡처하여 위 4점 시각 검토.
- [ ] `[manual-hard]` Unity Editor에서 SessionPanel을 띄우고 Volume Section을 활성화했을 때, Master 슬라이더 + 악기 슬라이더(악기 잡힘 시) 모두가 SessionPanel의 외곽 둥근 모서리 안쪽에 표시되고, RectMask2D에 의해 패널 시각 경계 밖으로 흘러나가는 자식 Graphic이 0건이다(슬라이더 hover/value 변경 중에도 잔류 시각 회귀 없음).
  **검증:** SampleScene 또는 TestSceneSanyo 로드 → 악기 잡힘 시뮬레이션으로 SessionPanel + Volume Section 활성 → 슬라이더 값 0%/50%/100% 3개 상태 screenshot 캡처 + 패널 외곽선 기준 자식이 밖으로 나가는지 시각 검토.

## Out of Scope

- VolumeSlider/InstrumentToggleButton의 색상·sprite 디자인 재정의 — sub-spec 06 및 fix plan에서 완료. 본 plan은 Background sprite를 fix plan 박제 sprite로 재참조할 뿐 신규 디자인 없음.
- `VolumeSectionController.cs` 변경(슬라이더 인스턴스화 정책, LayoutElement.preferredHeight 정책 등) — spec Out of Scope("상호작용 로직 변경").
- InstrumentToggleButton 100×100 크기 변경 — spec Out of Scope("크기 변경"); 레이아웃 내부 재배치만 다룬다.
- VolumePanel의 `RectTransform.sizeDelta`/`anchoredPosition` 변경 — RectMask2D + padding 미세 조정으로 충분, 부모-자식 anchor 재조정은 회귀 위험 높아 회피.
- `ContentSizeFitter` 추가/제거 — 현행 VolumePanel에 없음. 자식 슬라이더 합산 높이는 LayoutElement.preferredHeight=60×2 + spacing 8 + padding(8+12)=148로 VolumePanel 영역 260 안에 안정적으로 fit.
- StartMenu 섹션(fileID 7318693547341281361)의 ContentSizeFitter(line 624 부근, 자식 Content) — 본 plan 범위 밖. Volume Section과 동일 패턴 시각 회귀가 있을 경우 후속 plan 후보.
- `ButtonRoundedRect.png` 자체의 spriteBorder/크기 재정의 — fix plan에서 박제된 64×64/border 14 그대로 재사용.

## Notes

- Background RectTransform anchor (0.25,0)~(0.75,0.5)는 인디케이터를 "가로 중앙 50% 너비 / 하단 절반 높이"로 만드는 단일 변환이다. anchor만으로 처리하므로 sizeDelta(0,0) 유지가 그대로 적용된다(부모 100×100 기준 Background 영역 = (25,0)~(75,50) → 폭 50, 높이 50).
- Checkmark(20×20 center)는 새 50×50 인디케이터 영역 안 중앙에 그대로 위치한다 — 별도 anchor 조정 불필요. 이는 Toggle.graphic가 위치가 아니라 alpha만 토글하기 때문에 안전.
- RectMask2D 부착 시 자식 GraphicRaycaster/Button 상호작용은 영향 없다. 슬라이더 드래그 영역이 마스크 안에 있으면 정상 hit.
- VerticalLayoutGroup padding.bottom 12는 시각 안정성 + 마지막 슬라이더와 패널 하단 경계 사이 여백 4px 추가 효과. fix plan에서 박제된 8 → 12로 4px만 미세 조정.
- 후속 plan 후보: (a) StartMenu(RhythmSection)의 ContentSizeFitter+VerticalLayoutGroup 정합 점검 — 동일 패턴 회귀 가능성. (b) `DifficultyButton`/`SongRow` 등 다른 컨트롤 폰트 메트릭 정합. (c) 슬라이더 핸들 드래그 시 RectMask2D 클리핑 시각 끊김 발생 시 마스크 padding 확장.

## Handoff

<!-- /spec-build 가 plan 완료 시 doc-updater Task로 자동 갱신한다. -->
