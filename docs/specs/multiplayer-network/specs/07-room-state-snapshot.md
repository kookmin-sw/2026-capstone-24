# 룸 상태 snapshot

**Parent:** [`_index.md`](../_index.md)

## Why

유저는 개인 방에서 악기를 배치하고, 오브젝트를 놓고, 리듬게임 악보를 설정하는 등 자신만의 합주 환경을 구성한다. 멀티 룸을 생성했을 때 이 환경이 그대로 옮겨가지 않으면 "내 방에 친구를 초대해 함께 합주"라는 핵심 경험이 단절된다. 다만 멀티 룸 생성의 진실원은 클라이언트 메모리의 현재 상태가 아니라, 서버가 소유한 최신 committed 개인 룸 상태여야 한다.

## What

개인 룸 상태를 멀티 룸의 초기 상태로 가져가는 데이터 흐름과 진실원을 정의한다.

- 개인 룸 snapshot의 authoritative source는 클라이언트 현재 상태가 아니라 백엔드 DB에 저장된 최신 committed snapshot이다.
- 유저가 개인 룸에서 변경했지만 아직 저장하지 않았다면, 그 변경은 멀티 룸 생성에 반영되지 않는다.
- 각 개인 룸 snapshot에는 `snapshotVersion`을 부여해 어느 시점의 상태가 멀티 룸 생성에 사용되었는지 추적한다.
- 현재 모델은 개인 룸 snapshot을 멀티 룸 시작 시점에 복사해 사용하는 `copy-on-create`다.
- 백엔드는 room create 요청 시 요청 유저의 `playerId`를 기준으로 최신 committed snapshot과 `snapshotVersion`을 조회한다.
- 조회된 snapshot은 room provisioning payload의 일부로 Spring 내부 `RoomServerManager`에 전달되고, room server는 이를 복원한 뒤에만 ready callback을 보낸다.
- 백엔드는 snapshot의 소유권과 schema 유효성을 검증하고, room server는 해당 snapshot을 현재 runtime에서 복원 가능한지 검증한다.
- 향후 멀티 룸에서의 변경을 개인 룸에 반영할 때도, live 양방향 동기화가 아니라 호스트 권한의 명시적 save-back commit 흐름으로 다룬다. 이때 room 생성 시점의 `baseSnapshotVersion`과 저장 시점의 최신 개인 룸 snapshot 사이 충돌을 검사할 수 있어야 한다.

## Behavior

- **Given** 호스트 유저가 개인 룸 상태를 저장한 뒤
  **When** 새 멀티 룸 생성을 요청하면
  **Then** 백엔드는 해당 유저의 최신 committed snapshot과 `snapshotVersion`을 조회해 room provisioning의 진실원으로 사용한다.

- **Given** 호스트 유저가 개인 룸에서 변경했지만 아직 저장하지 않은 상태일 때
  **When** 새 멀티 룸 생성을 요청하면
  **Then** 미저장 변경은 무시되고 마지막 committed snapshot만 사용된다.

- **Given** room server가 전달받은 snapshot을 복원 중일 때
  **When** 복원이 성공하면
  **Then** room server는 복원된 `snapshotVersion`을 포함한 ready callback을 보낸다.

- **Given** 멀티 룸에 입장하는 다른 유저가
  **When** 룸 세션에 합류하면
  **Then** 호스트가 생성 시점에 선택한 동일한 `snapshotVersion`의 초기 룸 상태를 관찰한다.

- **Given** 향후 호스트가 멀티 룸 종료 후 변경된 배치를 개인 룸에 반영하려고 할 때
  **When** 명시적 save-back commit을 요청하면
  **Then** 백엔드는 room 생성 시점의 `baseSnapshotVersion`과 현재 개인 룸 snapshot을 비교해 충돌을 검사한 뒤 새 개인 룸 `snapshotVersion`을 커밋할 수 있다.

## Out of Scope

- snapshot 저장 UI와 개인 룸 편집 UX
- 룸 진행 중 변경을 개인 룸 DB에 실시간 반영하는 양방향 live 동기화
- 멀티 룸 진행 중 추가된 악기/오브젝트의 영속 저장
- 리듬게임 결과·자유 합주 녹음 등 세션 결과물 저장
- 호스트가 떠난 뒤 다른 유저가 룸 상태를 수정할 권한 정책

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| _아직 없음_ | — | — | — |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용. 파일명은 날짜·작성자·slug 기반.

## Open Questions

- [ ] snapshot 직렬화 형식 (JSON / Binary / Protobuf)
- [ ] snapshot 크기 상한과 초과 시 거절 정책
- [ ] snapshot payload를 `RoomServerManager`에 inline으로 전달할지, 참조형 저장소를 둘지
- [ ] snapshot 내용 검증·sanitize 책임을 백엔드와 room server 중 어디까지 나눌지
- [ ] 호스트가 떠난 뒤 save-back 권한과 충돌 해소 정책을 어떻게 정의할지
