# Instance Volume Slider Verification

**Linked Spec:** [`03-volume-section.md`](../specs/03-volume-section.md)
**Caused By:** [`2026-05-05-linksky0311-session-panel-volume-section.md`](../../_archive/session-panel/plans/2026-05-05-linksky0311-session-panel-volume-section.md)
**Status:** `Ready`

## Goal

선행 plan에서 "활성 악기 선택 UI 없음"으로 skip-deferred된 인스턴스 볼륨 슬라이더 AC #12/#13을 Editor Play mode에서 재검증한다. 추가 코드 변경 없이 이미 씬에 부착된 `DummyActiveInstrumentProvider`를 Inspector에서 조작해 검증한다.

## Context

> **선행 plan 검증 실패에서 파생됨.** 선행: `2026-05-05-linksky0311-session-panel-volume-section.md`.
> 실패한 Acceptance Criteria:
> - `[manual-hard]` dummy provider Current를 Piano로 set → 인스턴스 슬라이더 1개 추가 노출 (라벨 "Piano"). 슬라이더 변경 시 Piano 출력 음량만 변하고 DrumKit는 무영향. — 활성 악기 선택 UI 없어 검증 불가 (skipped-deferred)
> - `[manual-hard]` dummy provider Current를 DrumKit으로 swap → 인스턴스 슬라이더의 라벨/value가 DrumKit 기준으로 갱신. 슬라이더 변경 시 DrumKit 출력만 변함. — 활성 악기 선택 UI 없어 검증 불가 (skipped-deferred)
>
> 본 plan은 위 항목을 다시 통과 가능하게 만드는 부속 작업을 다룬다.

추가 배경:

- 선행 plan 시점에 사용자가 "활성 악기 선택 기능 없음"으로 skip했으나, 이후 씬 구조 재확인 결과 `DummyActiveInstrumentProvider`가 `SessionPanelManager` GameObject에 이미 부착·와이어됨이 확인됐다.
- `DummyActiveInstrumentProvider.Update()`는 `[SerializeField] InstrumentBase _current` 변경을 감지해 `ActiveInstrumentChanged` 이벤트를 발생시킨다. Editor Play mode 중 Inspector에서 `_current` 필드에 씬의 Piano/DrumKit 인스턴스를 드래그-할당하면 이벤트가 발생한다.
- `VolumeSectionController`는 `InjectProvider()`로 주입된 provider의 이벤트를 구독하며 슬라이더를 갱신한다. `InjectProvider()`는 `SessionPanelController.EnsurePanelInstance()` → 패널 토글 시 호출된다.
- **본 plan에서 추가 코드·씬 변경은 없다.** 모든 인프라가 선행 plan에서 완료됐으며, 이 plan은 순수 검증만 수행한다.

검증 절차 요약:

1. `SampleScene` Editor Play mode 진입
2. 패널 토글(InputAction) → 패널 오픈 → 마스터 슬라이더 1개 확인
3. Inspector: `SessionPanelManager` → `DummyActiveInstrumentProvider._current` 필드에 `Piano` 오브젝트 드래그
4. 인스턴스 슬라이더 출현 + 라벨 "Piano" 확인
5. 슬라이더 조작 → Piano 발화 음량 변화, DrumKit 무영향 확인
6. `_current`를 `DrumKit` 오브젝트로 교체 → 슬라이더 라벨/값 DrumKit 기준 갱신 확인
7. DrumKit 발화 음량 변화, Piano 무영향 확인

## Verified Structural Assumptions

- `SessionPanelManager` (SampleScene 루트): `SessionPanelController` + `DummyActiveInstrumentProvider` + `SessionVolumeBootstrap` 부착. `SessionPanelController._activeInstrumentProviderObject`는 SessionPanelManager 자신을 참조 (instanceID: 96550). `DummyActiveInstrumentProvider._current`는 null (편집 시점 기준, Play mode 중 Inspector 할당 예정). — `unity-scene-reader 보고 (2026-05-07)`
- SampleScene 루트에 `Piano` (instanceID: -52314, InstrumentId="Piano") + `DrumKit` (instanceID: 96576, InstrumentId="DrumKit") 인스턴스 존재. 두 오브젝트 모두 `InstrumentBase` 파생 컴포넌트 부착 + `IActiveInstrument` 구현체. — `unity-scene-reader 보고 (2026-05-07)`
- `DummyActiveInstrumentProvider.cs` (`Assets/SessionPanel/Tests/DummyActiveInstrumentProvider.cs`): `[SerializeField] InstrumentBase _current`를 `Update()`에서 감시, 변경 시 `ActiveInstrumentChanged` 발생. `IActiveInstrumentProvider` 구현체. — `Read Assets/SessionPanel/Tests/DummyActiveInstrumentProvider.cs (2026-05-07)`

## Approach

본 plan은 코드·씬 변경 없이 순수 검증만 수행한다.

1. **자동 사전 검증** — 빌드/컴파일 상태 확인. `read_console`로 컴파일 에러 0건 확인.
2. **Editor Play mode 검증 (AC #12 — Piano)**
   - SampleScene Play 진입.
   - InputAction 패널 토글로 패널 오픈 (마스터 슬라이더 1개만 노출 확인).
   - Inspector: `SessionPanelManager` → `DummyActiveInstrumentProvider` → `_current` 필드에 `Piano` GameObject 드래그 할당.
   - `VolumeSectionController`가 이벤트 수신 → 인스턴스 슬라이더 출현 + 라벨 "Piano" 확인.
   - Piano 슬라이더를 낮게 설정 후 Piano MIDI 발화 → 음량 감소 확인.
   - DrumKit MIDI 발화 → 음량 변화 없음 확인.
3. **Editor Play mode 검증 (AC #13 — DrumKit swap)**
   - AC #12 완료 상태 유지.
   - Inspector: `DummyActiveInstrumentProvider._current` 필드를 `DrumKit` GameObject로 교체.
   - 인스턴스 슬라이더 라벨이 "DrumKit"으로 갱신됨 확인.
   - 슬라이더 값이 DrumKit 마지막 저장 값으로 초기화됨 확인.
   - DrumKit MIDI 발화 → 슬라이더 반영 확인.
   - Piano MIDI 발화 → 음량 무영향 확인.

## Deliverables

_없음 — 코드·씬 변경 없는 순수 검증 plan._

## Acceptance Criteria

- [ ] `[auto-hard]` `Assets/SessionPanel/Tests/DummyActiveInstrumentProvider.cs` 파일이 존재하고 컴파일 에러 0건 (`read_console` 확인).
- [ ] `[manual-hard]` Editor Play mode에서 `DummyActiveInstrumentProvider._current`를 Piano로 설정 시 인스턴스 슬라이더가 출현하고 라벨이 "Piano"이며, 슬라이더 조작 시 Piano 음량만 변하고 DrumKit는 무영향이다.
- [ ] `[manual-hard]` `_current`를 DrumKit으로 swap 시 인스턴스 슬라이더의 라벨/값이 DrumKit 기준으로 갱신되고, DrumKit 음량만 변하며 Piano는 무영향이다.
- [ ] `[manual-hard]` 선행 plan `2026-05-05-linksky0311-session-panel-volume-section.md`의 실패 AC ("dummy provider Current를 Piano로 set → 인스턴스 슬라이더 1개 추가 노출 (라벨 "Piano"). 슬라이더 변경 시 Pian") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-05-05-linksky0311-session-panel-volume-section.md`의 실패 AC ("dummy provider Current를 DrumKit으로 swap → 인스턴스 슬라이더의 라벨/value가 DrumKit 기준으로 갱신. 슬라이더 변경 시 D") 가 이 plan 적용 후 재검증에서 통과한다.

## Out of Scope

- 실제 악기 잡기(grab)로 Current를 자동 설정하는 production 구현 — 별도 잡기 wiring plan.
- VR 헤드셋 착용 중 Inspector 접근 없이 악기를 선택하는 VR-native 방법 — production 잡기 wiring에서.
- Hand tracking UI 레이 — 선행 plan 롤백됨, 추후 개발.
- 멀티플레이 타 사용자 슬롯.

## Notes

- 검증은 Editor Play mode(헤드셋 불필요, Inspector 드래그 가능)에서 수행한다. Inspector에서 `_current` 드래그 후 `Update()`가 다음 프레임에 감지 → 이벤트 발생하므로 즉시 반응이 보임.
- `_current`를 null로 되돌리면 인스턴스 슬라이더가 SetActive(false)됨 (AC #15 추가 확인 가능).

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신 -->
