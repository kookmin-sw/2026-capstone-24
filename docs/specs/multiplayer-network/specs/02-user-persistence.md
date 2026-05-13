# 유저 데이터 영속화

**Parent:** [`_index.md`](../_index.md)

## Why

인증만으로는 유저 정보가 서버 프로세스 재시작 시 사라진다. 멀티플레이어 서비스에서는 같은 Meta 계정이 다시 접속해도 동일한 유저로 식별되고, 닉네임과 마지막 접속 시각 같은 기본 프로필이 유지되어야 한다. 유저가 개인 룸에서 구성한 합주 환경(악기 배치·오브젝트 배치 등)도 같은 유저로 재접속할 때 그대로 복원되어야 한다 — 그러지 않으면 매 접속마다 처음부터 다시 배치해야 한다.

## What

서버에서 인증된 유저 정보를 DB에 저장하고 조회하는 기능을 제공한다. 최초 로그인 시 유저 레코드를 생성하고, 재접속 시 기존 레코드를 조회해 같은 유저로 연결한다. DB 내부 조인과 FK는 `users.user_id`(내부 PK)를 사용하고, 외부 시스템과 클라이언트가 참조하는 영구 플레이어 식별자는 서버가 발급하는 ULID 기반 `users.player_id`로 분리한다. `metaAccountId`는 로그인 제공자 식별자, `nickname`은 표시용 값으로만 취급한다.

유저별 개인 룸 상태(악기 배치·오브젝트 배치·오브젝트별 설정·악보 식별자 등)를 함께 영속한다. 유저가 개인 룸을 변경하면 백엔드에 저장되고, 재접속 시 이전 상태가 복원된다. 개인 룸은 `users.player_id`를 소유자 키로 갖는다. 멀티 룸 생성의 진실원은 클라이언트 현재 상태가 아니라 백엔드 DB에 커밋된 최신 개인 룸 snapshot이다. 백엔드는 룸 생성 요청 시 요청 유저의 `playerId`를 식별하고, DB에서 해당 유저의 최신 committed snapshot을 조회해 멀티 룸 초기 상태로 사용한다. 유저가 개인 룸에서 변경했지만 아직 저장하지 않았다면, 멀티 룸에는 그 미저장 변경이 반영되지 않고 이전 committed snapshot이 사용된다. 각 개인 룸 snapshot에는 `snapshotVersion`을 부여해 어느 시점의 개인 룸 상태로 멀티 룸이 생성되었는지 추적한다.

## Behavior

- **Given** 최초 로그인한 유저가  
  **When** 인증에 성공하면  
  **Then** 유저 레코드가 DB에 생성된다.

- **Given** 이미 가입한 유저가  
  **When** 다시 로그인하면  
  **Then** 기존 레코드가 조회되어 동일한 유저로 연결된다.

- **Given** 서버가 재시작되더라도  
  **When** 유저가 다시 접속하면  
  **Then** 이전 유저 정보가 그대로 유지된다.

- **Given** 유저가 개인 룸 상태를 저장한 뒤  
  **When** 새 멀티 룸 생성을 요청하면  
  **Then** 백엔드는 DB의 최신 committed snapshot과 그 `snapshotVersion`을 조회해 룸 초기 상태의 진실원으로 사용한다.

- **Given** 유저가 개인 룸에서 변경했지만 아직 저장하지 않은 상태에서  
  **When** 새 멀티 룸 생성을 요청하면  
  **Then** 미저장 변경은 멀티 룸에 반영되지 않고 마지막 committed snapshot이 사용된다.

- **Given** 유저가 개인 룸에서 악기·오브젝트 배치를 변경한 뒤 저장한 상태에서  
  **When** 다음 접속을 하면  
  **Then** 이전에 저장된 개인 룸 상태가 그대로 복원된다.

## Out of Scope

- 유저 프로필 수정 UI
- 유저 차단/제재 기능
- 게임 플레이 통계 저장
- 멀티 룸 진행 중 변경된 콘텐츠를 개인 룸 DB에 다시 반영하는 양방향 동기화 ([`07-room-state-snapshot.md`](07-room-state-snapshot.md) Out of Scope와 일관)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-04-30 | 백엔드 유저 영속화 전환 | `Done` | [2026-04-30-namae1128-backend-user-persistence.md](../../_archive/multiplayer-network/plans/2026-04-30-namae1128-backend-user-persistence.md) |

> 상태 값은 `Ready` / `In Progress` / `Done`

## Open Questions

- 없음
