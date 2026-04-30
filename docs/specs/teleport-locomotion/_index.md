# Teleport Locomotion

## Why

VirtualMusicStudio는 사용자가 가상 공간에서 악기를 연주하는 것이 핵심 경험이다. 자유 이동 방식은 두 가지 비용을 만든다.

1. **악기 위치 정확도가 떨어진다.** 피아노·드럼처럼 사용자의 몸과 악기 사이의 정확한 위치·각도가 연주감을 좌우하는 고정형 악기에서, 사용자가 매번 자유롭게 이동·회전해 자세를 잡으려면 시간이 들고, 같은 악기 앞에 서더라도 매번 다른 위치에서 연주하게 된다.
2. **VR 입문자에게 조작이 무겁다.** 이동 방식이 여러 개 공존하면 어떤 입력이 무엇을 하는지 학습 비용이 생기고, 연주에 집중해야 할 사용자가 이동 자체에 인지 자원을 쓰게 된다.

이 피처는 이동 수단을 **텔레포트 하나로 단일화**하고, 고정형 악기에 대해서는 미리 정해진 자리·각도로 정확히 도착하게 만들어, 사용자가 이동 자체보다 연주에 집중할 수 있도록 한다.

## What

- 사용자는 **왼손에서 발사되는 텔레포트 라인**을 통해서만 가상 공간 안을 이동한다.
- 기존에 존재하던 다른 이동 방식(자유 이동 등)은 제거한다. **단, 회전 방식인 Snap Turn은 본 피처의 범위 밖이며 그대로 보존한다.**
- 사용자가 텔레포트할 수 있는 영역은 명시적으로 정의된다. **노 텔레포트 존**으로 지정된 영역은 라인이 그곳을 가리켜도 이동이 발생하지 않으며, 사용자에게 시각적으로 invalid임이 즉시 드러난다.
- 고정형 악기(피아노·드럼 등)는 **악기당 단일 텔레포트 anchor**(위치 + 향하는 각도)를 가진다. 사용자가 텔레포트 라인을 악기의 일정 반경 안으로 가져가면, 라인의 표현이 시각적으로 구별되는 형태로 바뀌고, 그 반경 안 어디를 가리켜도 사용자는 anchor의 위치·각도로 정확히 텔레포트된다.

## Sub-Specs

| 이름 | 상태 | 링크 |
|---|---|---|
| Base Teleport | Done | [specs/01-base-teleport.md](../_archive/teleport-locomotion/specs/01-base-teleport.md) |
| No Teleport Zones | Done | [specs/02-no-teleport-zones.md](specs/02-no-teleport-zones.md) |
| Instrument Anchors | Draft | [specs/03-instrument-anchors.md](specs/03-instrument-anchors.md) |

> 상태 값: `Draft` / `Active` / `Done` / `Abandoned`

## Open Questions

_현재 열린 질문 없음._

## Status

`Draft`
