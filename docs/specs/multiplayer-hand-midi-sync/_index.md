# Multiplayer Hand & MIDI Sync

## Why

VirtualMusicStudio 의 멀티플레이어 룸은 인증·룸 lifecycle·접속자 목록까지 동기화한다. 그러나 룸 안에서 다른 플레이어가 무엇을 하는지는 보이지도 들리지도 않는다. 같은 공간에 있다는 사회적 감각과 합주의 핵심 가치 — "다른 사람이 연주하는 것을 보고 듣기" — 가 빠져 있다. 이 피처가 없으면 룸은 본질적으로 1인 공간과 다르지 않다.

## What

같은 룸에 있는 다른 플레이어들의 손 동작이 자기 화면에 실시간으로 보이고, 그들이 연주하는 악기 소리가 자기 헤드폰에서 실시간으로 들리게 만든다. 손 위에는 표시 닉네임 라벨이 떠 있어 누구의 손인지 식별할 수 있다.

- 같은 룸의 다른 플레이어의 양손이 자기 화면에 실시간 동기화된다.
- 다른 플레이어의 손 위에 표시 닉네임이 떠 있다 (자기 손에는 표시되지 않는다).
- 다른 플레이어가 악기 (Piano / Drum / Trombone) 를 연주하면 그 소리가 자기 헤드폰에서 들린다.
- 룸에 새로 합류한 플레이어도 이미 들어와 있는 플레이어들의 손을 입장 즉시 볼 수 있다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| 원격 손 시각화 | `Active` | [01-remote-hand-visualization.md](specs/01-remote-hand-visualization.md) |
| 원격 MIDI 오디오 | `Active` | [02-remote-midi-audio.md](specs/02-remote-midi-audio.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Out of Scope

- 얼굴·머리·몸통 동기화 (손만 다룬다)
- 음성 채팅 / 텍스트 채팅
- 타이밍 보정·예측·jitter buffer·지연 보상 (네트워크 지연이 그대로 노출되어도 OK)
- 원격 손에 collider·Rigidbody 부착 (시각 메시 전용, 충돌-기반 입력 트리거 방지)
- 한 악기에 여러 플레이어가 동시 점유 (multiplayer-network 의 teleport anchor 모델이 단일 점유 강제)
- 네트워크 재연결 후 손 pose state 복원 보장
- 리플레이 / 녹화
- 8명 초과 대규모 룸 동기화 (현재 룸 정원 8명 가정)

## Open Questions

_현재 열린 질문 없음._

## Status

`Draft`
