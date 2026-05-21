# Grip 릴리즈 fade-out 조율 방식

**Sub-Spec:** [`05-audio-click-suppression.md`](../specs/05-audio-click-suppression.md)
**From Tech Spec:** `../tech-specs/05-audio-click-suppression.md` §Open Tech Decisions #2
**Status:** `Accepted`
**Date:** 2026-05-21

## Context

왼손 Grip을 뗄 때 voice가 즉시 절단되어 클릭이 발생한다. sub-spec 04는 이 경로를 "기존 발음 종료 흐름 변경 없음"으로 명시적으로 Out of Scope로 뒀으므로, sub-spec 05에서 별도 fade-out 경로를 추가해야 한다.

## Options Considered

- **TrombonePitchDsp.RequestFadeOut() 신규 추가** — DSP 상태 머신에 FadeOut 전용 상태 추가. Trombone.cs Grip release 핸들러에서 호출, fade 완료 후 voice 종료. DSP 내 볼륨 흐름 일관성 유지.
- **NoteOff/Choke 기존 경로 활용** — 기존 NoteOff 내 fade-out 경로가 있다면 연결. 없으면 동일한 수정 필요. Choke(앵커 이탈 즉시 절단)는 혼용 불가.

## Decision

**TrombonePitchDsp.RequestFadeOut() 신규 추가** — Choke(앵커 이탈 즉시 절단)와 Grip 릴리즈(부드러운 fade-out)를 명확히 분리. DSP 상태 머신 안에서 일관된 볼륨 관리.

## Spec What Coverage

- "왼손 Grip을 뗄 때 소리가 즉시 절단되지 않고 짧은 fade-out 후 무음이 된다" — 만족
- "앵커 이탈 기존 Choke 흐름 유지" — 만족 (Choke는 별도 경로로 유지)

## Consequences

- `TrombonePitchDsp`에 pitch 교체 없이 볼륨만 0으로 내리는 FadeOut 전용 상태 추가 필요.
- fade 완료 후 voice를 안전하게 종료하는 콜백 또는 플래그 필요.
- `Choke`(즉시 절단)와 `RequestFadeOut`(부드러운 종료)은 동일 경로를 공유하지 않는다.
