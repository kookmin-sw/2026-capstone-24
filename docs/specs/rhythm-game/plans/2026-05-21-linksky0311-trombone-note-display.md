# Trombone 파셜 패널 5종 (반원형) 노트 디스플레이

**Linked Spec:** [`13-trombone-note-display.md`](../specs/13-trombone-note-display.md)
**Status:** `Ready`

## Goal

트롬본 세션이 시작되면 5개의 파셜(배음) 패널이 트롬본 앞 수평 반원형으로 자동 생성되고, 차트의 MIDI 노트가 `InstrumentLaneConfig`(파셜 인덱스 0~4 매핑)를 통해 해당 파셜 패널로 라우팅되어 낙하·판정된다. 이를 위해 `TromboneNoteDisplayAdapter`(INoteDisplayController 구현), `Trombone_LaneConfig.asset`(5엔트리 SO), Trombone.prefab 와이어링(LaneConfig·Adapter 자식·PanelAnchor) 3건을 한 번에 갖춘다.

## Context

- spec 13은 "트롬본 앞 수평 반원형 5패널, 각 패널은 파셜 1개에 대응, 차트 MIDI → 파셜 인덱스(0~4) 매핑"을 요구한다. 슬라이드 7포지션을 패널 내 별도 레인으로 분리하는 것은 Out of Scope이며, 노트는 "요구 슬라이드 포지션의 이론적 3D 위치"에서 스폰되어 판정선으로 이동한다. **본 plan은 슬라이드 위치 시각화 축은 패널 내 X 오프셋(0~6 → 좌→우 매핑)으로 한정**하며, 별도 패널 분할이나 슬라이드 가이드라인 렌더링은 후속 plan으로 미룬다.
- `RhythmGameHost.StartSession()` (lines 64~80, `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs`)은 `instrument.GetComponentInChildren<INoteDisplayController>(true)`를 *우선* 채택하고, 없으면 inspector의 `noteDisplayPanel` 폴백을 쓴다. 즉 Trombone.prefab 자식에 `TromboneNoteDisplayAdapter`(INoteDisplayController) 하나만 부착하면 host 코드 변경 없이 자동 사용된다. 드럼이 `DrumNoteDisplayAdapter`로 답습한 패턴(Drum.prefab 자식).
- `Trombone.cs`의 음역 매핑 박제: `baseToneMidiNote = 33` (A1, line 29). `partialOffsetsSemitones = { 12, 19, 24, 28, 31 }` (5개, `TrombonePartialController.cs:18`). 슬라이드는 `-SlideIndex` semitones (`Trombone.cs:142`, range 0~6, total 7 positions). 즉 effective MIDI = `33 + partialOffset[i] - slideIndex`. 슬라이드 0(가장 짧음) 기준 파셜 0~4의 base MIDI는 각각 45, 52, 57, 61, 64 (= A2, E3, A3, C#4, E4) — 이게 각 파셜 패널의 "대표 MIDI"이자 LaneConfig에 등록할 값이다.
- 슬라이드 1~6 포지션은 base MIDI에서 각각 −1, −2, −3, −4, −5, −6 semitones. 즉 한 파셜 패널이 커버하는 MIDI 노트 집합은 `{ baseMidi, baseMidi-1, …, baseMidi-6 }` (7개). 차트가 보낼 수 있는 MIDI 노트 총합은 5파셜 × 7슬라이드 = 35개. **본 plan은 차트의 MIDI 노트가 어느 파셜에 속하는지 빠르게 판별하기 위해 LaneConfig에 35건 모두 등록**하고, 각 엔트리의 `laneIndex`를 0~4 파셜 인덱스로 박는다. `InstrumentLaneConfig.TryGetLane(midi, out laneIndex)`가 그대로 작동(같은 laneIndex가 여러 midiNote에 매핑돼도 lookup은 정상 — `TryGetLane`은 처음 매칭에서 즉시 return).
- `NoteDisplayPanel`은 `IsSingleLane` 분기를 이미 보유(`NoteDisplayPanel.cs:145, 283~307, 374~388`). 단일 레인 모드에선 노트가 패널 중앙에 폭 80%로 스폰. 본 plan에서 만들 5개 파셜 패널은 각각 **단일-레인 모드**로 동작(파셜 1개 = 레인 1개). 슬라이드 X 오프셋 시각화는 본 plan에서는 *생략*(spec 13 본문도 "이론적 3D 위치"라고만 명시하며 수평 X 오프셋 명시 강제는 아님 — 후속 plan으로 시각 fidelity 별도 다룸). 다만 spec 본문의 "차트가 요구하는 슬라이드 포지션의 이론적 3D 위치"를 일부라도 만족시키기 위해, 어댑터에서 노트 스폰 시 X 오프셋을 패널 폭의 ±40% 범위에서 `slideOffset = (slideIndex/6) * panelWidth*0.8 - panelWidth*0.4`로 적용하도록 *간단 hook*만 박는다. 이건 본 plan의 **선택적** 시각화이며 AC는 "5패널 생성 + 파셜 라우팅" 핵심에 집중하고 슬라이드 X 오프셋은 manual 확인 1건으로만 둔다.
- 반원형 배치: trombone 본체(=`tromboneRoot`의 forward 축)의 정면 반원을 5등분해 패널을 둔다. 트롬본 정면(forward) 기준 각도 `θ_i = -60° + i * 30°` (i = 0..4 → -60, -30, 0, +30, +60) 으로 anchor 중심에서 반경 `R`(예: 1.2m) 떨어진 위치에 패널을 놓고, 각 패널은 anchor 중심을 바라보게 회전(드럼 어댑터의 `ComputePanelRotation` 패턴과 동일). 파셜 인덱스 순서 ↔ 좌→우 배치는 spec 13 본문에 명시되지 않았으므로 **본 plan은 "파셜 인덱스 0(가장 낮음)이 왼쪽(-60°), 파셜 4(가장 높음)가 오른쪽(+60°)"**로 정한다 — 음높이 상승이 좌→우로 직관적으로 보이게 함. 이 배치 규칙은 후속 plan에서 사용자 피드백에 따라 조정 가능하며 어댑터 SerializeField로 쉽게 swap.
- 본 plan은 *트롬본 세션 노트가 실제 채점되는지*를 검증하는 manual AC를 포함하지만, 차트 측 트롬본 트랙 제작·instrumentId 매핑 정합·세션 진입(곡 목록 행 활성화 등)은 sub-spec 14(Trombone Session Panel) 및 후속 chart 제작 plan의 책임이다. 본 plan은 "어댑터·LaneConfig·prefab wiring이 도착하면 host가 자동으로 5패널 디스플레이를 띄운다"는 단위 동작까지만 책임진다 — 트롬본 트랙이 든 vmsong이 아직 없다면 manual AC는 "테스트용 임시 차트" 또는 "기존 piano 차트의 채널·instrument 메타를 trombone으로 임시 치환한 파일"로 재현한다.

## Verified Structural Assumptions

- `Trombone.prefab` 의 `InstrumentBase` 직렬화 슬롯: `instrumentId: Trombone`(line 915), `laneConfig: {fileID: 0}` 미할당(line 914), `_panelAnchor: {fileID: 0}` 미할당(line 917). 본 plan에서 `laneConfig`와 `_panelAnchor`를 모두 채움 — `Grep laneConfig|instrumentId|_panelAnchor Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-21)`.
- `Trombone.cs` 음역 매핑: `baseToneMidiNote = 33` (line 29), `partialOffsetsSemitones = { 12, 19, 24, 28, 31 }` (`TrombonePartialController.cs:18`), 슬라이드 = `-SlideIndex` semitones (`Trombone.cs:141~142`), `SlidePositionCount = 7` (`TromboneSlideController.cs:17`). 따라서 슬라이드 0 기준 파셜 0~4 base MIDI = {45, 52, 57, 61, 64} — `Read Assets/Instruments/Trombone/Scripts/Trombone.cs (2026-05-21) lines 29, 141~147`, `Read Assets/Instruments/Trombone/Scripts/TrombonePartialController.cs (2026-05-21) lines 18, 28~37`, `Read Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs (2026-05-21) line 17`.
- `RhythmGameHost.StartSession()` 자동 채택 경로: `instrument.GetComponentInChildren<INoteDisplayController>(true)`가 폴백보다 우선(lines 64~72). `Begin/Hide/OnJudged/Completed` 4-method를 구현한 컴포넌트가 trombone 자식 어디에 부착되든 자동 활성화 — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs (2026-05-21) lines 50~85`.
- `NoteDisplayPanel.IsSingleLane`은 `laneConfig != null && laneConfig.LaneCount == 1`로 판정(line 145). 단일 레인 모드에서 노트 배치는 `localX = pr.x + pw*0.5f`, `noteW = pw*0.80f`(lines 374~379) — 본 plan에서 슬라이드 X 오프셋 시각화 hook을 도입하려면 패널 내 위치 산출 변경이 필요하나, 본 plan 1차에서는 **건드리지 않고** 단순 중앙 배치를 유지 (Adapter 단에서 변경 없이 host 인터페이스만 사용). 슬라이드 X 시각화는 후속 plan 후보로 박제 — `Read Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs (2026-05-21) lines 145, 283~307, 360~410`.
  - **단, 본 plan의 파셜 패널은 LaneConfig 자체가 `LaneCount==1`이 되도록 어댑터에서 *런타임 단일-레인 임시 SO*를 파셜별로 생성**한다. 드럼 어댑터의 `InstrumentLaneConfig.CreateSingleNote(byte)` 헬퍼를 그대로 답습. 단 SingleNote는 노트 1개만 등록되므로 슬라이드 0~6 포지션 7건을 모두 표시하려면 multi-note single-lane이 필요 → 본 plan에서 **새 헬퍼** `InstrumentLaneConfig.CreateSingleLane(IReadOnlyList<byte> notes)`를 1개 추가(드럼 헬퍼와 분리, 한 줄 정도의 표면 추가). 모든 7개 노트는 `laneIndex=0`으로 등록되어 패널이 단일-레인 분기를 통과 — `Read Assets/Instruments/_Core/Scripts/InstrumentLaneConfig.cs (2026-05-21) lines 86~94`.
- `DrumNoteDisplayAdapter`의 답습 패턴: `Begin` → `Hide` → 자식 zone 순회로 패널 Instantiate → `SetLaneConfig` + `Show` → `Completed` 이벤트 집계 → `Hide` 시 일괄 destroy. 패널 회전은 `ComputePanelRotation(anchor, worldPos)`로 anchor를 바라보게 (`DrumNoteDisplayAdapter.cs:139~150`). 본 plan은 동일 라이프사이클·회전식을 그대로 채택 — `Read Assets/RhythmGame/Scripts/Runtime/Display/DrumNoteDisplayAdapter.cs (2026-05-21) lines 31~150`.
- asmdef 의존: 새 `TromboneNoteDisplayAdapter.cs`는 `Assets/RhythmGame/Scripts/Runtime/Display/` 폴더에 두며 어셈블리는 `RhythmGame.Runtime`. 이 asmdef는 이미 `Instruments`를 참조한다 — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef (2026-05-21) lines 1~19`. `Instruments`는 `Hands` / `Unity.InputSystem` / `Unity.XR.Interaction.Toolkit`을 참조하므로 본 plan이 `Trombone`·`InstrumentBase`·`InstrumentLaneConfig`·`InstrumentTeleportColliderBinder`를 import하는 데 신규 reference 불필요 — `Read Assets/Instruments/Instruments.asmdef (2026-05-21)`.
- LaneConfig 자산 위치 컨벤션: 기존 `Piano_LaneConfig.asset` / `Drum_LaneConfig.asset`이 모두 `Assets/RhythmGame/Data/`에 위치. 본 plan도 `Assets/RhythmGame/Data/Trombone_LaneConfig.asset`으로 신규 SO를 만들고 prefab의 `laneConfig` 슬롯에 박는다 — `Glob Assets/RhythmGame/Data/*_LaneConfig.asset (2026-05-21)`.
- `RhythmAccompaniment.BuildInstrumentMap` OrdinalIgnoreCase 매칭 — Trombone prefab `instrumentId: Trombone` 과 vmsong `instrument=trombone` (예상)은 매칭됨(piano 케이스와 동형). 따라서 instrumentId 정규화는 본 plan 범위 밖이며 그대로 둠 — `Read Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs (2026-05-18 박제 재인용) lines 96~112`(이전 plan에서 박제).

## Approach

1. **`InstrumentLaneConfig.CreateSingleLane(IReadOnlyList<byte>)` 헬퍼 추가** — `Assets/Instruments/_Core/Scripts/InstrumentLaneConfig.cs`에 정적 메서드 1개 추가. 모든 입력 노트를 `laneIndex=0`으로 묶어 런타임 SO를 만든다. 시그니처: `public static InstrumentLaneConfig CreateSingleLane(IReadOnlyList<byte> midiNotes)`. 기존 `CreateSingleNote(byte)`는 그대로 유지(드럼이 사용).
2. **`Trombone_LaneConfig.asset` SO 생성** — `Assets/RhythmGame/Data/Trombone_LaneConfig.asset` (메뉴: `VirtualMusicStudio/Rhythm/Instrument Lane Config`). `lanes` 리스트에 35건 등록:
   - 파셜 0 (laneIndex=0): MIDI {45, 44, 43, 42, 41, 40, 39}
   - 파셜 1 (laneIndex=1): MIDI {52, 51, 50, 49, 48, 47, 46}
   - 파셜 2 (laneIndex=2): MIDI {57, 56, 55, 54, 53, 52, 51}
   - 파셜 3 (laneIndex=3): MIDI {61, 60, 59, 58, 57, 56, 55}
   - 파셜 4 (laneIndex=4): MIDI {64, 63, 62, 61, 60, 59, 58}
   - 파셜 간 MIDI 겹침이 존재(예: 51은 파셜 1·2 둘 다)지만 `TryGetLane`은 최초 매칭에서 즉시 return하므로 *등록 순서를 파셜 0→4 오름차순*으로 정렬해 낮은 파셜 우선 라우팅. 동일 음을 두 파셜로 칠 수 있는 경우 차트 작성자가 의도하는 파셜이 낮은 쪽으로 정해진다 — Notes에 박제.
3. **`TromboneNoteDisplayAdapter.cs` 신규 작성** — `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs`. 시그니처:
   - `public class TromboneNoteDisplayAdapter : MonoBehaviour, INoteDisplayController`
   - SerializeField: `NoteDisplayPanel noteDisplayPanelPrefab`, `float radius = 1.2f`, `float arcDegrees = 120f`, `float panelTiltDegrees = 30f`, `Transform centerAnchor`(null이면 host의 `PanelAnchor` 폴백), `byte[] partialBaseMidi = {45,52,57,61,64}`, `int slidePositionsPerPartial = 7`.
   - `Begin(chart, judgedChannel, clock)`: host(=`GetComponentInParent<InstrumentBase>()`)의 `LaneConfig`를 읽음. null이면 즉시 `Completed?.Invoke()` 후 return (드럼 어댑터 답습). 그렇지 않으면 5개 파셜 슬롯을 순회해 각 파셜별로:
     - 슬라이드 0~6 모든 MIDI 노트 7건을 `InstrumentLaneConfig.CreateSingleLane(...)`로 단일-레인 런타임 SO 생성.
     - 패널 인스턴스화 → `transform.position = ComputePanelWorldPos(i)` → `transform.rotation = ComputePanelRotation(centerAnchor, panel.position)` (드럼 어댑터 식 그대로 import) → `SetLaneConfig(singleConfig)` + `Show(chart, judgedChannel, clock)`.
     - `Completed` 이벤트 구독해 `_pendingPanelCount` 집계.
   - `Hide()`: 생성한 패널·런타임 SO 일괄 destroy + 이벤트 해제 (드럼 어댑터 그대로).
   - `OnJudged(JudgmentEvent e)`: 본 plan에서는 *모든 패널에 broadcast*(드럼은 noteToPanel 라우팅을 썼지만 본 plan은 파셜 정확 인덱싱이 어렵고 — slideIndex가 OnJudged payload에 없음 — 모든 파셜에 popup을 띄워도 잘못된 곳이 안 뜨려면 추가 정보가 필요). 차선책으로 `judgedChannel`의 e.midiNote를 LaneConfig.TryGetLane으로 파셜 인덱스 역조회 → 해당 인덱스 패널에만 popup 전달. Drum 어댑터의 `noteToPanel` dictionary 패턴 답습.
   - `ComputePanelWorldPos(int partialIndex)`: `θ = -arcDegrees/2 + partialIndex * arcDegrees/4` (5패널이면 4개 간격 → -60, -30, 0, +30, +60 when arcDegrees=120). `forward = centerAnchor.forward` 수평 성분, `θ`만큼 yaw 회전한 방향에 `radius` 떨어진 위치 + `centerAnchor.position.y` 유지.
   - `ComputePanelRotation`: 드럼 어댑터 라인 139~150과 동일 식(패널이 centerAnchor를 향함, `panelTiltDegrees`만큼 위로 기울임).
4. **`Trombone.prefab` 와이어링 3건** — MCP `manage_prefabs`(또는 manage_scene) 우선, 실패 시 텍스트 Edit fallback. (단일 진실원: [`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md))
   - (a) `InstrumentBase.laneConfig` ← `Trombone_LaneConfig.asset` (line 914 fileID 갱신).
   - (b) `_panelAnchor` 미할당 → trombone의 mouthpiece 근처 child Transform 1개를 신규 생성(GameObject "PanelAnchor", local position 결정: trombone 본체의 forward+up 약간 위) 또는 기존 `mouthOnPlayer`/`mouthPieceOnTrombone`/`tromboneRoot` 중 *trombone 본체 정면 기준점*으로 사용 가능한 transform을 재활용. **본 plan은 prefab 안에 신규 child "PanelAnchor"를 만들지 않고 `tromboneRoot` 자체를 `_panelAnchor`로 박는다** — 어댑터의 `centerAnchor`는 별도 SerializeField로 같은 `tromboneRoot`를 넘겨 받을 수 있고, `_panelAnchor`는 일반 InstrumentBase 계약상 SessionPanel 위치용이므로 두 용도가 분리됨. (PanelAnchor 신규 child 추가는 prefab 골격 변경 비용이 커서 본 plan에서 회피, Notes에 기재.)
   - (c) Trombone root 자식으로 빈 GameObject `TromboneNoteDisplay` 신규 생성(MCP create_gameobject + add_component) → `TromboneNoteDisplayAdapter` 컴포넌트 부착 → SerializeField:
     - `noteDisplayPanelPrefab` ← 드럼이 사용 중인 동일 NoteDisplayPanel prefab 참조 (혹은 단일-레인 모드 검증된 piano와 공용). 기존 `noteDisplayPanelPrefab`의 자산 경로는 `Glob` 으로 사전 확인하여 박제.
     - `centerAnchor` ← `tromboneRoot` Transform.
     - 나머지 SerializeField는 디폴트 사용.
5. **컴파일·테스트 게이트** — 위 1, 3, 4 변경 후 `unity-mcp-workflow` skill의 컴파일 대기·`read_console` 절차로 errors 0 확인. 이후 `unity-test-runner` sub-agent를 1회 호출해 EditMode 회귀를 확인(InstrumentLaneConfigTests / RhythmGameHostTests 등 기존 테스트 pass). MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 진행.
6. **수동 재현 시나리오 박제** — 트롬본 세션을 진입할 차트가 아직 없는 상태이므로 검증을 위해 임시 차트 `Assets/StreamingAssets/Songs/_devtest-trombone-1.vmsong`을 1개 작성(혹은 기존 nabia-piano-1.vmsong을 복사해 `instrument=trombone`, `channel=2` 등으로 헤더만 치환, 본 plan에서만 사용). 본 plan 종료 시 `_devtest-*` 파일은 그대로 두되 Notes에 후속 정리 plan 후보로 기록. 정식 트롬본 차트 제작은 별도 spec(`<song>-trombone-N.vmsong`) 책임.

## Deliverables

- `Assets/Instruments/_Core/Scripts/InstrumentLaneConfig.cs` — `CreateSingleLane(IReadOnlyList<byte>)` 정적 헬퍼 1개 추가. 기존 API 변경 없음.
- `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` — INoteDisplayController 구현, 5개 파셜 패널을 반원형으로 생성·라우팅·정리.
- `Assets/RhythmGame/Data/Trombone_LaneConfig.asset` — 35건 LaneEntry(5파셜 × 7슬라이드) 등록 SO.
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — (a) `laneConfig` 슬롯에 Trombone_LaneConfig 박제, (b) `_panelAnchor` 슬롯에 `tromboneRoot` 박제, (c) 자식 `TromboneNoteDisplay` GameObject + `TromboneNoteDisplayAdapter` 부착(SerializeField 와이어링 포함).
- `Assets/StreamingAssets/Songs/_devtest-trombone-1.vmsong` — 검증용 임시 차트(트롬본 단일 트랙, 파셜 0~4 음역을 1마디씩 순회). 본 plan AC manual 재현 전용.

## Acceptance Criteria

- [ ] `[auto-hard]` `InstrumentLaneConfig.cs`에 `public static InstrumentLaneConfig CreateSingleLane(IReadOnlyList<byte>` 시그니처가 존재한다.
  **검증:** `Grep -n "public static InstrumentLaneConfig CreateSingleLane" Assets/Instruments/_Core/Scripts/InstrumentLaneConfig.cs` 결과 1줄.
- [ ] `[auto-hard]` `TromboneNoteDisplayAdapter.cs`가 존재하며 `INoteDisplayController`를 구현한다.
  **검증:** `Grep -n "class TromboneNoteDisplayAdapter.*INoteDisplayController" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 1줄.
- [ ] `[auto-hard]` `Trombone_LaneConfig.asset`이 존재하고 35건 LaneEntry를 보유하며, `laneIndex` 값은 0~4 사이에만 분포한다.
  **검증:** `Glob Assets/RhythmGame/Data/Trombone_LaneConfig.asset` 1건 + `Grep -c "midiNote:" Assets/RhythmGame/Data/Trombone_LaneConfig.asset` ≥ 35 + `Grep -n "laneIndex: 5" Assets/RhythmGame/Data/Trombone_LaneConfig.asset` 결과 0건 (5 이상 인덱스 없음).
- [ ] `[auto-hard]` `Trombone.prefab`의 `laneConfig` 슬롯이 더 이상 `{fileID: 0}`이 아니며(비-zero fileID 또는 guid 참조), `_panelAnchor` 슬롯도 비-zero이다.
  **검증:** `Grep -n "laneConfig:" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과가 `{fileID: 0}` 단독이 아니라 guid/fileID를 포함, `Grep -n "_panelAnchor:" ...` 동일.
- [ ] `[auto-hard]` `Trombone.prefab` 의 자식 GameObject 중 `TromboneNoteDisplayAdapter` 컴포넌트가 부착된 항목이 정확히 1개 존재한다.
  **검증:** `Grep -n "TromboneNoteDisplayAdapter" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과 1줄(혹은 컴포넌트 직렬화 블록 1개).
- [ ] `[auto-hard]` EditMode 테스트 스위트가 컴파일 후 errors 0이고, 기존 `InstrumentLaneConfig` 관련 테스트 + `RhythmGameHost` 관련 테스트가 모두 통과한다.
  **검증:** `unity-test-runner` sub-agent 호출 결과 EditMode 전체 pass. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC를 `pass(skip)`으로 처리하고 Notes에 사유 기록.
- [ ] `[manual-hard]` Trombone을 grab한 상태에서 트롬본 트랙이 든 차트(예: `_devtest-trombone-1.vmsong`)로 세션을 시작하면 5개 파셜 패널이 트롬본 정면 수평 반원형(약 ±60° 호)으로 동시에 가시화되며, 각 패널은 anchor 중심을 바라보는 방향으로 회전된다.
  **검증:** Editor Play → 트롬본 grab → 임시 트롬본 차트 세션 진입 → Scene/Game 뷰에서 5패널 위치·회전 시각 확인(`find_gameobjects search_term="NoteDisplayPanel(Clone)"` 결과 5건이면 보조 확인 가능).
- [ ] `[manual-hard]` 임시 트롬본 차트의 노트가 차트 시간축에 따라 *해당 파셜 패널*에 낙하한다. 구체적으로 파셜 0 음(MIDI 45)은 가장 왼쪽 패널, 파셜 4 음(MIDI 64)은 가장 오른쪽 패널에 낙하한다.
  **검증:** Editor Play → 트롬본 세션 진입 → 차트의 첫 5개 노트가 파셜 0→4 순서로 좌→우 패널에 각각 등장하는지 시각 확인.
- [ ] `[manual-hard]` 플레이어가 올바른 파셜+슬라이드를 유지한 채 판정선에 노트가 도달하면 Perfect/Good/Miss 팝업이 *그 파셜 패널 위*에 표시된다(다른 패널엔 표시 안 됨).
  **검증:** Editor Play → 트롬본 세션 진입 → 임의 노트 판정 → JudgmentPopup이 노트가 떨어진 패널에만 나타나는지 시각 확인.
- [ ] `[manual-hard]` 트롬본이 아닌 악기(예: 피아노)를 grab한 상태에서 세션을 시작하면 트롬본 파셜 패널이 표시되지 않는다(드럼 어댑터의 instrument-scoped 활성화 패턴과 동일하게 trombone 본체에 부착돼 있어 trombone이 활성 악기가 아닐 때 instantiate되지 않는다).
  **검증:** Editor Play → 피아노 grab → 피아노 차트 세션 진입 → Scene/Hierarchy에 `NoteDisplayPanel(Clone)`이 1개(피아노 폴백)만 있고 5개가 동시에 뜨지 않는지 확인. 또는 trombone GameObject 비활성 상태에서 host의 `instrument.GetComponentInChildren<INoteDisplayController>` 결과가 piano 쪽으로 라우팅됨을 read_console 로그로 확인.

## Out of Scope

- 슬라이드 7포지션을 패널 내 별도 X 오프셋·레인 분할로 시각화. 본 plan은 단일-레인 중앙 배치까지만 보장. 후속 plan 후보: `NoteDisplayPanel`에 slideIndex 기반 X 오프셋 hook 추가 + 어댑터에서 chart MIDI → slideIndex 역추론.
- 트롬본 정식 차트(`<song>-trombone-N.vmsong`) 제작. 본 plan은 `_devtest-trombone-1.vmsong` 한 건의 검증용 임시 차트만 둠.
- `Trombone Session Panel`(sub-spec 14) — 곡 선택/난이도/반주 토글 UI에서 trombone이 1급 시민으로 인식되는지. 본 plan은 디스플레이만, session-panel은 별도 spec.
- `instrumentId: Trombone` → `trombone` 정규화. 현재 OrdinalIgnoreCase 비교로 vmsong과 매칭되며, 변경 시 InstanceVolume PlayerPrefs 키 마이그레이션 부작용이 있어 본 plan 범위 밖.
- 파셜 인덱스 배치 순서(왼쪽=낮음 vs 왼쪽=높음) 사용자 검증. 본 plan은 "왼쪽=파셜 0 = 가장 낮은 배음"으로 박지만, 사용자 피드백 후 swap이 필요하면 어댑터 SerializeField로 1줄 swap 가능 — 후속 plan으로 처리.
- 트롬본 본체 정면(forward) 축이 prefab 좌표계에서 어느 축인지(±X / ±Y / ±Z)에 대한 사전 미세 조정. 본 plan은 `tromboneRoot.forward`를 그대로 사용하고, 시각 확인에서 호 방향이 어색하면 어댑터의 `centerAnchor`를 다른 child로 변경하거나 forward 부호 반전 SerializeField를 추가 — manual AC에서 확인.
- 자동화된 노트 카운트 / 채널 발화 검증 후크. 09 sub-spec Notes의 후속 plan 후보로 둠.

## Notes

- LaneConfig 등록 순서 박제: 파셜 0~4 오름차순으로 등록(파셜 0 7건 → 파셜 1 7건 → ... → 파셜 4 7건). `TryGetLane`는 최초 매칭 즉시 return하므로 MIDI 51처럼 파셜 1·2가 겹치는 음은 *낮은 파셜로 라우팅*된다. 차트 작성자가 의도하는 파셜이 다르면 차트 측에서 의도된 파셜의 다른 슬라이드 위치로 음역 우회 필요 — 본 plan은 이 정책을 픽스하고 후속 spec(차트 제작 가이드)에 명시.
- 차트 측 `judgedChannel`이 트롬본 트랙으로 자동 매핑되려면 `RhythmGameHost`가 chart에서 trombone 채널을 골라 `Begin(chart, judgedChannel, clock)`으로 전달해야 한다. 이 라우팅은 SessionPanel/Section controller 책임이며 본 plan에서는 *어댑터는 호출된 judgedChannel을 그대로 사용한다*는 전제만 둠. trombone channel 매칭에 회귀가 발견되면 sub-spec 14(Session Panel) 또는 별도 plan으로 분리.
- `_panelAnchor` 박제 결정: 본 plan은 신규 child "PanelAnchor"를 prefab에 추가하는 대신 `tromboneRoot`를 그대로 박는다. SessionPanel이 trombone 위에 뜨는 위치를 더 미세하게 조정해야 하면 후속 plan에서 PanelAnchor child 추가. Trombone Session Panel(sub-spec 14)이 그 책임을 가져갈 가능성 높음.
- `_devtest-trombone-1.vmsong`은 본 plan AC manual 검증을 위한 임시 자산이다. 본 plan 완료 후 정식 트롬본 차트가 생기면 삭제 또는 `_archive` 이동. 본 plan은 파일을 streamingAssets에 두지만 `_` prefix로 dev-only 의도를 명시.
- 후속 plan 후보: (a) 슬라이드 X 오프셋 시각화 (NoteDisplayPanel + 어댑터에 slideIndex 시각화 hook 추가), (b) PanelAnchor child를 trombone prefab에 정식 추가하여 SessionPanel 위치를 mouthpiece 근처로 조정, (c) `_devtest-trombone-1.vmsong` 정리, (d) 파셜 인덱스 좌→우 배치 사용자 피드백 반영.

## Handoff

<완료 시 메인 세션이 채움. 다음 plan(sub-spec 14 등)이 알 공개 API: `TromboneNoteDisplayAdapter` SerializeField 표면(`radius/arcDegrees/panelTiltDegrees/centerAnchor`), `Trombone_LaneConfig.asset` 경로, `InstrumentLaneConfig.CreateSingleLane(IReadOnlyList<byte>)` 헬퍼.>
