# 멀티플레이어 룸 세션

**Parent:** [`_index.md`](../_index.md)

## Why

여러 유저가 같은 VR 공간을 공유하려면 유저들을 하나의 세션으로 묶는 룸 개념이 필요하다. 룸이 없으면 각자 독립된 공간에서만 존재하게 되어 협주가 불가능하다.

## What

Photon Fusion을 통해 룸을 생성하거나 기존 룸에 입장·퇴장할 수 있는 세션 관리 기능을 제공한다. 여러 유저가 같은 룸에 동시 접속해 공유 공간을 형성한다.

## Behavior

- **Given** 로그인된 유저가
  **When** 새 룸 생성을 요청하면
  **Then** 룸이 생성되고 해당 유저가 첫 입장자로 합류한다.

- **Given** 로그인된 유저가
  **When** 기존 룸에 입장을 요청하면
  **Then** 같은 룸에 이미 있는 유저들과 동일한 세션에 합류한다.

- **Given** 룸에 접속 중인 유저가
  **When** 퇴장하면
  **Then** 해당 유저는 세션에서 제거되고 다른 유저에게 반영된다.

- **Given** 룸의 마지막 유저가
  **When** 퇴장하면
  **Then** 룸이 자동으로 소멸한다.

## Out of Scope

- 아바타·손 동작 실시간 동기화
- 악기 연주 동기화
- 음성 채팅
- 룸 비밀번호·초대 링크

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-28 | Photon Fusion SDK 패키지 준비 | `Done` | [2026-04-28-namae1128-photon-fusion-import.md](../plans/2026-04-28-namae1128-photon-fusion-import.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용.

## Open Questions

- [ ] 룸 최대 인원 제한
- [ ] 룸 목록 공개 여부 (누구나 검색 가능 vs 초대 전용)
- [ ] Photon Fusion 모드 선택 (Shared / Server / Host)
