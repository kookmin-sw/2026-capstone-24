# 루프 경계 클릭 억제 방식

**Sub-Spec:** [`05-audio-click-suppression.md`](../specs/05-audio-click-suppression.md)
**From Tech Spec:** `../tech-specs/05-audio-click-suppression.md` §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-21

## Context

왼손 Grip을 오래 유지하면 AudioSource 루프 경계에서 클립 끝 샘플과 첫 샘플 간 파형 불연속으로 클릭이 발생한다. 새 오디오 자산 제작 없이 기존 클립을 그대로 사용하면서 DSP 레벨에서 이 불연속을 억제해야 한다.

## Options Considered

- **OnAudioFilterRead wrap 감지 페이드** — 오디오 스레드 내 샘플 카운터로 루프 경계를 감지해 경계 전후 수 ms fade-out/fade-in 적용. 순수 DSP, 새 자산 불필요.
- **AudioClip 루프 포인트 seamless 편집** — 클립 파일을 경계가 seamless하도록 사전 편집. Out-of-Scope(새 오디오 콘텐츠 제작)에 해당.
- **voice 오버랩 재시작** — loop=false 설정, 클립 끝 전에 새 voice를 미리 시작해 오버랩. 두 voice 동시 관리 복잡도 급증.

## Decision

**OnAudioFilterRead wrap 감지 페이드** — 새 자산 없이 구현 가능하고 오디오 스레드 내 샘플 카운터로 정밀한 경계 감지가 가능하다. 오버랩 방식 대비 voice 단일 유지.

## Spec What Coverage

- "왼손 Grip을 유지한 채 오디오 클립이 루프 경계를 넘어도 가청 클릭이 발생하지 않는다" — 만족

## Consequences

- `OnAudioFilterRead`에서 clip.samples 기준 wrap 판정 로직 추가 필요.
- fade 길이는 기존 5 ms 크로스페이드와 통일 권장.
- `AudioSource.loop = true` 전제가 깨지면 본 결정 무효 — Invariant 박제.
