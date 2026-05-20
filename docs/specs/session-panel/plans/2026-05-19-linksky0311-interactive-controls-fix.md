# Interactive Controls Fix (sub-spec 06 회귀 수정)

**Linked Spec:** [`06-interactive-controls-style.md`](../specs/06-interactive-controls-style.md)
**Caused By:** [`2026-05-19-linksky0311-interactive-controls-style.md`](./2026-05-19-linksky0311-interactive-controls-style.md)
**Status:** `Done`

## Goal

선행 plan(`2026-05-19-linksky0311-interactive-controls-style.md`)의 manual-hard 검증에서 발견된 3건의 시각 회귀를 수정한다 — (1) 9-slice 압축 변형(border 32 > 버튼 높이 40), (2) Toggle `isOn=true` 상태가 ColorTint Selected에 자동 매핑되지 않는 문제, (3) `DifficultyButtonUI.cs`의 구 팔레트 하드코딩이 ColorBlock과 충돌. 신규 14px-border sprite 1장 + 3개 스크립트 수정 + 5개 프리팹 재셋업으로 처리한다.

## Context

선행 plan이 5종 프리팹에 `Assets/SessionPanel/Sprites/PanelRoundedRect.png` (128×128, spriteBorder 32/32/32/32)을 Sliced로 적용했으나, 실 사용자 시각 검증에서 다음이 보고됐다.

> **선행 plan(`2026-05-19-linksky0311-interactive-controls-style.md`)의 실패 manual-hard AC (3건):**
> 1. `DifficultyButton`/`AccompanimentToggle` 글씨 위치·크기가 비정상으로 변형됨. 원인: 9-slice border 32px가 버튼 높이 40px를 초과해 Sliced 중앙 영역이 음수 사이즈로 압축돼 sprite가 비정상 렌더된다.
> 2. `InstrumentToggleButton`에서 `isOn=true` 상태가 회색으로 보임. 원인: Unity UI `Toggle`은 ColorTint의 `m_SelectedColor`를 `isOn==true`에 자동 매핑하지 않는다 — Selected는 키보드/패드 navigation focus 상태이지 toggle on/off 상태가 아니다. 시각 상태 매핑은 코드에서 직접 backgroundImage.color를 갱신해야 한다.
> 3. `DifficultyButton` 선택 색이 액센트로 적용되지 않음. 원인: `DifficultyButtonUI.cs`에 `NormalColor=(0.18,0.22,0.4,0.9)` / `SelectedColor=(0.25,0.5,0.85,1.0)` 구 팔레트가 하드코딩돼 `Setup(... Image bg)`로 주입된 루트 Image의 color를 직접 덮어쓰며, 동시에 Button.ColorBlock의 `m_NormalColor=(1,1,1,0.15)`이 같은 Image의 m_Color를 갱신하므로 두 색상 시스템이 충돌한다.

본 plan은 sub-spec 06 What의 "둥근 모서리 + 액센트 선택색" 의도를 그대로 유지하되, 위 3건의 원인을 다음과 같이 끊는다.

- **(1) 해결**: 신규 64×64 sprite `ButtonRoundedRect.png`(corner radius 14px, spriteBorder 14/14/14/14)를 추가해 높이 40~50px 컨트롤에 사용. `InstrumentToggleButton`(100×100)은 기존 `PanelRoundedRect`(border 32)를 유지한다.
- **(2) 해결**: `InstrumentToggleButtonUI.cs`/`AccompanimentToggleUI.cs`에 `backgroundImage` 직렬화 필드와 `UpdateVisual(bool isOn)` 메서드를 추가해 Toggle.onValueChanged + Setup 시점에 backgroundImage.color를 OnColor/OffColor로 직접 셋팅. Toggle.colors는 중립값으로 변경해 ColorTint 간섭 차단.
- **(3) 해결**: `DifficultyButtonUI.cs`의 `NormalColor`/`SelectedColor`를 새 팔레트로 교체(`(1,1,1,0.15)` / `(0.290,0.620,1.000,0.85)`). Button.colors를 중립값으로 변경해 ColorTint와 충돌 차단(스크립트가 단일 색상 제어 책임을 가짐).

선행 plan에서 박제된 팔레트·sprite는 그대로 단일 진실원으로 재사용한다.

## Verified Structural Assumptions

- 신규 sprite 대상 경로: `Assets/SessionPanel/Sprites/ButtonRoundedRect.png` — 동일 폴더에 기존 `PanelRoundedRect.png` 존재 확인. `Glob Assets/SessionPanel/Sprites/*.png (2026-05-19)`.
- 기존 `PanelRoundedRect.png` 사양: 128×128, spriteBorder 32/32/32/32, alphaIsTransparency=1, spriteMode=1(Single), `spritePixelsToUnits=100`. — `Read Assets/SessionPanel/Sprites/PanelRoundedRect.png.meta (2026-05-19)` (handoff 입력 3과 일치).
- 본 plan 의도 신규 sprite 사양: 64×64, 모서리 반지름 14px(반투명 둥근 사각형 알파), spriteBorder x=14 y=14 z=14 w=14, alphaIsTransparency=1, spriteMode=1. 9-slice 중앙폭 = 64-14-14 = 36px(>0), 적용 대상 높이 40px일 때 Sliced 중앙 세로 높이 = 40-14-14 = 12px(>0)로 압축 없음.
- 적용 대상 프리팹 sizeDelta(선행 plan handoff에서 박제, 변경 없음):
  - `Assets/SessionPanel/UI/DifficultyButton.prefab` sizeDelta(0,40) → 신규 sprite 사용.
  - `Assets/SessionPanel/UI/AccompanimentToggle.prefab` sizeDelta(0,40) → 신규 sprite 사용.
  - `Assets/SessionPanel/UI/SongRow.prefab` sizeDelta(0,50) → 신규 sprite 사용 (40~50 영역 모두 안전).
  - `Assets/SessionPanel/UI/VolumeSlider.prefab` sizeDelta(0,60) → sprite 변경 없음(트랙 sprite는 sub-spec 06 What 외).
  - `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` sizeDelta(100,100) → 기존 `PanelRoundedRect.png` 유지.
- `DifficultyButton.prefab`의 현재 Button.colors는 이미 선행 plan에서 박제된 ColorBlock(Normal=(1,1,1,0.15), Highlighted/Selected=(0.290,0.620,1.000,0.85), Pressed=(0.290,0.620,1.000,1.0), Disabled=(0.5,0.5,0.5,0.3), FadeDuration=0.1)으로 적용돼 있음. — `Read Assets/SessionPanel/UI/DifficultyButton.prefab (2026-05-19)`. 본 plan은 이를 **중립값**(Normal/Highlighted/Pressed/Selected=(1,1,1,1), Disabled=(0.5,0.5,0.5,0.3))으로 재설정해 스크립트 단일 제어로 전환.
- `DifficultyButtonUI.cs` 현재 본문: 클래스 `SessionPanel.DifficultyButtonUI`, `static readonly Color NormalColor = new Color(0.18f, 0.22f, 0.4f, 0.9f); static readonly Color SelectedColor = new Color(0.25f, 0.5f, 0.85f, 1.0f);`, `Setup(string difficulty, Action<string, DifficultyButtonUI> callback, Image bg = null)`, `SetSelected(bool selected)`. — `Read Assets/SessionPanel/Scripts/DifficultyButtonUI.cs (2026-05-19)`.
- `InstrumentToggleButtonUI.cs` 현재 본문: 클래스 `SessionPanel.InstrumentToggleButtonUI`, 필드 `Toggle toggle`/`TextMeshProUGUI label`만 직렬화. `OnToggleChanged(bool isOn)`은 `_callback?.Invoke(...)`만 수행, 시각 갱신 없음. — `Read Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs (2026-05-19)`.
- `AccompanimentToggleUI.cs` 현재 본문: 클래스 `SessionPanel.AccompanimentToggleUI`, 필드 `Toggle toggle`/`TextMeshProUGUI label`. `OnToggleChanged(bool isOn)` 시각 갱신 없음. — `Read Assets/SessionPanel/Scripts/AccompanimentToggleUI.cs (2026-05-19)`.
- 외부 호출 API side-effect: `UnityEngine.UI.Toggle.onValueChanged` (UnityEvent<bool>) — `isOn` 토글 시 1회 발화, `Setup` 직후 첫 `isOn = defaultOn` 대입 시에도 발화(현 상태에서 이미 다른 값일 때). 본 plan은 `Setup` 마지막에서 `UpdateVisual(defaultOn)`을 명시 호출해 초기 시각 동기를 보장한다(Toggle이 같은 값 대입 시 onValueChanged를 발화하지 않는 케이스 방지). `Toggle.targetGraphic`은 graphic의 색상을 ColorTint state로 매번 덮어쓰므로 본 plan은 Toggle.colors를 모두 (1,1,1,1) 중립으로 설정 + Disabled만 (0.5,0.5,0.5,0.3)로 두어 정상 상태에서 backgroundImage.color를 스크립트가 단일 제어. — `Read Library/PackageCache/com.unity.ugui@*/Runtime/UI/Core/Toggle.cs` 동작 박제(공개 UGUI 사양).
- `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` references = `Instruments`, `RhythmGame.Data`, `RhythmGame.Runtime`, `Unity.InputSystem`, `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`, `autoReferenced=true`. 본 plan의 스크립트 수정은 신규 import 없음(`UnityEngine.UI.Image`는 이미 `using UnityEngine.UI;` 적재) — asmdef reference 추가 불필요. — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-19)`.
- `UIScalePunch` 컴포넌트(선행 plan 산출물)는 4종 Selectable 루트에 부착돼 있고 본 plan은 변경하지 않는다. — `Read Assets/SessionPanel/UI/DifficultyButton.prefab (2026-05-19)` line 280~292 (UIScalePunch instance peakScale=1.08, duration=0.12).

## Approach

1. **신규 sprite 생성: `Assets/SessionPanel/Sprites/ButtonRoundedRect.png`**
   1. 64×64 픽셀, 알파에 모서리 반지름 14px의 둥근 사각형 마스크(중앙은 불투명 흰색, 4모서리만 알파 그라데이션). 사용자가 외부 도구(Photoshop/Affinity 등)로 PNG 생성하거나, Unity MCP `manage_asset`로 byte payload write.
   2. `.meta` import setting: `textureType=8`(Sprite), `spriteMode=1`(Single), `spriteBorder: {x: 14, y: 14, z: 14, w: 14}`, `alphaIsTransparency=1`, `spritePixelsToUnits=100`. (기존 `PanelRoundedRect.png.meta` 베이스 + spriteBorder만 14로 교체)
   3. GUID는 신규 import 시 자동 발급. plan 적용 후 발급된 GUID를 본 plan `Handoff`에 박제 권장.

2. **4종 프리팹의 루트 Image sprite 교체 (Unity MCP `manage_asset` text patch 또는 prefab 직접 Edit)**
   1. `DifficultyButton.prefab` 루트 Image(fileID 9000000000000002)의 `m_Sprite: {fileID: 21300000, guid: 371cc7f19be24cc8944b77449df386f1, type: 3}` → 신규 `ButtonRoundedRect.png` GUID로 교체.
   2. `SongRow.prefab` 루트 Image의 sprite 동일 교체.
   3. `AccompanimentToggle.prefab` 루트 Image의 sprite 동일 교체.
   4. `InstrumentToggleButton.prefab`은 sprite 교체 없음(100×100 패널은 32-border가 정합 — 중앙폭 100-32-32=36 > 0).

3. **Selectable ColorBlock 중립화 (4종 프리팹)**
   - `DifficultyButton.prefab` / `SongRow.prefab` Button.m_Colors:
     - `m_NormalColor: {r: 1, g: 1, b: 1, a: 1}`
     - `m_HighlightedColor: {r: 1, g: 1, b: 1, a: 1}`
     - `m_PressedColor: {r: 1, g: 1, b: 1, a: 1}`
     - `m_SelectedColor: {r: 1, g: 1, b: 1, a: 1}`
     - `m_DisabledColor: {r: 0.5, g: 0.5, b: 0.5, a: 0.3}`
     - `m_ColorMultiplier: 1`, `m_FadeDuration: 0.1`
   - `AccompanimentToggle.prefab` / `InstrumentToggleButton.prefab` Toggle.m_Colors도 동일 중립값.
   - 의도: ColorTint state가 graphic을 매 프레임 (1,1,1,1)로 곱해 덮어써도 스크립트가 직접 갱신하는 backgroundImage.color에 영향 없음(곱 항등). Disabled만 graphic 곱(0.5,0.5,0.5,0.3) 시 시각적으로 흐려지는 효과 유지.

4. **`Assets/SessionPanel/Scripts/DifficultyButtonUI.cs` 수정**
   - `NormalColor` 상수값을 `new Color(1f, 1f, 1f, 0.15f)`로 교체.
   - `SelectedColor` 상수값을 `new Color(0.290f, 0.620f, 1.000f, 0.85f)`로 교체.
   - 그 외 메서드 시그니처·이름 변경 없음(다른 호출자 `RhythmGameSectionController` 등의 결합 보존).

5. **`Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs` 수정**
   - 직렬화 필드 추가: `[SerializeField] Image backgroundImage;` (top of class).
   - 상수 추가: `static readonly Color OnColor = new Color(0.290f, 0.620f, 1.000f, 0.85f);`, `static readonly Color OffColor = new Color(1f, 1f, 1f, 0.15f);`.
   - `Setup(...)` 끝에서 `UpdateVisual(defaultOn);` 호출.
   - `OnToggleChanged(bool isOn)`에서 콜백 invoke 전 `UpdateVisual(isOn);` 호출.
   - 메서드 추가:
     ```csharp
     void UpdateVisual(bool isOn)
     {
         if (backgroundImage != null)
             backgroundImage.color = isOn ? OnColor : OffColor;
     }
     ```
   - `using UnityEngine.UI;`는 이미 적재(`Image`/`Toggle` 모두 동일 네임스페이스) — 신규 import 불필요.

6. **`Assets/SessionPanel/Scripts/AccompanimentToggleUI.cs` 수정**
   - InstrumentToggleButtonUI와 동일 패턴 적용: `backgroundImage` 필드, `OnColor`/`OffColor` 상수, `Setup` 끝 `UpdateVisual(defaultOn)`, `OnToggleChanged`에서 `UpdateVisual(isOn)`, `UpdateVisual(bool)` 메서드 추가.

7. **2종 Toggle 프리팹의 `backgroundImage` 직렬화 필드 연결**
   - `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab`: 자식 `Background`의 Image fileID를 InstrumentToggleButtonUI MonoBehaviour 직렬화 블록 끝에 `backgroundImage: {fileID: <Background Image fileID>}` 1줄 추가. (기존 `toggle`/`label` 필드 아래.)
   - `Assets/SessionPanel/UI/AccompanimentToggle.prefab`: 루트 Image(선행 plan에서 신규 추가)의 Image fileID를 AccompanimentToggleUI MonoBehaviour 직렬화 블록 끝에 `backgroundImage: {fileID: <root Image fileID>}` 1줄 추가.

8. **컴파일 대기 & 콘솔 검증** — [`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../../.claude/skills/unity-mcp-workflow/SKILL.md)의 스크립트 컴파일 대기(`editor_state.isCompiling==false` 폴링) + `read_console` types=error/warning 0건 절차.

9. **screenshot 시각 검증** — `TestSceneSanyo` 또는 `SampleScene` 로드 후 SessionPanel 인스턴스에서 (a) DifficultyButton 라벨 미왜곡, (b) AccompanimentToggle 라벨 미왜곡, (c) InstrumentToggleButton `isOn=true` 시 액센트, `isOn=false` 시 반투명 흰 톤, (d) DifficultyButton 선택 시 액센트 색 4종 케이스를 캡처해 시각 검토 (사용자 직접).

## Deliverables

- `Assets/SessionPanel/Sprites/ButtonRoundedRect.png` (+ `.meta`) — 신규 64×64 둥근사각형 sprite, spriteBorder 14/14/14/14.
- `Assets/SessionPanel/UI/DifficultyButton.prefab` — 루트 Image sprite를 신규 `ButtonRoundedRect`로 교체 + Button.colors 중립화(수정).
- `Assets/SessionPanel/UI/SongRow.prefab` — 루트 Image sprite 교체 + Button.colors 중립화(수정).
- `Assets/SessionPanel/UI/AccompanimentToggle.prefab` — 루트 Image sprite 교체 + Toggle.colors 중립화 + AccompanimentToggleUI.backgroundImage 직렬화 연결(수정).
- `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` — Toggle.colors 중립화 + InstrumentToggleButtonUI.backgroundImage 직렬화 연결(수정, sprite 유지).
- `Assets/SessionPanel/Scripts/DifficultyButtonUI.cs` — NormalColor/SelectedColor 신팔레트 교체(수정).
- `Assets/SessionPanel/Scripts/InstrumentToggleButtonUI.cs` — backgroundImage 직렬화 필드 + UpdateVisual 시각 동기 추가(수정).
- `Assets/SessionPanel/Scripts/AccompanimentToggleUI.cs` — backgroundImage 직렬화 필드 + UpdateVisual 시각 동기 추가(수정).

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/SessionPanel/Sprites/ButtonRoundedRect.png` 파일과 `.meta`가 존재하며, `.meta`의 `spriteBorder`가 `{x: 14, y: 14, z: 14, w: 14}` 와 `spriteMode: 1`, `textureType: 8` (Sprite)을 만족한다.
  **검증:** `Glob Assets/SessionPanel/Sprites/ButtonRoundedRect.png` 1건 매치 + `Grep "spriteBorder: \{x: 14, y: 14, z: 14, w: 14\}"` 매치 + `Grep "textureType: 8"` 매치 (대상: `Assets/SessionPanel/Sprites/ButtonRoundedRect.png.meta`).
- [ ] `[auto-hard]` Unity 도메인 리로드 완료 후 `read_console` types=error/warning 결과 0건(3개 스크립트 수정 + 신규 sprite import + 5개 프리팹 수정 반영 직후).
  **검증:** MCP `read_console action=get types=["error","warning"]` 호출 → count==0. `editor_state.isCompiling`이 false로 떨어진 직후에 측정. 또한 SessionPanel을 띄우는 씬을 1회 로드해 직렬화 누락 경고 0건 확인.
- [ ] `[auto-hard]` `DifficultyButtonUI.cs`의 `NormalColor`가 RGBA(1,1,1,0.15), `SelectedColor`가 RGBA(0.290,0.620,1.000,0.85)로 교체됐고, 구 팔레트(0.18,0.22,0.4) / (0.25,0.5,0.85) 리터럴이 남아 있지 않다.
  **검증:** `Grep "new Color\(1f, 1f, 1f, 0\.15f\)" Assets/SessionPanel/Scripts/DifficultyButtonUI.cs` 매치 + `Grep "new Color\(0\.290f, 0\.620f, 1\.000f, 0\.85f\)" Assets/SessionPanel/Scripts/DifficultyButtonUI.cs` 매치 + `Grep "0\.18f, 0\.22f, 0\.4f" Assets/SessionPanel/Scripts/DifficultyButtonUI.cs` 0건.
- [ ] `[auto-hard]` `InstrumentToggleButtonUI.cs`와 `AccompanimentToggleUI.cs` 모두에 `[SerializeField] Image backgroundImage;` 필드와 `void UpdateVisual(bool` 시그니처가 존재하고, `OnToggleChanged` 본문에서 `UpdateVisual(` 호출이 1회 이상 등장한다.
  **검증:** 두 파일에 대해 `Grep "\[SerializeField\] Image backgroundImage"` 각 1건 + `Grep "void UpdateVisual\(bool"` 각 1건 + `Grep "UpdateVisual\("` 본 메서드 정의 외 2건 이상(Setup, OnToggleChanged) 매치.
- [ ] `[auto-hard]` 4종 Selectable 프리팹(`DifficultyButton.prefab` / `SongRow.prefab` / `AccompanimentToggle.prefab` / `InstrumentToggleButton.prefab`)의 `m_Colors` 블록이 중립값(`m_NormalColor: {r: 1, g: 1, b: 1, a: 1}`)을 만족한다.
  **검증:** 각 prefab YAML에 대해 `Grep "m_NormalColor: \{r: 1, g: 1, b: 1, a: 1\}"` 4건 모두 매치.
- [ ] `[auto-hard]` `DifficultyButton.prefab` / `SongRow.prefab` / `AccompanimentToggle.prefab` 3종의 루트 Image `m_Sprite`가 신규 `ButtonRoundedRect.png`의 GUID(plan 적용 시 발급, Handoff에 박제)를 참조하고, 기존 `PanelRoundedRect` GUID(`371cc7f19be24cc8944b77449df386f1`)는 본 3종에서 더 이상 등장하지 않는다. `InstrumentToggleButton.prefab`은 기존 GUID 유지.
  **검증:** 3종 prefab YAML에서 `Grep "371cc7f19be24cc8944b77449df386f1"` 0건 매치 + 신규 sprite GUID `Grep` 1건 이상 매치. `InstrumentToggleButton.prefab`에서는 기존 GUID `Grep` 1건 이상 매치.
- [ ] `[auto-soft]` 2종 Toggle 프리팹(`AccompanimentToggle.prefab` / `InstrumentToggleButton.prefab`)의 ToggleUI MonoBehaviour 직렬화 블록에 `backgroundImage: {fileID:` 1줄이 존재한다(직렬화 정합 — 미연결 시 런타임 NullRef 없이 silent skip이지만 시각 회귀 재발).
  **검증:** 각 prefab YAML에 대해 `Grep "backgroundImage: \{fileID:"` 1건 이상 매치. 실패 시 노트에 기록 후 후속 plan 분기.
- [ ] `[manual-hard]` Unity Editor에서 SessionPanel을 띄우고, (a) DifficultyButton·AccompanimentToggle의 라벨 글씨가 비정상 변형 없이 정상 위치·크기로 보이고, (b) DifficultyButton 선택 시 액센트 #4A9EFF 톤으로 전환되고, (c) InstrumentToggleButton의 `isOn=true` 상태가 액센트로, `isOn=false` 상태가 반투명 흰 톤으로 표시된다.
  **검증:** TestSceneSanyo 또는 SampleScene 로드 → SessionPanel 활성 → DifficultyButton 라벨/AccompanimentToggle 라벨 스크린샷 1장 + DifficultyButton 선택 전/후 1장 + InstrumentToggleButton isOn=true/false 1장 캡처 후 시각 검토.
- [ ] `[manual-hard]` 선행 plan `2026-05-19-linksky0311-interactive-controls-style.md` 의 실패 AC '글씨·isOn 상태 회귀' 가 이 plan 적용 후 재검증에서 통과한다.
  **검증:** 선행 plan의 시각 검증 시나리오를 동일 씬에서 1회 재수행 — 글씨 변형 없음 + isOn=true 액센트 + DifficultyButton 선택 액센트의 3건이 모두 시각적으로 확인되면 pass.

## Out of Scope

- VolumeSlider의 sprite·트랙 변경 — 본 회귀와 무관, sub-spec 06 What 외.
- `UIScalePunch` 펀치 동작 변경 — 회귀 보고에 펀치 이슈 없음.
- 4종 Selectable의 라벨 폰트·정렬·자동 사이즈 조정 — 본 plan 원인은 9-slice 압축 변형(컨테이너 sprite 문제)이고 텍스트 자체는 정상 셋업이므로 폰트 메트릭은 건드리지 않는다.
- SongRow의 시각 검증 manual-hard — 본 plan은 SongRow의 sprite를 동일 패턴으로 교체하지만 회귀 보고에 SongRow가 없으므로 별도 manual-hard AC를 추가하지 않는다(필요 시 후속 plan).
- DOTween / 외부 트윈 의존 도입 — 본 회귀와 무관.

## Notes

- `Setup`에서 `toggle.isOn = defaultOn` 대입 시 기존 값과 같으면 `onValueChanged`가 발화하지 않으므로, `UpdateVisual(defaultOn)`은 그 대입 *이후*에 명시 호출해야 한다. 본 plan은 Approach 5·6단계에서 `Setup` 마지막 줄에 명시한다.
- 신규 sprite를 외부 도구로 생성하지 않고 기존 `PanelRoundedRect.png`를 그대로 두면서 spriteBorder만 14로 줄이는 대안은 *피한다* — 같은 sprite asset에서 border만 줄이면 9-slice 중앙폭이 늘어나 100×100 InstrumentToggleButton의 모서리 반지름이 사실상 14px로 축소돼 패널과 톤이 어긋난다. 두 sprite를 분리 보존.
- Toggle.colors 중립값에서 `Disabled=(0.5,0.5,0.5,0.3)`은 graphic의 m_Color에 곱해지므로, backgroundImage.color가 OnColor(0.290,0.620,1.000,0.85)일 때 Disabled 진입 시 약 (0.145,0.31,0.5,0.255)로 흐려진다(의도된 시각 효과).

## Handoff

**신규 sprite GUID:** `30dbb1d077de4bb7b01b367491dbae50` (`Assets/SessionPanel/Sprites/ButtonRoundedRect.png`, 64×64, spriteBorder 14/14/14/14)

**3개 스크립트 갱신된 직렬화 시그니처:**
- `DifficultyButtonUI`: NormalColor=RGBA(1,1,1,0.15), SelectedColor=RGBA(0.290,0.620,1.000,0.85). public API 변경 없음.
- `InstrumentToggleButtonUI`: `[SerializeField] Image backgroundImage` 추가. `UpdateVisual(bool)` private 메서드 추가. public Setup 시그니처 변경 없음.
- `AccompanimentToggleUI`: `[SerializeField] Image backgroundImage` 추가. `UpdateVisual(bool)` private 메서드 추가. public Setup 시그니처 변경 없음.

**4종 프리팹 ColorBlock 상태:** 모두 m_NormalColor/Highlighted/Pressed/Selected=(1,1,1,1), Disabled=(0.5,0.5,0.5,0.3) 중립값 적용.

**프리팹 sprite 상태:**
- DifficultyButton/SongRow/AccompanimentToggle 루트 Image sprite → ButtonRoundedRect GUID `30dbb1d077de4bb7b01b367491dbae50`
- InstrumentToggleButton sprite 유지(PanelRoundedRect GUID `371cc7f19be24cc8944b77449df386f1`)

**직렬화 연결:**
- AccompanimentToggleUI.backgroundImage → fileID 9000000000000006 (루트 Image)
- InstrumentToggleButtonUI.backgroundImage → fileID 2672284072590456740 (Background 자식 Image)

**추가 수정 (manual-hard 후):** InstrumentToggleButton.prefab Label RectTransform anchors (0,0)-(1,1) stretch, SizeDelta (0,0), TMP auto-sizing true (min:8, max:36).

**미해결 레이아웃 이슈 (신규 sub-spec으로 이관):**
- InstrumentToggleButton: 악기명 + 토글 버튼 수직 레이아웃
- VolumeSection: 패널 외부 노출 문제
