# Accompaniment Toggle Gate — RhythmAccompaniment.Fire OFF Skip

**Linked Spec:** [`07-session-options-instrument-toggle-tempo.md`](../specs/07-session-options-instrument-toggle-tempo.md)
**Status:** `Done`

## Goal

`RhythmAccompaniment.Begin`에 `IReadOnlyDictionary<int, bool> enabled` 옵션 인자를 도입하고, `Fire(ScheduledEvent)`가 OFF 채널 이벤트를 skip하도록 게이트를 추가한다. `RhythmGameHost.StartSession`은 이미 보유한 `accompanimentEnabled` 사전을 이 옵션 인자로 그대로 위임한다. UI/chart 머지/effective tempo 적용 일관성은 후속 Plan B/C 책임.

## Context

sub-spec 07 (Session Options — Instrument Toggle & Tempo) 의 What 중 "토글 OFF 악기는 사운드 발화 없음" 단일 책임만 다루는 분할판 첫 plan (Plan A).

이전 시도 plan `2026-05-18-linksky0311-instrument-toggle-tempo-baseline` 은 5파일 수정 + 3 EditMode 테스트 묶음으로 orchestrator 한도 초과·중간 abort. 사용자 결정에 따라 본 책임을 Plan A/B/C 로 분리했다 (Caused By 헤더는 부착하지 않음 — 검증 실패 파생이 아니라 분할 결정).

현 코드 베이스 상태 (sub-spec 06 Done 시점):
- `RhythmGameSectionController.OnPlayButtonClicked` (L356-361) 이 이미 `accompaniment = Dictionary<int, bool>`을 빌드해 judgedChannel 외 모든 채널에 `true`를 박는다. `host.StartSession(_loadedChart, rhythmSong, judgedChannel, accompaniment)` 호출에 4번째 인자로 그대로 전달.
- `RhythmGameHost.StartSession(chart, song, judgedChannel, IReadOnlyDictionary<int,bool> accompanimentEnabled = null)` (L50-51) 이 그 사전을 받아 `lastAccompanimentEnabled` 필드에만 박제 (L53). 곧장 `accompaniment?.Begin(chart, judgedChannel, clock)` (L59) 호출 — Begin 시그니처에 사전이 전달되지 않아 현재는 모든 채널이 무조건 발화.
- `RhythmAccompaniment.Fire(ScheduledEvent)` (L116-120) 은 `_map.TryGetValue(ev.channel, out var inst)` 만 검사 — channel이 매핑돼 있으면 무조건 `TriggerMidi`.

본 Plan A 가 채워야 할 hook은 단 두 곳: (1) Begin 시그니처에 enabled 옵션 추가 + 내부 박제, (2) Fire 진입부 OFF skip. Plan B (UI 토글 컨테이너 빌드), Plan C (chart 머지 + manual 시각 검증) 가 이어진다.

## Verified Structural Assumptions

- `RhythmAccompaniment.Begin(VmSongChart, int, IRhythmClock)` 시그니처는 현 `Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs` L40. `End()` 호출 → `_clock`/`_map`/`_events` 초기화 → `_playing = true` 셋업. 본 plan은 4번째 옵션 인자 `IReadOnlyDictionary<int, bool> enabled = null`를 추가해도 기존 호출부 (`RhythmGameHost.StartSession` L59) 의 시그니처 호환을 깨지 않는다. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs (2026-05-18)`
- `RhythmAccompaniment.Fire(ScheduledEvent ev)` (L116-120) 의 현재 동작: `_map.TryGetValue(ev.channel, out var inst)` 실패 시 early return, 성공 시 `inst.TriggerMidi(new MidiEvent(ev.midiNote, ev.velocity, ev.type, (byte)(ev.channel - 1)))` 무조건 호출. OFF skip 게이트는 이 early return 바로 다음 (또는 그 이전) 한 줄 분기로 들어간다. NoteOn/NoteOff 두 종 이벤트 모두 동일 channel 키로 호출되므로 NoteOff도 같은 게이트에 막힌다 — 발화 안 한 노트의 NoteOff를 발화시키지 않으므로 stuck-note 위험 없음. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs (2026-05-18)`
- `RhythmAccompaniment.Update()` (L60-71) 의 frame-level loop: `_clock.State == Running` 게이트 → `_events[_next].fireTime <= now` 동안 `Fire(_events[_next++])` 호출 → `_next >= _events.Count` 면 `_playing = false`. enabled 사전 게이트는 `Fire` 안에서 처리하므로 `_next++` 와 `_playing` 종료 조건은 변경 불가 — OFF 채널 이벤트도 `_next` 카운터는 진행하되 `TriggerMidi`만 skip 해야 곡 끝 시점에 `_playing = false` 가 정상 발동한다. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs (2026-05-18)`
- `RhythmGameHost.StartSession` (L50-84) 호출 순서: `lastAccompanimentEnabled = accompanimentEnabled` (L53) → `StopSession()` → `clock.Start(chart, effectiveLeadIn)` (L58) → `accompaniment?.Begin(chart, judgedChannel, clock)` (L59) → `judge.Start(...)` → `activeSession.Start()` → NoteDisplay 부착 → `SessionStarted?.Invoke()`. 본 plan은 L59 한 줄을 `accompaniment?.Begin(chart, judgedChannel, clock, accompanimentEnabled)` 로 수정. `lastAccompanimentEnabled` 박제 라인 (L53) 은 그대로 유지 (외부 LastAccompanimentEnabled property 가 노출 중). — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs (2026-05-18)`
- `RhythmGameSectionController.OnPlayButtonClicked` (L356-361) 은 `Dictionary<int, bool>` 인 `accompaniment` 를 빌드해 judgedChannel 외 모든 entry channel에 `true`를 박고 `host.StartSession(_loadedChart, rhythmSong, judgedChannel, accompaniment)` 로 전달. 본 plan은 이 호출부를 변경하지 않는다 — Plan B 에서 UI 토글 상태에 따라 일부 channel을 `false` 로 박는 형태로 확장될 것. — `Read Assets/SessionPanel/Scripts/RhythmGameSectionController.cs (2026-05-18)`
- asmdef 의존: `Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef` references = [`RhythmGame.Data`, `RhythmGame.Runtime.Clock`, `Instruments`, `Unity.InputSystem`]. `IReadOnlyDictionary<int, bool>` 추가는 `System.Collections.Generic` 의존이며 이미 `RhythmAccompaniment.cs` 가 `using System.Collections.Generic;` 사용 중 — 추가 reference 불필요. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef (2026-05-18)`
- EditMode 테스트 asmdef: `Assets/RhythmGame/Tests/Editor/RhythmGame.Tests.Editor.asmdef` references = [`UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `RhythmGame.Data`, `RhythmGame.Runtime.Clock`, `RhythmGame.Runtime`, `Instruments`]. 새 테스트 파일이 `RhythmGame.Runtime` 네임스페이스의 `RhythmAccompaniment` 를 직접 참조 가능. — `Read Assets/RhythmGame/Tests/Editor/RhythmGame.Tests.Editor.asmdef (2026-05-18)`
- EditMode 테스트 hook 가능성 박제: `RhythmAccompaniment.BuildInstrumentMap` 은 `FindObjectsByType<InstrumentBase>` 로 씬을 스캔한다 — EditMode 단위 테스트에는 InstrumentBase 인스턴스가 없으므로 `_map` 이 빈 상태가 된다. 그 결과 `Fire(ev)` 의 `_map.TryGetValue` 단계에서 이미 early return — OFF skip 게이트의 실제 동작을 외부에서 직접 관찰할 수 없다. 따라서 본 plan은 **internal seam** 을 도입한다: `RhythmAccompaniment` 에 `internal bool ShouldFire(int channel)` 메서드를 추가해 enabled 사전 게이트 로직만 노출하고, `Fire(ev)` 와 EditMode 테스트가 같은 메서드를 호출. asmdef 의 `RhythmGame.Runtime` 어셈블리가 `InternalsVisibleTo("RhythmGame.Tests.Editor")` 를 갖도록 `AssemblyInfo.cs` 1개 신규 추가. — `Read Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs (2026-05-18) + Read Assets/RhythmGame/Tests/Editor/RhythmJudgeTests.cs (2026-05-18)`

## Approach

1. `Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs` 수정.
   - 필드 추가: `IReadOnlyDictionary<int, bool> _enabled;` (private).
   - `Begin` 시그니처 확장: `public void Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock, IReadOnlyDictionary<int, bool> enabled = null)`. 옵션 인자 — 기존 3-arg 호출은 그대로 컴파일 통과. 내부에서 `_enabled = enabled;` 박제.
   - `End()` 의 정리 블록에 `_enabled = null;` 추가.
   - 신규 internal 메서드: `internal bool ShouldFire(int channel)` — `_enabled == null` 이면 true (사전 미지정 = 전체 ON, 후방 호환), 아니면 `_enabled.TryGetValue(channel, out var on) ? on : true` (사전에 키가 없으면 ON 으로 fallback — judgedChannel 등은 사전에 없을 수 있고 어차피 `Fire`까지 안 오지만 안전 fallback).
   - `Fire(ScheduledEvent ev)` 본문 수정: `if (!ShouldFire(ev.channel)) return;` 라인을 `_map.TryGetValue(...)` 전에 삽입. `_next++` 카운터는 `Update()` 가 이미 진행하므로 `Update()` 의 loop 동작은 변경 불필요.
2. `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` 수정.
   - L59 의 `accompaniment?.Begin(chart, judgedChannel, clock);` 를 `accompaniment?.Begin(chart, judgedChannel, clock, accompanimentEnabled);` 로 한 줄 변경. `lastAccompanimentEnabled` 박제 라인 (L53) 은 변경 없음.
3. `Assets/RhythmGame/Scripts/Runtime/AssemblyInfo.cs` 신규 추가 (이 폴더의 `RhythmGame.Runtime.asmdef` 어셈블리에 자동 포함).
   - `using System.Runtime.CompilerServices;` + `[assembly: InternalsVisibleTo("RhythmGame.Tests.Editor")]` 1줄.
4. `Assets/RhythmGame/Tests/Editor/RhythmAccompanimentTests.cs` 신규 EditMode 테스트 1건.
   - `[Test] ShouldFire_RespectsEnabledDictionary_OffChannelSkipped` — internal `ShouldFire(int)` 의 동작을 검증.
   - 시나리오: `var go = new GameObject(); var acc = go.AddComponent<RhythmAccompaniment>();` → 최소 chart (channelMap 에 channel 1, 2; judgedChannel=1) + `RhythmClock` + `FakeTimeProvider` 스텁 → `acc.Begin(chart, 1, clock, new Dictionary<int,bool>{ {2, false} });` → `Assert.IsFalse(acc.ShouldFire(2))` + `Assert.IsTrue(acc.ShouldFire(1))` (judgedChannel은 사전에 없으므로 fallback true, 단 실 호출 경로에서는 BuildEvents 가 judgedChannel skip 이라 무관) + `Assert.IsTrue(acc.ShouldFire(99))` (사전에 없는 키 → fallback true).
   - 두 번째 case: `enabled == null` 시 `acc.Begin(chart, 1, clock);` → `Assert.IsTrue(acc.ShouldFire(2))` (후방 호환 — 사전 미지정 = 전체 ON).
   - 테스트 끝 `Object.DestroyImmediate(go)` 정리.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs` — `Begin` 옵션 인자 + `ShouldFire` internal seam + `Fire` OFF skip 게이트.
- `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs` — L59 `Begin` 호출에 4번째 인자 전달.
- `Assets/RhythmGame/Scripts/Runtime/AssemblyInfo.cs` (신규) — `InternalsVisibleTo("RhythmGame.Tests.Editor")` 1줄.
- `Assets/RhythmGame/Tests/Editor/RhythmAccompanimentTests.cs` (신규) — `ShouldFire` 게이트 검증 EditMode 테스트.

## Acceptance Criteria

- [ ] `[auto-hard]` 변경 후 Unity 에디터가 컴파일 에러 0건으로 도메인 리로드 완료.
  **검증:** Unity MCP `read_console` 호출 후 type=error 카운트 0. 또는 `editor_state` resource 의 `isCompiling=false` & errors=[].
- [ ] `[auto-hard]` `RhythmAccompaniment.cs` 가 `Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock, IReadOnlyDictionary<int, bool> enabled = null)` 시그니처 + `internal bool ShouldFire(int channel)` + `Fire` 내부 `if (!ShouldFire(ev.channel)) return;` 라인 3개를 모두 보유.
  **검증:** Grep `Begin\(VmSongChart chart, int judgedChannel, IRhythmClock clock, IReadOnlyDictionary<int, bool> enabled` + Grep `internal bool ShouldFire\(int channel\)` + Grep `if \(!ShouldFire\(ev\.channel\)\) return;` 각 1건 이상 match in `Assets/RhythmGame/Scripts/Runtime/RhythmAccompaniment.cs`.
- [ ] `[auto-hard]` `RhythmGameHost.StartSession` 이 `accompaniment?.Begin(chart, judgedChannel, clock, accompanimentEnabled);` 형태로 4-arg 호출.
  **검증:** Grep `accompaniment\?\.Begin\(chart, judgedChannel, clock, accompanimentEnabled\)` 1건 match in `Assets/RhythmGame/Scripts/Runtime/RhythmGameHost.cs`. 기존 3-arg 호출 `accompaniment\?\.Begin\(chart, judgedChannel, clock\);` 잔존 0건.
- [ ] `[auto-hard]` `AssemblyInfo.cs` 가 `[assembly: InternalsVisibleTo("RhythmGame.Tests.Editor")]` 라인을 보유하며 `RhythmGame.Runtime` 어셈블리에 속함 (폴더 위치 `Assets/RhythmGame/Scripts/Runtime/`).
  **검증:** Grep `InternalsVisibleTo\("RhythmGame\.Tests\.Editor"\)` 1건 match in `Assets/RhythmGame/Scripts/Runtime/AssemblyInfo.cs`.
- [ ] `[auto-hard]` 신규 EditMode 테스트 `RhythmAccompanimentTests.ShouldFire_RespectsEnabledDictionary_OffChannelSkipped` 가 통과. enabled 사전에 `{2: false}` 박힌 상태에서 `ShouldFire(2) == false`, `ShouldFire(99) == true` (사전에 없는 키 fallback), enabled=null 시 `ShouldFire(2) == true` (후방 호환) 3가지 assertion 모두 통과.
  **검증:** `unity-test-runner` sub-agent 호출 또는 Unity MCP `run_tests` (mode=EditMode, filter=`RhythmAccompanimentTests`) — passed=1 / failed=0.
- [ ] `[auto-soft]` `RhythmJudgeTests`, `RhythmClockTests`, `TempoMapResolverTests`, `VmSongParserTests` 등 기존 EditMode 테스트가 모두 통과 (회귀 없음).
  **검증:** `unity-test-runner` sub-agent 호출 전체 EditMode run — pre-existing pass count 유지 (회귀 0).

## Out of Scope

- `InstrumentToggleButtonUI` 신규 컴포넌트 및 prefab 자식 추가 — **Plan B** 책임.
- `RhythmGameSectionController` 의 토글 컨테이너 빌드/난이도 변경 시 재구성 — **Plan B** 책임.
- 플레이어 악기와 다른 악기의 chart 머지 (player chart + accompaniment chart 의 tracks/channelMap 합치기) — **Plan C** 책임.
- 템포 슬라이더 초기값 = `[Tempo]` BPM 박제 및 `effectiveBpm` 을 `RhythmAccompaniment` 의 `chart.tempoMap` 에 일관 적용 — **Plan C** 책임 (현재도 `RhythmGameHost` 가 chart.tempoMap 을 그대로 사용하므로 동일 chart 면 자동 일치하나, chart 머지 후의 일관성은 별도 검증 필요).
- VR 헤드셋 실측 시각/청각 검증 (drum/violin OFF 시 사운드 미발화) — **Plan C** 의 `[manual-hard]` 책임.
- `NoteDisplayPanel` 에 자동 반주 노트 시각화 — sub-spec Boundaries 에 의해 영구 out-of-scope.
- 세션 도중 토글 변경, 트랙별 음량 — sub-spec Boundaries.

## Notes

- 본 plan 은 sub-spec 07 분할판의 **Plan A** (3 of unknown). 후속 Plan B (UI 토글 컨테이너) → Plan C (chart 머지 + effectiveBpm 일관성 + manual 시각 검증) 순서로 진행 예정. 사용자 결정에 따라 분할.
- `ShouldFire` 를 `internal` 로 노출한 이유: `BuildInstrumentMap` 이 씬 스캔 의존이라 EditMode 에서 `_map` 이 비어 `Fire` 의 모든 분기를 외부에서 관찰 불가. enabled 사전 게이트 자체의 단위 검증을 위해 seam 도입. PlayMode 통합 테스트로 격상되면 seam 제거 가능.
- `enabled == null` 시 `ShouldFire` 가 true 반환 — 후방 호환. 기존 3-arg `Begin` 호출 (예: `RhythmGameHost` 가 아닌 다른 경로에서 호출되는 미래 코드) 가 전체 ON 동작을 유지.
- `BuildEvents` 단계에서 OFF channel 이벤트를 처음부터 생성 안 하는 최적화는 본 plan 범위 밖. Plan B/C 적용 후에도 OFF channel 이벤트가 `_events` 리스트에 남으나 `Fire` 게이트로 skip — 메모리 사용량은 약간 더 들지만 단순성 우선.
- 2026-05-18 자동 AC 결과: 6/6 PASS.
  - AC1 (컴파일 0건) PASS — read_console types=error 0건.
  - AC2 (RhythmAccompaniment 3 라인) PASS — Grep 매치 L43/L83/L134.
  - AC3 (RhythmGameHost 4-arg 호출) PASS — Grep 매치 L59, 3-arg 잔존 0건.
  - AC4 (AssemblyInfo InternalsVisibleTo) PASS — Grep 매치.
  - AC5 (RhythmAccompanimentTests PASS) PASS — RhythmGame.Tests.Editor 38/38 통과.
  - AC6 (기존 회귀 없음) PASS — SessionPanel.Tests 15/15 통과.
- manual-hard 없음 (UI 변경 없음). handoff-approval next_action.
- Plan B/C 가 후속 — UI 토글 컨테이너 + chart 머지 + manual 시각 검증.

## Handoff

Plan A 완료. RhythmAccompaniment.Begin 4-arg 시그니처 + internal ShouldFire(int) seam + Fire OFF skip 게이트 도입. RhythmGameHost.StartSession은 이미 보유한 accompanimentEnabled 사전을 Begin에 그대로 전달.

후속 Plan B 시작 시점 박제:
- 4-arg Begin 시그니처 + internal ShouldFire 가 공개됨. Plan B의 UI 토글 상태 → Dictionary<int,bool> 매핑은 기존 RhythmGameSectionController.OnPlayButtonClicked L356-361 의 accompaniment dict 빌드 로직을 토글 상태 기반으로 확장하면 됨.
- enabled==null fallback=true, 사전 키 부재 fallback=true 의 안전 동작 박제.
- AssemblyInfo.cs 의 InternalsVisibleTo 가 후속 plan의 통합 테스트 단위 검증에도 재사용 가능.

후속 Plan C 시작 시점 박제:
- chart 머지 후의 effectiveBpm 일관성은 본 plan에서 다루지 않음. Plan C 가 머지된 chart 의 tempoMap.segments[0].bpm 을 effectiveBpm 으로 in-place 갱신해야 RhythmClock 과 RhythmAccompaniment 가 동일 BPM 으로 발화.
- manual-hard (drumkit OFF 시 무음, piano 노트 판정 정상) 는 Plan C 에서 부착.
