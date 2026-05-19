# Panel Theme — Glassmorphism Background + Border

**Linked Spec:** [`05-panel-theme.md`](../specs/05-panel-theme.md)
**Status:** `Done`

## Goal

`SessionPanel.prefab` 루트 컨테이너 배경을 글래스모피즘(반투명 다크 + 둥근 모서리) 스타일로 교체하고, 얇고 밝은 반투명 테두리를 별도 자식 Image로 추가한다. 향후 sub-spec 06에서 사용할 공통 색 팔레트(반투명 배경 / 액센트 #4A9EFF / 흰색 텍스트 / 보조 반투명 흰색)를 plan 본문에 박제해 한 곳에서 참조할 수 있도록 한다.

## Context

현재 `SessionPanel.prefab` 루트 GameObject(`fileID: 1151033334272281024`)에는 단일 `Image`(`fileID: 3501293914087151377`)가 부착되어 있고, `m_Sprite: {fileID: 0}` / `m_Color: {r: 0.15, g: 0.15, b: 0.2, a: 0.7}` / `m_Type: 0`(Simple) 로 둔각 직사각형 단색 패널만 표시한다. 둥근 모서리·테두리가 없어 VR 공간 환경에 어울리지 않는다는 게 sub-spec 05의 박제된 문제다.

Sub-spec 05의 4개 What 항목은 모두 시각 자산 변경(Image 컴포넌트 필드 + 둥근 모서리 sprite + Border 자식 추가 + 텍스트 컴포넌트 color)이며 신규 스크립트가 0건이다. 따라서 본 plan은 (a) 둥근 모서리 sprite 1장 생성, (b) `SessionPanel.prefab` 루트 Image 필드 갱신, (c) 첫 자식 위치에 Border Image 추가, (d) 자식 텍스트 컴포넌트의 color 값 갱신만 다룬다. 공통 색 팔레트는 ScriptableObject 자산화하지 않고 본 plan 본문 + sub-spec 의 참조로 박제한다(palette SO 자산화는 sub-spec 범위 밖 — `## Out of Scope` 박제).

`TabBar`/`VolumePanel`/`RhythmGamePanel` 등 자식 섹션 자체의 인터랙티브 요소(버튼/슬라이더/탭 활성 상태) 스타일은 sub-spec 06 범위이므로 본 plan에서 건드리지 않는다. 단, 자식 안의 TextMeshProUGUI color 흰색 통일은 sub-spec 05 What #3(공통 색 팔레트 정의·기본 텍스트 흰색)에 직접 대응하므로 본 plan에 포함한다.

## Verified Structural Assumptions

- `SessionPanel.prefab` 루트 GameObject(`fileID: 1151033334272281024`) `Image`(`fileID: 3501293914087151377`) 현재 값: `m_Sprite: {fileID: 0}`, `m_Color: {r: 0.15, g: 0.15, b: 0.2, a: 0.7}`, `m_Type: 0`(Simple), `m_Material: {fileID: 0}` — `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab L259-291 (2026-05-18)`
- 루트 `RectTransform` `m_SizeDelta: {x: 400, y: 300}`, `m_LocalScale: {x: 0.001, y: 0.001, z: 1}` — 본 plan은 size/scale을 건드리지 않는다. Border Image는 루트 RectTransform을 stretch(anchorMin 0,0 / anchorMax 1,1)로 부모 영역을 채우고 `m_SizeDelta`에 약간의 음수 padding(예: -4,-4)으로 안쪽 살짝 들어와 보이게 한다 — `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab L147-168 (2026-05-18)`
- 루트 `m_Children` 순서: `4251547738239070682` (TabBar), `5751264090231694390` (VolumePanel), `7318693547341281361` (RhythmGamePanel). Border Image는 이 목록의 0번 인덱스(맨 앞)에 삽입해 배경(루트 Image) 위·자식 섹션 아래에 그려지도록 한다. (Unity UI 그리기 순서: 같은 GameObject 내에서는 sibling index 큰 순으로 위에 그려진다 — Border가 sibling[0]이면 배경보다는 위, 자식 섹션보다는 아래.) — `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab L158-162 (2026-05-18)`
- 둥근 모서리 sprite는 신규 자산 `Assets/SessionPanel/Sprites/PanelRoundedRect.png`로 생성하고 Texture Type=Sprite (2D and UI), `Sprite Mode=Single`, `Border` (L,B,R,T) = (32,32,32,32) 9-slice 셋업, `m_Type: 1`(Sliced) 사용. `Assets/SessionPanel/Sprites/` 폴더는 현재 존재하지 않음(생성 대상) — `Glob Assets/SessionPanel/Sprites/** (2026-05-18) → 결과 없음`
- `Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` 가 본 plan 폴더 도메인의 런타임 asmdef. 본 plan은 신규 C# 0건이므로 asmdef references 변경 불필요 — `Glob Assets/SessionPanel/**/*.asmdef (2026-05-18)`
- 공통 색 팔레트 박제(본 plan 결정값, sub-spec 06이 참조할 단일 진실원):
  - 반투명 배경: `RGBA(0.10, 0.10, 0.14, 0.65)` (현 `0.15/0.15/0.2/0.7`보다 약간 어두워지고 alpha 미세 감소)
  - 액센트: `#4A9EFF` = `RGBA(0.290, 0.620, 1.000, 1.0)` (sub-spec What #3 hex 직접 명시)
  - 기본 텍스트(흰색): `RGBA(1, 1, 1, 1)`
  - 보조 텍스트(반투명 흰색): `RGBA(1, 1, 1, 0.65)`
  - 테두리(밝은 반투명): `RGBA(1, 1, 1, 0.18)`

## Approach

1. **둥근 모서리 sprite 생성** — `Assets/SessionPanel/Sprites/PanelRoundedRect.png` 생성. 128x128 PNG, 중앙 64x64는 완전 불투명 흰색, 네 모서리는 corner radius 32px의 둥근 마스크. Unity import settings: Texture Type=Sprite, Sprite Mode=Single, Border (32,32,32,32), Filter=Bilinear, Compression=None(VR 텍스처 압축 artifact 회피). 이 한 장의 sprite는 배경·테두리에 모두 9-slice로 재사용된다.

2. **루트 Image 9-slice 적용** — `manage_components` MCP로 `SessionPanel.prefab` 루트 `Image`의 다음 필드를 갱신:
   - `m_Sprite`: 새로 만든 `PanelRoundedRect.png`의 sprite GUID 참조
   - `m_Color`: `{r: 0.10, g: 0.10, b: 0.14, a: 0.65}` (팔레트의 반투명 배경)
   - `m_Type`: `1` (Sliced)
   - `m_PixelsPerUnitMultiplier`: `1` (기본)

3. **Border 자식 Image 추가** — `manage_gameobject create` 로 `SessionPanel/Border` GameObject를 신규 추가. `RectTransform` anchor stretch (0,0)~(1,1), `m_SizeDelta: {x: 0, y: 0}`, sibling index 0. `Image` 컴포넌트 부착, `m_Sprite`는 같은 `PanelRoundedRect.png`, `m_Color: {r: 1, g: 1, b: 1, a: 0.18}`, `m_Type: 1`(Sliced), `m_RaycastTarget: 0` (테두리는 입력 막지 않음). `m_FillCenter: 0`을 직접 시도 — Unity의 9-slice Image는 `m_FillCenter: 0`이면 가운데가 비고 9-slice 경계만 그려진다(중앙 hole = 테두리만 보임). 만약 `manage_components`가 `m_FillCenter` 필드를 지원하지 않으면 `Edit` 도구로 단일 propertyPath 폴백(plan 명시로 사전 허가).

4. **텍스트 색 통일** — `SessionPanel.prefab` 안의 모든 TextMeshProUGUI 컴포넌트를 `find_gameobjects` + 직렬화 grep으로 찾아내 다음을 결정:
   - 기본 텍스트(타이틀·라벨류): `m_faceColor` `RGBA(255,255,255,255)` (TMP는 0~255 byte 스케일 사용)
   - 보조 텍스트(`NoSongLabel` 등 placeholder): `m_faceColor` `RGBA(255,255,255,166)` (=0.65 alpha의 255 스케일)
   - 라벨 종류 분류는 직렬화 grep으로 GameObject 이름 기준 결정. 분류 매핑은 본 plan Notes에 박제하고, 결정된 매핑을 단일 `manage_components` 호출 시퀀스로 적용.

5. **컴파일·콘솔 확인** — 본 plan은 신규 `.cs` 0건이지만 prefab 수정 후 `read_console(types=["error","warning"])` 0건임을 확인(Image 컴포넌트 직렬화 sanity).

6. **인스턴스화 검증** — `SampleScene.unity` 또는 임시 씬에 `SessionPanel.prefab`을 인스턴스화 시 콘솔 에러 0건. 직렬화 정합성 확인용.

## Deliverables

- `Assets/SessionPanel/Sprites/PanelRoundedRect.png` — 9-slice 둥근 모서리 sprite (신규)
- `Assets/SessionPanel/Sprites/PanelRoundedRect.png.meta` — Unity 자동 생성 sprite import settings (Border 32/32/32/32, Sliced 준비)
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` — 루트 Image 9-slice + 색 변경, Border 자식 추가, TMP 텍스트 색 통일 (수정)

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/SessionPanel/Sprites/PanelRoundedRect.png` 및 `.meta` 파일이 존재하고, `.meta`의 `spriteBorder`가 `{x: 32, y: 32, z: 32, w: 32}`로 설정되어 있다.
  **검증:** `Grep "spriteBorder" Assets/SessionPanel/Sprites/PanelRoundedRect.png.meta` → 단일 매치 `{x: 32, y: 32, z: 32, w: 32}` 라인 확인.

- [ ] `[auto-hard]` `SessionPanel.prefab` 루트 Image(`fileID: 3501293914087151377`)의 `m_Sprite`가 `PanelRoundedRect.png` GUID를 가리키고, `m_Type: 1`(Sliced), `m_Color: {r: 0.1, g: 0.1, b: 0.14, a: 0.65}` 로 박제된다.
  **검증:** `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab L259-291` 으로 해당 Image 블록 직접 확인 + `Grep "PanelRoundedRect" Assets/SessionPanel/Sprites/PanelRoundedRect.png.meta` 의 GUID와 prefab 내 `m_Sprite.guid` 동일.

- [ ] `[auto-hard]` `SessionPanel.prefab` 루트의 첫 자식(sibling index 0)이 `Border` GameObject이며, 그 GameObject에 `UnityEngine.UI.Image` 1개 부착(`m_Type: 1`, `m_FillCenter: 0`, `m_RaycastTarget: 0`, `m_Color: {r: 1, g: 1, b: 1, a: 0.18}`)이고, RectTransform은 `m_AnchorMin: {x: 0, y: 0}`, `m_AnchorMax: {x: 1, y: 1}` 인 stretch 셋업이다.
  **검증:** `Grep "m_Name: Border" Assets/SessionPanel/Prefabs/SessionPanel.prefab` 단일 매치 + 해당 블록 ±30줄 Read로 RectTransform/Image 필드 박제 확인.

- [ ] `[auto-hard]` `SessionPanel.prefab` 안 모든 TextMeshProUGUI 컴포넌트의 `m_faceColor` 가 본 plan 박제 매핑(기본=`(255,255,255,255)` / 보조=`(255,255,255,166)`)과 일치한다.
  **검증:** `Grep "m_faceColor" Assets/SessionPanel/Prefabs/SessionPanel.prefab -A 1 -B 4` 출력 라인을 본 plan Notes의 GameObject 이름→색 매핑과 1:1 대조.

- [ ] `[auto-hard]` `SessionPanel.prefab` 수정 후 `read_console(types=["error","warning"])` 가 0건이고, MCP `manage_scene` 으로 임시 빈 씬에 `SessionPanel.prefab` 인스턴스화 시 콘솔 에러 0건이다(직렬화 정합성·인스턴스화 sanity).
  **검증:** `mcp__UnityMCP__read_console(types=["error"])` 호출 → 0건. 이후 `manage_scene` 으로 빈 씬 새로 만들고 `manage_gameobject create from_prefab` → `read_console(types=["error"])` 재확인 0건.

- [ ] `[auto-soft]` `SessionPanel.prefab` 의 GameObject 총 개수가 본 plan 적용 전 +1(Border 추가) 와 정확히 일치하며, `TabBar`/`VolumePanel`/`RhythmGamePanel` 자식의 `m_Father` fileID가 변경되지 않았다(기존 자식 hierarchy 무손상).
  **검증:** `Grep "GameObject:" Assets/SessionPanel/Prefabs/SessionPanel.prefab -c` 카운트 비교 + 자식 3개의 `m_Father.fileID` Grep 매치가 모두 root RectTransform `4247339568228231875` 유지.

- [ ] `[manual-hard]` Unity 에디터(Game view 또는 VR 헤드셋)에서 `SampleScene` 로드 후 `SessionPanel.prefab` 인스턴스를 활성화하면 배경이 둥근 모서리 다크 반투명 패널로 보이고, 가장자리에 얇은 밝은 반투명 테두리가 보이며, 패널 안 텍스트가 흰색으로 가독성 있게 표시된다. screenshot 1장을 plan Notes에 첨부.
  **검증:** Game view 또는 VR 시뮬레이터에서 SessionPanel 인스턴스를 정면 응시 → 배경의 둥근 모서리 4개, 테두리 1선, 흰 텍스트 3개 영역(타이틀·곡 라벨·placeholder) 모두 시각으로 확인 가능. screenshot 1장 첨부.

## Out of Scope

- 인터랙티브 요소(버튼/슬라이더/토글) 스타일 — sub-spec 06.
- Palette ScriptableObject 자산화 — 본 plan에서는 색 값을 본문에 직접 박제. SO 도입은 sub-spec 06 이상에서 필요 시.
- Blur 효과 (sub-spec 05 Out of Scope 명시).
- Border 두께·corner radius 조정 슬라이더 같은 디자이너 편집 도구.
- 자식 섹션(`TabBar`/`VolumePanel`/`RhythmGamePanel`) 자체의 배경 Image 재스타일 — 인터랙티브 영역으로 sub-spec 06.

## Notes

- TMP `m_faceColor` 분류 매핑(직렬화 grep으로 사전 점검 필요):
  - 기본(흰색 255,255,255,255): 타이틀·탭 라벨·곡명·난이도 라벨·볼륨 슬라이더 라벨류
  - 보조(흰색 255,255,255,166): `NoSongLabel`(placeholder 안내문), 비활성/힌트성 라벨
  - 분류가 모호한 항목은 기본(흰색)으로 처리하고 plan Notes에 명시.
- Border `m_FillCenter: 0` 가 manage_components MCP에서 지원되지 않으면 단일 propertyPath 텍스트 Edit으로 폴백(plan 명시 → 사전 허가).
- 둥근 모서리 sprite 디자인 가이드: 9-slice border 32px / 중앙 64x64 완전 불투명 흰색 / 모서리 부드러운 anti-aliased 곡선. PNG 8-bit RGBA, 128x128, premultiplied alpha 비활성.
- 후속 plan 후보(sub-spec 06): 본 plan의 팔레트 박제(반투명 배경 / 액센트 #4A9EFF / 흰색 / 보조 흰색 0.65 / 테두리 흰색 0.18)를 그대로 import해 버튼·슬라이더·탭 활성 상태에 적용.
- 2026-05-19: manual-hard pass — Unity Editor Game view에서 둥근 모서리 다크 반투명 배경 + 얇은 테두리 + 흰색 텍스트 시각 확인.

## Handoff

SessionPanel.prefab에 glassmorphism 테마를 적용했다.

변경 요약:
- Assets/SessionPanel/Sprites/PanelRoundedRect.png 신규 생성 (128×128 RGBA PNG, 반경 32px 둥근 모서리)
- PanelRoundedRect.png.meta: spriteBorder {32,32,32,32} 9-slice, GUID=371cc7f19be24cc8944b77449df386f1
- SessionPanel.prefab 루트 Image: Sliced + RGBA(0.10,0.10,0.14,0.65)
- Border 자식(sibling[0]): stretch RectTransform, Image Sliced FillCenter=0 RaycastTarget=0 RGBA(1,1,1,0.18)
- TMP 텍스트: NoSongLabel 보조색(255,255,255,166), 나머지 기본 흰색(255,255,255,255)

공통 팔레트(sub-spec 06 참조용):
- 반투명 배경: RGBA(0.10,0.10,0.14,0.65)
- 테두리: RGBA(1,1,1,0.18)
- 기본 텍스트: RGBA(1,1,1,1)
- 보조 텍스트: RGBA(1,1,1,0.65)
- 액센트: RGBA(0.290,0.620,1.000,1.0) (#4A9EFF)
