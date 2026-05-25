# SessionPanel 도메인 가이드

VR 패널 UI 한 곳에서 **활성 악기 변경·곡 선택·난이도·반주 토글·볼륨**까지 사용자 제어를 모으는 도메인이다. **Instruments → RhythmGame 방향 단방향 의존** — SessionPanel은 두 도메인의 인터페이스를 소비할 뿐, 본 도메인을 외부에서 끌어가지 않는다.

## 1. 폴더 규칙

| 폴더 | 들어가는 것 |
|---|---|
| `Prefabs/` | 최상위 재사용 컴포넌트 — `SessionPanel.prefab`, `InstrumentToggleButton.prefab` |
| `UI/` | 패널 내부 작은 부품 — `VolumeSlider.prefab`, `DifficultyButton.prefab`, `SongRow.prefab`, `AccompanimentToggle.prefab`, `StartMenuSection.prefab` |
| `Scripts/` | 런타임 컨트롤러·UI·계약 |
| `Editor/` | 빌드 전처리(`SongIndexBuilder.cs`) |
| `Audio/` | `SessionMixer.mixer` |
| `Sprites/` | 패널·버튼 sprite |
| `Tests/` | EditMode 회귀 + Instruments 의존 차단용 Dummy |

> 규칙이 코드에 명시되어 있지는 않다 — 컨벤션이다. 새 prefab을 추가할 때 "다른 패널에 통째로 끼워 쓸 단위"면 `Prefabs/`, "이 패널 내부에서만 의미"면 `UI/`.

## 2. 상태 머신 — `SessionPanelController`

`Scripts/SessionPanelController.cs` 의 `PanelState`:

```
        ┌─────────────┐
        │   Hidden    │
        └──┬─────▲────┘
   pinch │     │ pinch / 악기 변경
           ▼     │
        ┌─────────────┐
        │ PinchOpened │  ← 손바닥 위 작은 패널
        └──┬─────▲────┘
   탭 선택  │     │ 악기 변경
           ▼     │
        ┌─────────────┐
        │InstrumentOpened│  ← 활성 악기 PanelAnchor 따라가는 큰 패널
        └─────────────┘
```

전환 트리거:
- **Hidden ↔ PinchOpened** — pinch `InputActionReference` (`SessionPanelController.cs:127` `OnPanelToggle`)
- **Any → Hidden** — `IActiveInstrumentProvider.ActiveInstrumentChanged` (활성 악기 바뀌면 패널을 한 번 닫는다)
- **InstrumentOpened** — 활성 악기의 `PanelAnchor` 위치 추적

`snapOnce` 플래그(`SessionPanelController.cs` 인스펙터): InstrumentOpened 진입 시 1회만 정렬 후 추적을 멈춘다. **트롬본처럼 player head를 따라가는 악기에서 SessionPanel이 함께 끌려다니는 것을 방지**.

## 3. 계약 경계 (단방향)

**SessionPanel → Instruments**:
- `IActiveInstrumentProvider` 주입: 인스펙터 필드 `_activeInstrumentProviderObject: UnityEngine.Object` → Awake에서 `as IActiveInstrumentProvider` 캐스팅(explicit wiring). `SessionPanelController`, `RhythmGameSectionController`, `VolumeSectionController` 모두 동일 패턴.
- `IActiveInstrument.PanelAnchor` — 패널 위치 기준점
- `IActiveInstrument.InstanceVolume` — 볼륨 슬라이더 ↔ 악기 볼륨 양방향 동기

**SessionPanel → RhythmGame**:
- `RhythmGameSectionController.cs:472` 가 `_currentInstrument.InstrumentRoot.GetComponentInChildren<RhythmGameHost>()` 로 host를 찾아 `host.StartSession(...)` 호출(`:500`). `GetComponentInChildren`로 느슨한 결합 — host를 SessionPanel이 직접 보유하지 않는다.

→ Instruments·RhythmGame은 SessionPanel을 모른다. 본 도메인을 통째로 다른 UI로 교체해도 두 도메인은 무영향.

## 4. 카탈로그 분기 (`ISongCatalog`)

| 구현 | 사용처 | 동작 |
|---|---|---|
| `FolderScanSongCatalog.cs:46` | 프로덕션 | `StreamingAssets/Songs/*.vmsong` 스캔. 파일명 패턴 `songname-instrument-difficulty.vmsong`. Android(`UnityWebRequest`) vs 데스크탑(`File.ReadAllText`) 분기. **`index.json` 폴백** (Android는 디렉터리 스캔 불가) |
| `StubSongCatalog.cs` | 에디터/테스트 | `RhythmSongDatabase[]` 배열 직접 참조. `GetChartText(relPath)` 는 `null` 반환 (차트 텍스트 로드 안 함) |

**빌드 전처리** `Editor/SongIndexBuilder.cs` — `IPreprocessBuildWithReport` 구현. 빌드 직전에 `StreamingAssets/Songs/index.json` 자동 생성 → Android 런타임의 폴더 스캔 불가 문제 해결.

→ 새 곡 추가 = `StreamingAssets/Songs/` 에 `.vmsong` 추가하면 됨. 빌드 시 index.json 자동 갱신.

## 5. 볼륨 영속화

- `Scripts/SessionVolume.cs:19` — `Bind(AudioMixer)` 호출 시점에 `InstanceVolumeStore.SetActive(new SessionVolumeStore())` 로 Instruments 측 store 교체. 그 이후 모든 `IActiveInstrument.InstanceVolume` 변경이 PlayerPrefs에 자동 저장.
- `SessionVolumeBootstrap.cs` — 인스펙터 AudioMixer 참조 → `SessionVolume.Bind()` 호출만 담당. 씬에 1개.
- PlayerPrefs 키:
  - 마스터: `SessionPanel.Volume.Master`
  - 악기별: `SessionPanel.Volume.{instrumentId}`

`VolumeSectionController` 는 마스터 슬라이더 + 악기별 슬라이더를 만들어 위 키와 `IActiveInstrument.InstanceVolume` 을 양방향 바인딩.

## 6. 컨트롤러·UI 책임 요약

| 스크립트 | 책임 |
|---|---|
| `SessionPanelController.cs` | §2 상태 머신, 패널 spawn/위치 추적/탭 |
| `TabPanelController.cs` | 탭 버튼 배열 ↔ 패널 배열 연결. 선택 탭만 활성 |
| `RhythmGameSectionController.cs` | 곡 목록·난이도·악기 토글·반주 토글 동적 생성 + `RhythmGameHost.StartSession` 트리거 |
| `VolumeSectionController.cs` | 마스터 + 악기별 볼륨 슬라이더, `SessionVolume`과 양방향 sync |
| `InstrumentToggleButtonUI.cs` | 토글 색상(활성: 파랑, 비활성: 흐림) + 콜백 |
| `AccompanimentToggleUI.cs` | 차트 채널별 토글, 위와 동일 시각 방식 |
| `DifficultyButtonUI.cs` | 난이도 버튼 선택 색상 |
| `SongRowUI.cs` | 제목·아티스트 표시. 악기 미지원 시 비활성+투명도 감소 |
| `UIScalePunch.cs` | Button/Toggle 클릭 펀치 애니메이션 (1.08배, 0.12s, unscaledDeltaTime). 자동 listener 등록 |

## 7. 테스트 stub (Tests/)

| 파일 | 역할 |
|---|---|
| `Tests/DummyActiveInstrument.cs` | `IActiveInstrument` mock — instrumentId/panelAnchor/instanceVolume 인스펙터 설정 |
| `Tests/DummyActiveInstrumentProvider.cs` | `IActiveInstrumentProvider` mock — Update에서 active 변경 감지·이벤트 발화 |

Dummy 사용으로 SessionPanel 테스트는 Instruments 도메인의 실제 InstrumentBase 구체 클래스에 의존하지 않는다.

## 8. 더 깊이 보고 싶을 때

- 상태 전환 로직: `Scripts/SessionPanelController.cs`
- 곡 로딩 분기: `Scripts/FolderScanSongCatalog.cs`
- 볼륨 store 교체 시점: `Scripts/SessionVolume.cs:19`
- 빌드 시 index.json 생성: `Editor/SongIndexBuilder.cs`
- 회귀 테스트: `Tests/{SessionPanelWiring, RhythmGameSection*, SessionVolume, FolderScanSongCatalog}Tests.cs`
