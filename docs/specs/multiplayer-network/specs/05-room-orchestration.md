# 룸 오케스트레이션

**Parent:** [`_index.md`](../_index.md)

## Why

[`03-room-session.md`](03-room-session.md)는 룸 서버 풀의 lifecycle을 관리하는 "별도 관리 컴포넌트"의 존재만 추상화로 도입했다. 별도 관리 컴포넌트의 구체 책임(claim/release 계약, 상태 머신, 장애 복구, 풀 운영 정책)이 한 곳에 모이지 않으면 백엔드·룸 서버와의 책임 경계가 매번 흔들리고 plan 사이에서 같은 결정을 반복하게 된다.

## What

룸 서버 풀의 lifecycle을 관리하는 별도 관리 컴포넌트의 책임을 정의한다.

- 풀에 등록된 룸 서버 인스턴스의 `IDLE → CLAIMED → RUNNING → IDLE` 상태 머신을 운영한다.
- 백엔드의 claim 요청 수신 시 IDLE 인스턴스 하나를 선택해 CLAIMED로 전환하고 식별자를 반환한다.
- 클레임된 인스턴스의 룸 종료 보고를 받아 IDLE로 복귀시켜 재사용한다.
- 룸 서버 인스턴스의 heartbeat을 추적해 unhealthy 인스턴스를 풀에서 제외하고, 가능한 경우 정리한다.
- 백엔드는 룸 메타데이터(목록·상태) DB 관리만 담당하고, 풀·컨테이너 오케스트레이션 권한은 본 컴포넌트가 단독으로 갖는다.

## Behavior

- **Given** 풀에 IDLE 상태의 룸 서버 인스턴스가 1개 이상 있을 때
  **When** 백엔드가 claim을 요청하면
  **Then** 인스턴스 하나가 CLAIMED 상태로 전환되고 식별자가 응답에 담긴다.

- **Given** 풀에 IDLE 인스턴스가 하나도 없을 때
  **When** 백엔드가 claim을 요청하면
  **Then** claim이 거부되고 거부 사유가 응답에 담긴다.

- **Given** CLAIMED/RUNNING 상태의 룸 서버 인스턴스가
  **When** 마지막 유저가 퇴장해 룸이 비면
  **Then** 해당 인스턴스는 IDLE 상태로 복귀해 재사용 가능 상태가 된다.

- **Given** 풀에 등록된 룸 서버 인스턴스가
  **When** heartbeat이 정해진 시간 안에 도달하지 않으면
  **Then** 인스턴스가 unhealthy로 표시되고 풀에서 제외된다.

## Out of Scope

- 백엔드의 클라이언트 향 서비스 API 본체 (룸 생성/목록/입장 등 — 백엔드 책임)
- 룸 서버 자체의 게임 로직·세션 동기화·판정 (룸 서버 책임)
- 컨테이너 이미지 빌드·산출 ([`06-build-targets.md`](06-build-targets.md) 책임)
- K8s 등 동적 컨테이너 오케스트레이션 환경의 스케일링 (시연 이후 후속 단계, [`03-room-session.md`](03-room-session.md) Out of Scope과 일관)
- 백엔드↔매니저, 매니저↔룸 서버 간 와이어 프로토콜의 구체 형식 (plan-level)
- 개인 룸 snapshot의 직렬화·복원 ([`07-room-state-snapshot.md`](07-room-state-snapshot.md) 책임)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [ ] 백엔드↔매니저 통신 프로토콜 (REST / gRPC / MQ)
- [ ] 매니저↔룸 서버 통신 채널 (REST polling / 푸시 / shared volume 등)
- [ ] 시연 단계 풀 사이즈 고정값 결정
- [ ] heartbeat 주기와 unhealthy 판정 임계
- [ ] 매니저 자체의 장애 시 풀 상태 복구 정책 (in-memory vs persistent)
