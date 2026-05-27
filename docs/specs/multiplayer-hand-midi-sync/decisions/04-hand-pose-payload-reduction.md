# 손 pose payload 1차 축소 (본 10 + LateUpdate throttle + MIDI 채널 분리)

**Sub-Spec:** [`01-remote-hand-visualization.md`](../specs/01-remote-hand-visualization.md)
**Supersedes (partial):** [`01-hand-pose-network-encoding.md`](01-hand-pose-network-encoding.md) 의 "본 26개 매 틱 송신" 가정 — Capacity 와 throttle 정책을 본 decision 으로 갱신.
**Status:** `Accepted`
**Date:** 2026-05-27

## Context

archive plan `2026-05-20-remote-hand-wrist-placeholder` + `2026-05-22-remote-hand-finger-pose` + `2026-05-20-remote-midi-broadcast-and-apply` 를 통해 PR #43 (`6f4d99f`) 으로 main 에 합류된 후, 사용자가 실기기 (Quest + 핫스팟 또는 학교 WiFi) 환경에서 **체감 분 단위의 멀티플레이 동기화 지연** 을 보고. 사용자 가설은 "main merge 충돌 해결 중 큰 payload 가 들어왔다" 였으나 2026-05-27 광범위 진단 (`Explore` agent + Tech Spec 정합성 검토 + `b2b3a9a` merge diff 분석) 결과:

- merge conflict 해결의 결함은 발견되지 않음.
- 다만 **설계상 payload 가 작지 않다**:
  - `NetworkedWristPose.FingerRotations` 가 `NetworkArray<Quaternion>` Capacity 25 → **400 bytes 손당 + WristPos/Rot 28 bytes = 손당 428 bytes**.
  - `LocalHandPoseSource.LateUpdate` 가 매 프레임 (≈60 FPS) `RPC_PushPose` 양손 발사 → **클라이언트 업로드 ≈ 51 KB/sec**.
  - `Photon Fusion NetworkProjectConfig.TickRate = 64Hz` 에 따라 server→clients 의 [Networked] sync 도 비슷한 빈도.
  - `MidiNetBus.RPC_*` 어트리뷰트가 `Channel` 옵션 미지정 → 기본 Reliable 채널에서 손 pose 와 backlog 공유 가능.
- 핫스팟·WiFi 같은 packet loss 가 있는 환경에서 Reliable state replication (Fusion 의 [Networked] sync) 의 backlog 가 누적되면 분 단위 지연을 만들 수 있음.

decision 01 (`01-hand-pose-network-encoding.md`) 이 명시적으로 "향후 대역폭 측정 결과로 압축이 필요하면 본 결정을 재평가한다" 라는 trigger 를 박았고, 본 decision 이 그 발동 후속.

또한 사용자 정책 의도 (2026-05-27 결정):
- 음악 이벤트 경로는 손 pose 와 분리되어 큰 Networked state 와 섞이지 않아야 함.
- Reliable RPC 남발 지양.
- 본 사이클은 "1차 quick-win" — timestamp 기반 MIDI / adaptive jitter buffer 같은 2차 정책은 측정 결과 후 결정.

## Options Considered

- **A. TickRate 64 → 30** — `NetworkProjectConfig.fusion` 한 줄. 대역폭/RPC 50% 감소. 시각 jitter 일부 우려.
- **B. ClientSendIndex 조정 (TickRate 유지)** — Fusion SendIndex 의미 검증 후 적용. 시뮬레이션 정확도 유지, send rate 만 감소.
- **C. 본 수 25 → 10 (Proximal + Intermediate × 4 fingers + Thumb Proximal/Distal)** — Capacity 60% 감소, palm 은 wrist 자식이라 자동 따라옴.
- **D. Quaternion smallest-three 4-byte 압축** — 75% per-bone 감소. 커스텀 직렬화 + 디버깅 부담.
- **E. LocalHandPoseSource LateUpdate throttle (2 프레임마다 1회)** — RPC 빈도 절반. 클라이언트 단순 변경.
- **F. MidiNetBus RPC channel 명시 분리** — Reliable 유지하되 손 pose 와 channel index 분리해 backlog 영향 절단. timestamp 인자 추가 / jitter buffer 는 2차 후속.

## Decision

**1차 = C + E + F**.

구체:
- `NetworkedWristPose.FingerRotations` Capacity `25 → 10`.
- `RemoteHandBoneNames` 본 매핑 (Right / Left 동일 구조) 10개 슬롯으로 재구성:
  | index | Right | Left |
  |---|---|---|
  | 0 | R_IndexProximal | L_IndexProximal |
  | 1 | R_IndexIntermediate | L_IndexIntermediate |
  | 2 | R_MiddleProximal | L_MiddleProximal |
  | 3 | R_MiddleIntermediate | L_MiddleIntermediate |
  | 4 | R_RingProximal | L_RingProximal |
  | 5 | R_RingIntermediate | L_RingIntermediate |
  | 6 | R_LittleProximal | L_LittleProximal |
  | 7 | R_LittleIntermediate | L_LittleIntermediate |
  | 8 | R_ThumbProximal | L_ThumbProximal |
  | 9 | R_ThumbDistal | L_ThumbDistal |
- `LocalHandPoseSource.LateUpdate` 에 frame counter 가드 — 2 프레임에 1회만 `RPC_PushPose` 발사 (60 FPS → effective 30 Hz).
- `MidiNetBus.RPC_SendMidiToServer` + `RPC_RelayMidiToClients` 의 `[Rpc]` 어트리뷰트에 명시적 channel 분리 어트리뷰트 적용 (Fusion 의 `Channel` 옵션 사용 가능 시 손 pose 와 다른 channel index 지정, 미지원이면 RPC 자체는 그대로 두고 본 결정의 검증 evidence 에 "Fusion 2 가 채널 index 분리 미지원 — MIDI 와 손 pose 는 별도 NetworkBehaviour 분리로만 격리" 박제).

명시적으로 채택 **안 한** 옵션:
- A (TickRate 64 → 30): C+E 로 충분한 효과 기대. TickRate 자체는 Fusion 의 시뮬레이션 정확도와 직결되므로 손대지 않음. 1차 효과 부족 시 2차 plan 후보.
- B (SendIndex): A 와 동일 — TickRate 계열 조정은 본 사이클 범위 밖.
- D (smallest-three 압축): 직렬화 코드 + 디버깅 부담. 본 수 축소 + throttle 만으로 효과 충분할 가능성. 1차 측정 후 재평가.
- timestamp 기반 MIDI / adaptive jitter buffer: spec `01-remote-hand-visualization.md` 와 `02-remote-midi-audio.md` 의 Out of Scope (타이밍 보정·jitter buffer) 박제 재평가가 필요. 본 decision 범위 밖, 별도 decision + spec 갱신 사이클로 분리.

## Spec What Coverage

| Spec What 항목 | 만족 여부 |
|---|---|
| 같은 룸의 다른 플레이어의 양손이 자기 화면에 실시간 동기화. | 만족 — wrist + 핵심 finger 10본 sync. |
| 손가락 본 전부 동기화. | **부분 만족** — Metacarpal/Distal/Tip/Palm 은 sync 대상에서 제외. wrist 와 Proximal/Intermediate 만으로 grip / key press 시각이 충분히 보임 (악기 연주 시 가장 visible 한 마디는 Proximal). 잘림 표현이 인지 가능 수준이면 본 정책 재평가. |
| Late join 시 입장 즉시 손 보임. | 만족 — Fusion [Networked] join snapshot 자동 전달 (Capacity 만 줄어듦, 메커니즘 동일). |

`02-remote-midi-audio.md` cross-cutting:
| 항목 | 만족 |
|---|---|
| 다른 플레이어의 악기 소리 실시간 sync. | 만족 — MidiNetBus 의 RPC relay 흐름 그대로. channel 분리는 backlog 격리 보강이지 sync 의 신뢰성 변경 X. |

## Consequences

- planner 가 작성할 plan 의 Approach 가 본 decision 의 C/E/F 3건을 코드/자산 변경으로 풀어쓴다.
- Tech Spec `01-remote-hand-visualization.md` § Data/Control Flow 의 "손가락 본 25개" 박제는 **본 decision 이 단일 진실원** 으로 superseded. 추후 spec/tech-spec 동기화 갱신은 plan 의 Deliverables 에 포함.
- 1차 plan 의 acceptance 측정 evidence 두 축:
  - **자동**: NetworkedWristPose Capacity grep + RemoteHandBoneNames 라인 카운트 + LocalHandPoseSource throttle 가드 grep + Unity EditMode 컴파일 0 에러.
  - **수동(dev)**: dev 환경에서 spring/dedicated-server 컨테이너 traffic capture (`docker stats` 또는 `iftop`) 로 손당 평균 KB/sec 측정 — 기대 50%↓.
- 1차 측정 결과:
  - 분 단위 지연 해소 시 → 2차 사이클 (timestamp / jitter buffer) **보류 가능**, decision 04 가 종착점.
  - 미해소 시 → 2차 decision (예: 05-content-sync-timing-policy.md) 박제 + spec Out of Scope 재평가 후속 plan.
- 본 결정으로 visual quality 의 미세 degradation (손가락 끝 마디 표현력 감소) 발생 가능. 사용자 피드백이 "잘림 표현 인지" 수준이면 본 decision 의 본 매핑을 재평가 (Metacarpal 추가 / Distal 추가 등).

## 단일 진실원 박제

본 decision 이후 손 pose payload 정책의 진실원 우선순위:
1. **decision 04 (본 문서)** — Capacity / throttle / channel 분리 정책.
2. decision 01 — encoding 방식 (Fusion [Networked] quaternion 직접) 은 유효, 재평가 trigger 의 1차 결과 박제.
3. Tech Spec 01 — Data/Control Flow 의 Capacity 25 기술은 본 decision 이 supersede.
