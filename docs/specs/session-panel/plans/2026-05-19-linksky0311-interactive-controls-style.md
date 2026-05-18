# Interactive Controls Glass Style (Sub-spec 06 단일 plan)

**Linked Spec:** [`06-interactive-controls-style.md`](../specs/06-interactive-controls-style.md)
**Status:** `Done`

## Goal

DifficultyButton / SongRow / AccompanimentToggle / VolumeSlider / InstrumentToggleButton 5종 프리팹에 둥근-모서리 9-slice 배경, glassmorphism 팔레트 기반 상태별 ColorBlock, 선택 시 scale-up 펀치 애니메이션(슬라이더 제외)을 적용해 sub-spec 05의 패널 디자인 언어와 통일한다.

## Context

sub-spec 05에서 SessionPanel 루트에 반투명 어두운 배경(RGBA 0.10/0.10/0.14/0.65) + 둥근 모서리 sprite(`Assets/SessionPanel/Sprites/PanelRoundedRect.png`, GUID `371cc7f19be24cc8944b77449df386f1`, spriteBorder 32/32/32/32) + 액센트 #4A9EFF 팔레트가 박제됐다. 이 패널 위 5종 인터랙티브 컨트롤은 여전히 단색 직사각형(SongRow는 RGBA 0.22/0.28/0.48, InstrumentToggleButton은 0.18/0.22/0.4/0.9, 나머지는 루트 Image 없음) 또는 기본 Unity 흰색이라 시각 톤이 어긋난다.

본 plan은 한 세션 분량으로 다음을 끝낸다.

- 5종 프리팹에 동일 sprite(`PanelRoundedRect`)를 Sliced로 적용 (루트 Image 미존재 2종은 신규 Image 추가).
- 4종 컨트롤(DifficultyButton/SongRow/AccompanimentToggle/InstrumentToggleButton)의 Button·Toggle `ColorBlock`을 팔레트 기반으로 셋업하고 `targetGraphic`을 둥근 배경 Image로 연결.
- VolumeSlider의 Fill·Handle Image를 액센트 #4A9EFF로, Background Image를 반투명 흰색(0.20)으로 변경.
- `Assets/SessionPanel/Scripts/UIScalePunch.cs` 1개 신규 추가 — `Selectable`(Button/Toggle)의 click/value-change에서 짧은 scale-up(1.0→1.08→1.0, 0.12s) 코루틴을 발화. DifficultyButton/SongRow/AccompanimentToggle/InstrumentToggleButton 4종 프리팹 루트에 부착.
- InstrumentToggleButton Checkmark(자식)의 노란색을 흰색(1,1,1,1)으로 통일.

## Verified Structural Assumptions

- `Assets/SessionPanel/Sprites/PanelRoundedRect.png` 존재 (GUID `371cc7f19be24cc8944b77449df386f1`, 128×128, spriteBorder 32/32/32/32) — 신규 sprite 생성 없이 5종 프리팹이 재참조. — `Read Assets/SessionPanel/Sprites/PanelRoundedRect.png.meta (2026-05-19)` + sub-spec 05 handoff
- 5종 프리팹 경로 모두 존재 확인 — `DifficultyButton.prefab` / `SongRow.prefab` / `AccompanimentToggle.prefab` / `VolumeSlider.prefab` (모두 `Assets/SessionPanel/UI/`), `InstrumentToggleButton.prefab` (`Assets/SessionPanel/Prefabs/`). — `Glob Assets/SessionPanel/**/*.prefab (2026-05-19)`
- 프리팹별 컴포넌트 상태(unity-scene-reader Pattern A 박제, 2026-05-19):
  - `DifficultyButton.prefab`: sizeDelta(0,40), Button(Transition=ColorTint), **루트 Image 없음**, Button.targetGraphic 없음. → 루트에 Image 신규 추가 + targetGraphic 갱신 필요.
  - `SongRow.prefab`: sizeDelta(0,50), 루트 Image(color 0.22/0.28/0.48/1, sprite=null, Simple), Button(Transition=ColorTint). → 기존 Image를 Sliced+rounded-sprite로 전환, color=흰색(1,1,1,1).
  - `AccompanimentToggle.prefab`: sizeDelta(0,40), Toggle(Transition=ColorTint), **루트 Image 없음**. → 루트 Image 신규 추가 + Toggle.targetGraphic 갱신.
  - `VolumeSlider.prefab`: sizeDelta(0,60), Slider(min=0/max=1/value=0.5), Background/Fill/Handle Image 모두 흰색. → Fill/Handle 액센트, Background 반투명.
  - `InstrumentToggleButton.prefab`: sizeDelta(100,100), Toggle(Transition=ColorTint, isOn=true), Background(Image RGBA 0.18/0.22/0.4/0.9) → Checkmark(0.95/0.95/0.5/1). → Background를 rounded sprite + ColorTint 흰색base, Checkmark 흰색 통일.
- 공통 팔레트(sub-spec 05 handoff에서 단일 진실원으로 박제): 반투명배경 RGBA(0.10,0.10,0.14,0.65) / 테두리 RGBA(1,1,1,0.18) / 기본텍스트 RGBA(1,1,1,1) / 보조텍스트 RGBA(1,1,1,0.65) / 액센트 RGBA(0.290,0.620,1.000,1.0) (#4A9EFF). — sub-spec 05 handoff (2026-05-19)
- 본 plan 의도 ColorBlock (Selectable 4종 공통): Normal=RGBA(1,1,1,0.15) / Highlighted=Selected=RGBA(0.290,0.620,1.000,0.85) / Pressed=RGBA(0.290,0.620,1.000,1.0) / Disabled=RGBA(0.5,0.5,0.5,0.3) / ColorMultiplier=1.0 / FadeDuration=0.1. Image base color=흰색(1,1,1,1). — 본 plan 박제(sub-spec 06 What 직접 매핑).
- `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef`의 references에 `Unity.TextMeshPro` / `Unity.InputSystem` / `Unity.XR.Interaction.Toolkit` 포함. `UIScalePunch.cs`는 `UnityEngine` + `UnityEngine.UI`만 import하므로 asmdef reference 추가 불필요(`UnityEngine.UI`는 `autoReferenced=true`로 자동 해소). — `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef (2026-05-19)`
- `UnityEngine.UI.Selectable.transition` enum: `None=0` / `ColorTint=1` / `SpriteSwap=2` / `Animation=3`. 본 plan 의도값 = `1`(ColorTint). 4종 프리팹 모두 이미 ColorTint 셋업이므로 변경 없이 colors만 갱신. — Unity UGUI 공개 enum 박제(2026-05-19).

## Approach

1. **DifficultyButton.prefab 갱신** (Unity MCP `manage_asset` / `manage_gameobject` 우선)
   1. 루트에 `Image` 컴포넌트 추가: sprite=PanelRoundedRect, type=Sliced, fillCenter=true, color=RGBA(1,1,1,1), raycastTarget=true.
   2. Button의 `m_TargetGraphic`을 신규 루트 Image fileID로 연결.
   3. Button.colors를 위 박제 ColorBlock 값으로 셋업.
   4. 루트에 `UIScalePunch` 컴포넌트 부착 (auto-detect Button).
2. **SongRow.prefab 갱신**
   1. 기존 루트 Image를 Sliced + sprite=PanelRoundedRect + color=흰색(1,1,1,1)로 전환. (기존 0.22/0.28/0.48 단색은 ColorBlock으로 이전.)
   2. Button.targetGraphic 그대로(이미 루트 Image), Button.colors 박제값 적용.
   3. 루트에 `UIScalePunch` 부착.
3. **AccompanimentToggle.prefab 갱신**
   1. 루트에 Image 신규 추가(동일 sprite/type/color).
   2. Toggle.targetGraphic을 루트 Image로 갱신, Toggle.colors 박제값 적용.
   3. 루트에 `UIScalePunch` 부착 (Toggle 모드).
4. **VolumeSlider.prefab 갱신**
   1. SliderArea/Background Image: color=RGBA(1,1,1,0.20), sprite는 기존 유지(필요 시 PanelRoundedRect 적용 가능하나 슬라이더 트랙은 sub-spec 06 What에 명시되지 않으므로 색상만 변경).
   2. Fill Area/Fill Image: color=RGBA(0.290,0.620,1.000,1.0).
   3. Handle Slide Area/Handle Image: color=RGBA(0.290,0.620,1.000,1.0).
   4. `UIScalePunch`는 부착하지 않는다(슬라이더 값 변경마다 펀치 발화는 의도 외).
5. **InstrumentToggleButton.prefab 갱신**
   1. Background Image: sprite=PanelRoundedRect, type=Sliced, color=흰색(1,1,1,1) (Toggle ColorTint base).
   2. Toggle.targetGraphic은 이미 Background, Toggle.colors 박제값 적용.
   3. Checkmark Image color: RGBA(1,1,1,1).
   4. 루트에 `UIScalePunch` 부착 (Toggle 모드).
6. **`Assets/SessionPanel/Scripts/UIScalePunch.cs` 신규 작성**
   - 네임스페이스: `SessionPanel` (asmdef rootNamespace 일치).
   - 컴포넌트: `[RequireComponent(typeof(RectTransform))]` + `[DisallowMultipleComponent]`.
   - 필드(SerializeField, 모두 inspector 노출): `peakScale=1.08f`, `duration=0.12f` (0.06s up + 0.06s down).
   - `Awake/Start`: 본인 GameObject의 `Button` 또는 `Toggle`을 `GetComponent`로 탐색 → Button.onClick / Toggle.onValueChanged 구독 (Toggle은 isOn==true일 때만 발화).
   - `OnDestroy`: 구독 해제.
   - `IEnumerator Punch()`: 시간 기반(`Time.unscaledDeltaTime`) 코루틴 — 본인 RectTransform.localScale을 (1,1,1)→(peak,peak,1)→(1,1,1) 보간. 활성화 중이 아니면 `gameObject.activeInHierarchy` false 가드.
   - 한 번에 한 코루틴만(`activeCoroutine != null`이면 stop 후 재시작).
   - sub-spec 06 What "scale-up 애니메이션"을 단순·결정적으로 구현. DOTween 등 외부 패키지 의존 없음.
7. **컴파일 대기 & 콘솔 검증** — [`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md)의 스크립트 컴파일 대기 + `read_console` error/warning 0건 절차.
8. **screenshot 시각 검증** — TestSceneSanyo 또는 SampleScene에 SessionPanel 인스턴스를 띄우고, Default/Selected/Disabled 상태별 screenshot 캡처 (사용자 직접 검증 AC로 위임).

## Deliverables

- `Assets/SessionPanel/Scripts/UIScalePunch.cs` — Button/Toggle 선택 시 RectTransform localScale을 1.0→1.08→1.0으로 0.12s 펀치하는 런타임 컴포넌트(신규).
- `Assets/SessionPanel/UI/DifficultyButton.prefab` — 루트 Image 추가(Sliced + PanelRoundedRect) + Button ColorBlock 팔레트 적용 + UIScalePunch 부착(수정).
- `Assets/SessionPanel/UI/SongRow.prefab` — 루트 Image sprite/type/color 갱신 + Button ColorBlock 팔레트 적용 + UIScalePunch 부착(수정).
- `Assets/SessionPanel/UI/AccompanimentToggle.prefab` — 루트 Image 추가 + Toggle ColorBlock 팔레트 적용 + UIScalePunch 부착(수정).
- `Assets/SessionPanel/UI/VolumeSlider.prefab` — Background/Fill/Handle Image color 갱신(수정).
- `Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` — Background Image sprite+type 갱신 + Toggle ColorBlock 팔레트 적용 + Checkmark 색 통일 + UIScalePunch 부착(수정).

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/SessionPanel/Scripts/UIScalePunch.cs`가 존재하며 `namespace SessionPanel` + `class UIScalePunch` + `MonoBehaviour` 상속을 만족하고, `peakScale`/`duration`/`Punch` 식별자가 모두 등장한다.
  **검증:** `Grep "class UIScalePunch\s*:\s*MonoBehaviour"` 1건 매치 + `Grep "namespace SessionPanel"` 매치 + `Grep "peakScale|duration|Punch"` 3종 모두 매치 (대상: `Assets/SessionPanel/Scripts/UIScalePunch.cs`).
- [ ] `[auto-hard]` Unity 도메인 리로드 완료 후 `read_console` types=error/warning 결과 0건(UIScalePunch 신규 작성 + 프리팹 수정 후).
  **검증:** MCP `read_console action=get types=["error","warning"]` 호출 → count==0. `editor_state.isCompiling`이 false가 될 때까지 폴링한 직후에 측정.
- [ ] `[auto-hard]` 5종 프리팹 모두 PanelRoundedRect sprite GUID(`371cc7f19be24cc8944b77449df386f1`)를 적어도 1회 참조한다(VolumeSlider는 트랙/핸들 sprite 변경이 의도 외이므로 제외 가능 — 본 AC는 4종 한정).
  **검증:** `Grep -r "371cc7f19be24cc8944b77449df386f1" Assets/SessionPanel/UI/DifficultyButton.prefab Assets/SessionPanel/UI/SongRow.prefab Assets/SessionPanel/UI/AccompanimentToggle.prefab Assets/SessionPanel/Prefabs/InstrumentToggleButton.prefab` 4개 파일 모두 매치.
- [ ] `[auto-hard]` 4종 Selectable 프리팹(DifficultyButton/SongRow/AccompanimentToggle/InstrumentToggleButton)에 `UIScalePunch` MonoBehaviour가 루트에 부착돼 있다(직렬화 정합).
  **검증:** MCP `find_gameobjects search_method=by_component search_term=UIScalePunch` 결과에 4개 프리팹 루트 instanceId 모두 등장 + 각 프리팹 YAML을 `Grep "UIScalePunch"` 했을 때 m_Script 참조 등장.
- [ ] `[auto-soft]` 4종 Selectable의 ColorBlock이 박제값과 일치한다(`m_Colors`의 `m_NormalColor.a≈0.15`, `m_HighlightedColor.r≈0.290 g≈0.620 b≈1.0`, `m_DisabledColor.a≈0.3`).
  **검증:** 각 프리팹 YAML을 Read해 `m_Colors:` 블록의 색상 값을 본 plan의 박제 팔레트와 비교. 부동소수 오차 ±0.01 허용. 실패 시 노트에 기록 후 후속 plan 분기.
- [ ] `[auto-soft]` VolumeSlider Fill/Handle Image color가 액센트(0.290/0.620/1.0/1.0)에 일치한다.
  **검증:** `Assets/SessionPanel/UI/VolumeSlider.prefab` YAML에서 Fill/Handle Image의 `m_Color` 블록 추출 후 액센트값과 비교.
- [ ] `[manual-hard]` Unity Editor에서 SessionPanel을 띄우고 5종 컨트롤이 둥근 모서리로 렌더링되며, DifficultyButton/SongRow/AccompanimentToggle을 마우스 hover/press하면 액센트 #4A9EFF 톤으로 전환됨이 시각적으로 확인된다. InstrumentToggleButton의 isOn 토글 시 Checkmark가 흰색으로 점등되고, VolumeSlider 채움/핸들이 액센트로 표시된다.
  **검증:** TestSceneSanyo 또는 SampleScene 로드 → SessionPanel 인스턴스 활성 → Default/Selected/Disabled 상태 screenshot 1장씩 캡처 + VolumeSlider 50%·100% screenshot 1장 → 시각 검토.
- [ ] `[manual-hard]` 4종 Selectable을 클릭/토글했을 때 1.0→1.08→1.0 펀치 애니메이션이 ~0.12s 이내에 1회 재생되고 잔류 스케일이 없다(localScale=(1,1,1) 복귀).
  **검증:** Play 모드 진입 후 Inspector에서 RectTransform.localScale을 관찰하며 4종 컨트롤 각 1회 클릭/토글 → 펀치 재생 후 (1,1,1) 복귀 확인.

## Out of Scope

- 패널 컨테이너 배경/테두리 — sub-spec 05에서 다룸.
- 컨트롤 크기·위치·레이아웃 변경 — sub-spec 06 What 제외 항목.
- Hover 상태 별도 처리 — VR 인터랙션 모델상 의도 외(spec Out of Scope 명시).
- Particle/VFX 피드백 — spec Out of Scope.
- 기존 인터랙션 로직(클릭 핸들러, 슬라이더 값 바인딩) 변경 — spec Out of Scope.
- DOTween 등 외부 트윈 라이브러리 도입 — 단순 코루틴으로 충분, 의존성 부담 회피.
- VolumeSlider 트랙/핸들 sprite를 PanelRoundedRect로 교체 — sub-spec 06 What이 색상만 명시하므로 본 plan은 색상 변경에 한정.

## Notes

- `UIScalePunch` 펀치는 Toggle의 onValueChanged에서 `isOn==true`일 때만 발화한다. off-전환은 무펀치(사용자가 "선택" 상태일 때만 피드백을 본다는 의도).
- screenshot AC는 캡처 파일 저장 위치를 별도 표준화하지 않는다 — 검토 후 폐기.
- 후속 plan 후보: VolumeSlider Background sprite도 PanelRoundedRect로 교체해 트랙 모서리를 둥글게 다듬는 시각 정합 plan(현재 sub-spec 06 범위 외).
- 2026-05-19: 후속 plan `2026-05-19-linksky0311-interactive-controls-fix.md` 추가. 완료 후 본 plan의 manual-hard 재검증 필요(9-slice 압축 변형 / isOn 회색 / DifficultyButton 선택 색 미적용 3건).
