# Persistent 데모 룸 정책

**Sub-Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Accepted`
**Date:** 2026-05-26

## Context

시연·발표 자리에서 즉시 join 가능한 룸 1개를 상시 유지해야 한다. 현재 lifecycle 는 single-use + last-player-left = terminate 로 박혀 있어 0명이면 즉시 종료된다. `ghost-room-reconciliation` 의 heartbeat timeout (90s) 도 0명 룸을 자동 청소한다. 데모 룸은 이 두 정책의 명시적 예외가 되어야 한다.

## Options Considered

- **persistent flag + 운영자 수동 생성 + 동시 1개** — 일반 룸 lifecycle 에 `is_persistent` 분기 추가. 생성은 internal endpoint / SQL 직접. 자원 부담 최소.
- **모든 유저가 자유 생성** — 룸 생성 API 에 `persistent` 옵션 노출. 남용 위험. 데모 외 정당한 use case 없음 ⚠️.
- **별도 ECS Service + 외부 watchdog** — backend lifecycle 손대지 않고 외부 cron 이 룸 부활. 0명 → 종료 → 재생성 사이 콜드 스타트 갭 (수십 초~분) ⚠️.

## Decision

**persistent flag + 운영자 수동 생성 + 동시 1개** — backend lifecycle 분기가 깔끔. 권한 채널은 internal endpoint 1개로 제한 (`X-Internal-Token` 재사용), 동시 1개 강제. 데모 룸은 일반 룸 목록에 그대로 노출되어 유저 UX 분기 없음. 사용자 결정 (2026-05-26).

## Spec What Coverage

- 03-room-session `## What` "single-use" — 만족 (예외 형태로 박제, 일반 룸은 정책 그대로).
- 03-room-session `## What` "Spring 이 admission 가능 룸 목록 노출" — 만족 (persistent 룸도 동일 노출 흐름).

## Consequences

- `rooms` 테이블에 `is_persistent BOOLEAN NOT NULL DEFAULT FALSE` 컬럼 추가 (마이그레이션).
- `RoomServerManagerImpl` 의 last-player-left 처리에 `is_persistent=true` skip 분기.
- `RoomReconciliationScheduler` 의 heartbeat-timeout 후보 추출 쿼리에 `is_persistent=false` 제약 추가.
- 운영자 전용 internal endpoint (예: `POST /internal/rooms/persistent`) 신설 — `X-Internal-Token` 인증 재사용. 동시 1개 강제 (이미 살아 있으면 409).
- 일반 룸 생성 API DTO 에 `persistent` 옵션 노출 X.
- 운영 매뉴얼: 데모 룸 생성 → 사용 → (발표 종료 후) 명시적 종료 수동 절차 1줄. `docs/dev/` 또는 README 어딘가에 박제.
- ECS task 자체 crash 시 자동 재시작 없음 — desired_count 기반 service 전환은 본 spec Out of Scope (후속 plan 후보).
- 향후 재평가 trigger: 데모 룸이 1개로 부족해질 때, 자동 재시작 필요할 때, 또는 일반 유저에게 persistent 옵션을 노출해야 할 use case 가 생길 때.
