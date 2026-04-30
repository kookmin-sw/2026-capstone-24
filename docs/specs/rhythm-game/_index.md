# Rhythm Game

## Why

VR 악기 연습 환경에서 박자감을 게임적으로 익히게 하는 학습 도구가 필요하다. 자유 연주만으로는 자신의 박자가 정확한지 스스로 판단하기 어렵고, 곡을 따라 치는 즐거움도 제한된다. 리듬게임 모드를 통해 플레이어는 곡을 따라 정해진 타이밍에 노트를 연주하며, 박자 정확도에 대한 즉각적인 피드백을 받고, 동시에 곡 자체를 즐길 수 있다.

## What

플레이어는 한 악기를 잡은 상태에서 메뉴를 통해 곡과 난이도를 선택하고, 그 곡의 리듬게임 세션을 플레이한다. 곡은 외부에서 작성된 **텍스트 차트 파일** 하나로 정의되며, 한 곡에는 여러 악기 트랙이 포함될 수 있다. 플레이어가 잡은 악기와 일치하는 트랙은 입력을 채점받고, 나머지 트랙은 게임이 시간에 맞춰 자동으로 해당 악기에서 소리내어 곡을 완성한다. 별도 오디오 파일은 동반되지 않으며, 모든 사운드는 기존 MIDI 사운드 파이프라인을 통해 발화된다.

- 텍스트 차트 파일이 곡의 단일 진실원이다 (오디오 파일 없음).
- 한 곡에 여러 악기 트랙이 들어가며, 어떤 악기를 잡았는지에 따라 채점 대상 트랙이 바뀐다.
- 플레이어가 잡지 않은 트랙은 게임이 자동 반주로 발화한다.
- 플레이어 트랙의 각 노트는 Perfect / Good / Miss 로 판정된다.
- 모든 악기에 동일한 구조가 적용된다 (악기 종류와 무관).

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| Chart Format | Done | [`chart-format.md`](../_archive/rhythm-game/specs/chart-format.md) |
| Chart Import | Draft | [`specs/chart-import.md`](specs/chart-import.md) |
| Session Flow | Draft | [`specs/session-flow.md`](specs/session-flow.md) |
| Timing Clock | Done | [`timing-clock.md`](../_archive/rhythm-game/specs/timing-clock.md) |
| Judgment | Done | [`judgment.md`](../_archive/rhythm-game/specs/judgment.md) |
| Accompaniment | Draft | [`specs/accompaniment.md`](specs/accompaniment.md) |
| Note Display | Draft | [`specs/note-display.md`](specs/note-display.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Open Questions

- [x] 노트 시각화 UI(떨어지는 노트 등)를 별도 sub-spec으로 추가할 시점. → `note-display` sub-spec으로 추가 완료.
- [ ] 결과 화면(점수 합산/등급)을 후속 단계에서 별도 sub-spec으로 추가할지, 아니면 `judgment` 안에서 확장할지.
- [ ] 차트에 정의된 악기가 씬에 존재하지 않을 때(예: 트럼펫 트랙이 있으나 트럼펫 InstrumentBase 인스턴스가 씬에 없음)의 정책 — 트랙 무시 / 사용자 알림 / 곡 자체 비활성 중 어디로.
- [ ] 곡 ↔ 난이도 연결 자료구조의 위치 — 기존 SongDatabase에 흡수할지, 새 sub-spec(예: `song-catalog`)으로 분리할지.

## Status

`Active`
