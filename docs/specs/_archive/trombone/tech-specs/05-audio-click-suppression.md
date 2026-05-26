# 오디오 클릭 노이즈 억제 — Tech Spec

**Sub-Spec:** [`05-audio-click-suppression.md`](../specs/05-audio-click-suppression.md)
**Status:** `Draft`
**Date:** 2026-05-21

## Components

- **TrombonePitchDsp** (기존) — OnAudioFilterRead 기반 DSP 상태 머신. 루프 wrap 감지 페이드 및 RequestFadeOut() 추가 대상.
- **Trombone** (기존) — 악기 메인 컨트롤러. 왼손 Grip release 이벤트에서 TrombonePitchDsp의 새 fade-out 경로를 조율.
- **AudioSource** (Unity 기존) — loop=true 재생 컴포넌트. 샘플 카운터를 통해 루프 wrap 타이밍 제공.

## Data / Control Flow

- 왼손 Grip 유지 중 루프 wrap 발생 → TrombonePitchDsp `OnAudioFilterRead` 내 wrap 감지 → 루프 경계 전후 짧은 fade-out/fade-in 적용 → 볼륨 연속 재생 복귀
- 왼손 Grip release 이벤트 → Trombone.cs → `TrombonePitchDsp.RequestFadeOut()` → FadingOut 상태 진입 → fade 완료 → voice 종료
- 크로스페이드 진행 중(FadingOut/FadingIn) `RequestPitchChange()` 호출 → 현재 `_volumeMultiplier` 유지하며 FadingOut 재진입 → AwaitingPitch → FadingIn → Idle

## Boundaries

- **건드린다**: `TrombonePitchDsp.cs` (wrap 감지 로직·RequestFadeOut 추가·중단 재진입 처리), `Trombone.cs` (Grip release → RequestFadeOut 호출 연결)
- **건드리지 않는다**: `TromboneSlideController.cs`, `TrombonePartialController.cs`, `InstrumentBase`, MIDI 이벤트 표면, 멀티플레이어 코드, 오디오 클립 파일

## Invariants

- `AudioSource.pitch`는 메인 스레드(LateUpdate)에서만 설정한다.
- `OnAudioFilterRead`에서 GC 할당 금지. volatile 필드와 샘플 카운터만 사용한다.
- 모든 fade(루프 경계·Grip 릴리즈·크로스페이드 중단)의 지속 시간은 ≤ 10 ms.
- 앵커 이탈(Choke) 경로는 기존 즉시 절단 동작을 유지한다. `RequestFadeOut`과 경로를 공유하지 않는다.
- 발음 미시작 상태에서는 어떠한 오디오 처리도 일어나지 않는다.

## Assumptions

- `TrombonePitchDsp.cs`에 `_state`(volatile int), `_volumeMultiplier`(float), 5 ms fade 상수, `OnAudioFilterRead()`, `RequestPitchChange(float)`, `ResetEnvelope()`이 구현되어 있다 — Read `Assets/Instruments/Trombone/Scripts/TrombonePitchDsp.cs` (2026-05-21, Explore agent)
- `Trombone.cs`에서 왼손 Grip release 이벤트 핸들러가 현재 voice를 종료하는 경로가 존재한다 — Read `Assets/Instruments/Trombone/Scripts/Trombone.cs` (2026-05-21, Explore agent)
- sustain 중 `AudioSource.loop = true` 상태로 재생된다 (루프 wrap 감지 전제).

## Comparable Siblings

| 대상 | 대응 산출물 | 차이 |
|---|---|---|
| `docs/specs/_archive/trombone/specs/04-dsp-pitch-crossfade.md` | `specs/05-audio-click-suppression.md` | 04는 슬라이드/Partial 변경 크로스페이드 도입; 05는 루프 경계·Grip 릴리즈·크로스페이드 중단 세 잔존 경로를 추가로 닫음 |

## Open Tech Decisions

- [ ] 루프 경계 클릭 억제 방식 (OnAudioFilterRead wrap 감지 vs 기타) → decisions/05-loop-boundary-fade.md
- [ ] Grip 릴리즈 fade-out 조율 방식 (RequestFadeOut 신규 추가 vs 기존 NoteOff 경로) → decisions/06-grip-release-fadeout.md
- [ ] 크로스페이드 중 새 RequestPitchChange() 처리 (현재 볼륨 유지 FadeOut vs 즉시 pitch 교체) → decisions/07-interrupted-crossfade.md
