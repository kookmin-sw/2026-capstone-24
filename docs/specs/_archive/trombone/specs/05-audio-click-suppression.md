# 오디오 클릭 노이즈 억제

**Parent:** [`_index.md`](../_index.md)

## Why

sub-spec 04(DSP Pitch Crossfade)는 슬라이드·Partial 변경 시 크로스페이드를 도입해 음 전환 클릭을 억제했지만, 세 가지 경로에서 가청 클릭 노이즈가 잔존한다. ① 오디오 클립 루프 경계에서의 파형 불연속(왼손 Grip을 오래 유지할 때), ② 왼손 Grip 해제 시 즉시 절단, ③ 크로스페이드 진행 중 새 피치 요청이 들어올 때의 볼륨 점프가 그것이다. 이 sub-spec은 추가 오디오 자산 없이 세 경로를 모두 닫는다.

## What

- 왼손 Grip을 유지한 채 오디오 클립이 루프 경계를 넘어도 가청 클릭이 발생하지 않는다.
- 왼손 Grip을 뗄 때 소리가 즉시 절단되지 않고 짧은 fade-out 후 무음이 된다.
- 크로스페이드 진행 중 슬라이드 위치가 추가로 변경되어도 볼륨 불연속 없이 피치 전환이 완료된다.

## Behavior

- **Given** 왼손 Grip 유지(발음 중)
  **When** 오디오 클립이 루프 경계를 넘는다
  **Then** 볼륨 불연속 없이 재생이 계속된다. 클릭 노이즈는 가청 한계 이하다.

- **Given** 발음 중
  **When** 왼손 Grip을 뗀다
  **Then** 소리가 짧은 fade-out(≤ 10 ms) 후 무음이 된다. 절단 클릭이 없다.

- **Given** 크로스페이드 진행 중 (FadingOut 또는 FadingIn 상태)
  **When** 새 피치 변경이 요청된다
  **Then** 현재 볼륨 수준에서 이어서 fade-out이 진행되고 볼륨 점프가 발생하지 않는다.

- **Given** 발음 미시작 (왼손 Grip 미입력)
  **When** 슬라이드 이동, 루프 경계 통과, 기타 이벤트가 발생한다
  **Then** 오디오에 아무런 변화가 없다.

- **Given** 앵커 이탈
  **When** 발음 중
  **Then** 기존 즉시 절단(Choke) 흐름이 그대로 동작한다. (변경 없음)

## Out of Scope

- 포르타멘토(pitch 점진 보간)
- 멀티플레이어 pitch 동기화
- 새 오디오 콘텐츠 제작 또는 클립 파일 편집
- PlayMode 이외 주파수 연주 (RhythmGame 연동)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리.

## Open Questions

_현재 열린 질문 없음._
