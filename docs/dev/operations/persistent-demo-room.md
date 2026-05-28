# Persistent 데모 룸 운영 매뉴얼

> **단일 진실원**: [`decisions/05-persistent-demo-room-policy.md`](../../specs/multiplayer-network/decisions/05-persistent-demo-room-policy.md)
> 운영자 전용 채널 — `X-Internal-Token` 필수, 동시 1개 강제

## 1줄 절차 요약

발표 전 persistent 룸 1개를 생성하고, 발표 후 SQL로 플래그를 내린 뒤 `/terminate`를 호출해 종료한다.

---

## 생성

```bash
curl -X POST https://api.mu-rang.com/internal/rooms/persistent \
  -H "X-Internal-Token: $SECRET" \
  -H "Content-Type: application/json" \
  -d '{
    "ownerUserId": 1,
    "photonSessionName": "demo-2026-q2",
    "maxPlayers": 8,
    "roomRuntimeVersion": "v0.1.0"
  }'
```

성공 응답 예시 (HTTP 200):

```json
{
  "success": true,
  "data": {
    "roomId": 42,
    "photonSessionName": "demo-2026-q2",
    "maxPlayers": 8,
    "locked": false,
    "status": "SERVER_STARTING",
    "taskPublicIp": null,
    "gamePort": null,
    "createdAt": "2026-05-27T10:00:00Z",
    "readyAt": null
  }
}
```

이미 살아 있는 persistent 룸이 있는 경우 (HTTP 409):

```json
{"code": "PERSISTENT_ROOM_ALREADY_EXISTS", "detail": "이미 살아 있는 persistent 룸이 있습니다."}
```

---

## 수동 종료 절차

> **주의**: persistent 룸에 `/terminate`를 직접 호출하면 idempotent 204를 반환하고 실제로 종료되지 않는다.
> 반드시 아래 순서를 따른다.

### 1단계 — `is_persistent` 플래그 해제 (EC2 SSH 후 MySQL 직접)

```sql
UPDATE rooms SET is_persistent = FALSE WHERE room_id = 42;
```

### 2단계 — `/terminate` 콜백 발사

```bash
curl -X POST https://api.mu-rang.com/internal/rooms/42/terminate \
  -H "X-Internal-Token: $SECRET" \
  -H "Content-Type: application/json" \
  -d '{"reason": "demo-end"}'
```

응답: HTTP 204. 30초 후 ECS task `lastStatus=STOPPED`, DB `status=TERMINATED`, `closed_at` 셋팅.

---

## 확인 쿼리

```sql
-- 현재 살아 있는 persistent 룸 확인
SELECT room_id, is_persistent, closed_at
FROM rooms
WHERE is_persistent = TRUE AND closed_at IS NULL;

-- persistent 룸의 instance 상태 확인
SELECT rsi.status, rsi.last_heartbeat_at
FROM room_server_instances rsi
JOIN rooms r ON r.room_id = rsi.room_id
WHERE r.is_persistent = TRUE AND r.closed_at IS NULL;
```
