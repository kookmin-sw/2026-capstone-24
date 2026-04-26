# `.vmsong` Syntax & Runtime Data Model

**Linked Spec:** [`chart-format.md`](../specs/chart-format.md)
**Status:** `Ready`

## Goal

`.vmsong` 텍스트 차트의 신택스를 확정하고, 파싱 결과를 담을 런타임 C# 자료구조를 정의한다. 파서 자체와 tick→시간 변환 로직은 후속 plan(`002`)이 담당한다.

## Context

`chart-format.md`는 곡 1개를 `.vmsong` 텍스트 파일 1개로 표현하라고 요구하며, 그 파일은 다음을 모두 담아야 한다.

- **곡 메타데이터** (제목, 작곡가 등). **난이도는 차트가 모른다** — 외부 매핑의 책임이다.
- **채널-악기 매핑** — 채널 1–16 → instrument key. **채널 10은 항상 드럼** (일반 MIDI 컨벤션).
- **마스터 시계** — tick 해상도 + (구간별) BPM/박자.
- **채널별 노트 트랙** — 각 노트는 시간 위치, MIDI 노트 번호, 길이, 세기.

Spec의 핵심 행동 요구:

- 같은 차트 → 같은 자료구조 (deterministic).
- 차트에 정의된 정보만으로 모든 노트의 절대 시각(초) 계산 가능.
- 곡 중간 BPM 변화 구간이 정의되면 절대 시각은 변화를 누적 반영해 결정.

### 현재 코드 상태

`Assets/RhythmGame/Scripts/Data/`에 다음 자산들이 존재하지만, 모두 spec 이전의 단순 구조다.

- `RhythmChart.cs` — `ScriptableObject`, 단일 BPM/offset, `List<RhythmNote>`, `RhythmDifficulty` 필드 보유.
- `RhythmNote.cs` — `timeSeconds` / `midiNote` / `duration`. 세기 없음, 채널 없음, tick 없음.
- `RhythmSong.cs` — `AudioClip` 참조 보유.
- `RhythmDifficulty.cs` — `Beginner/Intermediate/Advanced`.
- `RhythmSongDatabase.cs` — `RhythmChart.difficulty`로 곡 필터링.

Spec과의 격차:

| 요구 | 현재 |
|---|---|
| 채널-악기 매핑 | 없음 |
| tick + tickResolution | 없음 (초 직접) |
| BPM 변화 구간 | 없음 (단일 BPM) |
| 세기(velocity) | 없음 |
| 차트 != 난이도 | `RhythmChart.difficulty` 보유 |
| 오디오 파일 없음 | `RhythmSong.audioClip` 보유 |

본 plan은 **신택스 명세 + 새 자료구조 도입**까지만 책임진다. 기존 `RhythmChart`/`RhythmNote`는 본 plan에서 **제거**한다 (spec과 정면 충돌하고, 후속 plan들이 새 자료구조를 전제로 한다). `RhythmSong`/`RhythmSongDatabase`는 chart-import / song-catalog 영역이라 본 plan에서 손대지 않는다 — 단, `RhythmSongDatabase.GetSongsByDifficulty`가 제거 대상 `RhythmChart.difficulty`를 참조하므로 컴파일 유지를 위해 해당 메서드만 임시로 비활성화(주석 또는 빈 결과 반환)한다.

`RhythmSession.cs`/`RhythmGameHost.cs`는 런타임 스텁이며 본 plan에서 직접 다루지 않는다. 컴파일이 깨지지 않을 정도로만 신규 타입 참조를 정리한다.

### 포맷 결정: INI-스타일 텍스트 DSL

JSON, YAML, 자체 DSL 중 **INI-스타일 자체 DSL**을 채택한다.

- 텍스트 편집기에서 사람이 직접 손볼 수 있어야 한다 (spec의 핵심 motive).
- 노트 라인이 압도적으로 많으므로, 한 노트 = 한 줄(공백 구분)이 가장 작다.
- 의존성 없이 단순 라인 파서로 끝낼 수 있다.

## Approach

### 1. `.vmsong` 신택스 명세 (이 plan의 산출물 중 하나)

```
# 줄 단위 주석은 '#' 으로 시작.
# 빈 줄은 무시.
# 섹션은 [SectionName] 한 줄로 시작하며, 다음 [Section] 또는 EOF까지가 본문.
# 키-값 라인: key = value  (공백은 자유)
# 섹션 이름과 키 이름은 대소문자 구분 없음. value는 대소문자 보존.

[Meta]
title    = <string>     # 곡 제목 (필수)
artist   = <string>     # 작곡가/연주자 (선택)
songId   = <string>     # 안정 식별자 (선택, 없으면 파일 stem 사용)

[Resolution]
ticksPerQuarter = <int>  # 4분음표 1박자당 tick 수. 필수. 양의 정수.

[Tempo]
# 한 라인당 한 BPM 구간 시작점. 다음 segment 시작 전까지 유효.
# tick=<int>  bpm=<float>  [beats=<int>]  [beatUnit=<int>]
# beats/beatUnit 생략 시 직전 segment 값을 상속. 첫 segment는 둘 다 명시 권장.
tick=0      bpm=120  beats=4  beatUnit=4
tick=1920   bpm=90

[Channels]
# 채널 번호 (1..16) → 악기 key. 채널 10은 항상 drum (이 라인이 없어도 묵시적 예약).
# channel=<int>  instrument=<string>
channel=1   instrument=piano
channel=10  instrument=drum

[Track:1]
# 노트 한 줄: tick=<int>  note=<int 0..127>  len=<int>  vel=<int 0..127>
# tick: 시작 위치. len: tick 단위 길이 (>=1). vel: 세기.
tick=0     note=60  len=240  vel=100
tick=240   note=64  len=240  vel=100
tick=480   note=67  len=480  vel=110

[Track:10]
tick=0     note=36  len=120  vel=120   # kick
tick=480   note=38  len=120  vel=110   # snare
```

#### 신택스 규칙 정리

- **인코딩**: UTF-8.
- **줄 끝**: `\n` 또는 `\r\n` 모두 허용.
- **주석**: 라인 시작 또는 라인 중간에 `#` 등장 시 그 뒤는 주석.
- **섹션 헤더**: `[<Name>]` 또는 `[<Name>:<Index>]` 형식. 본 포맷에서 인덱스를 쓰는 섹션은 `Track`뿐.
- **키-값 라인**: 같은 라인에 `key=value` 쌍이 1개 이상 등장 가능. 공백 구분.
- **필수 섹션**: `[Meta]`, `[Resolution]`, `[Tempo]`, `[Channels]`, 최소 1개 이상의 `[Track:N]`.
- **결정성**: 같은 입력 → 같은 자료구조. 섹션 출현 순서가 달라도 결과는 동일하나, **동일 섹션 안의 라인 순서는 보존**된다 (특히 노트의 등록 순서가 정렬 전 결과에 영향).
- **정렬**: 파서는 노트를 tick 오름차순으로 정렬한다 (다음 plan).

### 2. 런타임 C# 자료구조

`Assets/RhythmGame/Scripts/Data/` 아래에 신규 plain C# 타입(SO 아님)으로 정의한다. 이 객체들은 파서 결과로 메모리에 로드되어 런타임이 사용한다.

```csharp
// Channels.cs
public static class RhythmChannels {
    public const int DrumChannel = 10;     // 일반 MIDI 컨벤션. 채널-악기 매핑에서 항상 drum 예약.
    public const int MinChannel  = 1;
    public const int MaxChannel  = 16;
}

// ChartNote.cs
[System.Serializable]
public struct ChartNote {
    public int  tick;          // 시작 tick (>=0)
    public byte midiNote;      // 0..127
    public int  durationTicks; // >=1
    public byte velocity;      // 0..127
}

// ChartTrack.cs
[System.Serializable]
public sealed class ChartTrack {
    public int channel;                // 1..16
    public List<ChartNote> notes = new();
}

// TempoSegment.cs
[System.Serializable]
public struct TempoSegment {
    public int   tick;        // 이 BPM이 시작되는 tick
    public float bpm;
    public int   beatsPerBar; // 박자 분자 (예: 4)
    public int   beatUnit;    // 박자 분모 (예: 4)
}

// TempoMap.cs
[System.Serializable]
public sealed class TempoMap {
    public int ticksPerQuarter;             // 양의 정수
    public List<TempoSegment> segments = new();   // tick 오름차순
}

// ChannelInstrumentMap.cs
[System.Serializable]
public sealed class ChannelInstrumentMap {
    [System.Serializable] public struct Entry { public int channel; public string instrumentKey; }
    public List<Entry> entries = new();
}

// VmSongChart.cs (root)
[System.Serializable]
public sealed class VmSongChart {
    public string title;
    public string artist;
    public string songId;
    public TempoMap tempoMap = new();
    public ChannelInstrumentMap channelMap = new();
    public List<ChartTrack> tracks = new();
}
```

자료구조 설계 의도:

- **Plain C# (`[Serializable]`) — `ScriptableObject` 아님.** Spec은 차트가 텍스트 파일이라고 명시했고, SO는 chart-import 단계에서 결정할 사항이다. 본 plan의 자료구조는 "파싱 결과를 담는 메모리 모델"이다.
- **Difficulty 미포함.** Spec의 "차트는 자기가 어느 난이도인지 모른다"를 강제.
- **AudioClip 미포함.** Spec의 "오디오 파일 없음"을 강제.
- `byte`로 MIDI/velocity, `int`로 tick. tick은 곡 길이에 따라 수십만까지 가도 안전한 범위가 필요해 `int`.
- `List<TempoSegment>`는 항상 tick 오름차순으로 유지된다는 invariant. 보장은 다음 plan(파서)이 책임진다.

### 3. 기존 자산 정리

- **삭제**: `Assets/RhythmGame/Scripts/Data/RhythmChart.cs`, `Assets/RhythmGame/Scripts/Data/RhythmNote.cs`, `Assets/RhythmGame/Scripts/Data/RhythmDifficulty.cs`.
  - `.meta` 파일도 함께 삭제.
- **수정**: `Assets/RhythmGame/Scripts/Data/RhythmSongDatabase.cs` — `GetSongsByDifficulty` 메서드를 제거 또는 빈 리스트 반환으로 임시화. (이 메서드는 chart-format 영역이 아니라 song-catalog/난이도 매핑 영역의 미해결 Open Question에 속한다 — `_index.md`의 "곡 ↔ 난이도 연결 자료구조" 항목.)
- **수정**: `Assets/RhythmGame/Scripts/Data/RhythmSong.cs` — `RhythmChart[] charts` 참조가 사라지므로 해당 필드 제거. `AudioClip` 필드는 spec과 충돌하지만 chart-import / song-catalog 영역이라 본 plan에서는 손대지 않는다 — `RhythmSong`은 우선 메타데이터(songId/title/artist)만 남기고, charts 배열만 제거한다.
- **수정**: `Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs`, `RhythmGameHost.cs` — `RhythmChart`/`RhythmNote` 참조가 있다면 컴파일이 깨지지 않도록 임시로 주석 처리하거나 신규 `VmSongChart`로 시그니처만 교체. 동작 구현은 다음 plan들의 몫.

### 4. 컴파일 확인

- Unity Editor에서 `read_console`로 컴파일 에러 0 확인.
- 신규 자료구조가 인스펙터에서 직렬화되는지 가벼운 확인은 권장이지만 강제 아님.

## Deliverables

- `docs/specs/rhythm-game/plans/001-chart-syntax-and-data-model.md` — 본 파일.
- `Assets/RhythmGame/Scripts/Data/RhythmChannels.cs` — 채널 상수.
- `Assets/RhythmGame/Scripts/Data/ChartNote.cs`
- `Assets/RhythmGame/Scripts/Data/ChartTrack.cs`
- `Assets/RhythmGame/Scripts/Data/TempoSegment.cs`
- `Assets/RhythmGame/Scripts/Data/TempoMap.cs`
- `Assets/RhythmGame/Scripts/Data/ChannelInstrumentMap.cs`
- `Assets/RhythmGame/Scripts/Data/VmSongChart.cs`
- (삭제) `Assets/RhythmGame/Scripts/Data/RhythmChart.cs` (+ `.meta`)
- (삭제) `Assets/RhythmGame/Scripts/Data/RhythmNote.cs` (+ `.meta`)
- (삭제) `Assets/RhythmGame/Scripts/Data/RhythmDifficulty.cs` (+ `.meta`)
- (수정) `Assets/RhythmGame/Scripts/Data/RhythmSong.cs` — `charts` 필드 제거.
- (수정) `Assets/RhythmGame/Scripts/Data/RhythmSongDatabase.cs` — `GetSongsByDifficulty` 임시 비활성화.
- (수정) `Assets/RhythmGame/Scripts/Runtime/RhythmSession.cs`, `RhythmGameHost.cs` — 컴파일 유지 한도에서만.

## Acceptance Criteria

- [ ] 본 plan 본문에 `.vmsong` 신택스가 섹션/라인/주석 규칙 + 예제 차트로 구체적으로 명세돼 있다.
- [ ] 위 신택스 명세에 메타데이터, 채널-악기 매핑(채널 10 = 드럼 포함), tick 해상도, BPM 변화 구간, 채널별 노트 트랙(tick/midi/len/vel)이 모두 표현 가능하다.
- [ ] `VmSongChart`, `ChartTrack`, `ChartNote`, `TempoMap`, `TempoSegment`, `ChannelInstrumentMap`, `RhythmChannels` 신규 타입이 컴파일된다 (Unity 콘솔 에러 0).
- [ ] `RhythmChannels.DrumChannel == 10` 상수가 코드에 존재한다.
- [ ] `VmSongChart` 및 그 하위 타입 어디에도 difficulty 필드가 없다.
- [ ] 기존 `RhythmChart`, `RhythmNote`, `RhythmDifficulty` 파일이 `.meta` 포함 삭제됐다.
- [ ] Editor 컴파일이 에러 없이 통과한다 (`read_console`로 확인).

## Out of Scope

- 텍스트 → `VmSongChart` 변환 로직 (Plan `002`).
- tick → 절대시각(초) 변환 함수 (Plan `002`).
- `.vmsong` 파일을 Unity 에셋으로 import하는 메커니즘 (`chart-import` sub-spec).
- 채널-악기 매핑 검증, 악기 인스턴스 부재 시 정책 (root-spec Open Question).
- 곡 ↔ 난이도 연결 자료구조 위치 결정 (root-spec Open Question).
- `RhythmSong.audioClip` / `RhythmSongDatabase.instrumentKey`의 운명 — chart-import / song-catalog 영역.

## Notes

- 본 plan에서 결정한 `.vmsong` 신택스는 후속 plan들 모두의 전제다. 신택스 변경은 사용자 합의 후에만.
- INI-스타일 채택은 의존성 없이 단순 라인 파서로 끝나도록 한 결정. 후일 확장이 필요하면 새 섹션을 추가하는 방향(예: `[Markers]`, `[Lyrics]`)으로 무리 없이 자랄 수 있다.
- BPM segment의 `beats`/`beatUnit`은 본 plan 시점에서는 메타로만 보존된다. 마디/박자 기반 UI나 메트로놈 동작은 후속 plan들에서 사용.
