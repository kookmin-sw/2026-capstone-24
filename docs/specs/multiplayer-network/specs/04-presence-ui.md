# 접속 현황 UI

**Parent:** [`_index.md`](../_index.md)

## Why

같은 룸에 누가 있는지 알 수 없으면 공유 공간의 사회적 감각이 사라진다. 실시간 접속 현황을 UI로 보여줘야 유저가 함께 있다는 인식을 가질 수 있다.

## What

현재 룸에 접속 중인 유저 목록을 실시간으로 표시하고, 룸 생성·입장·퇴장을 수행할 수 있는 UI를 제공한다. 클라이언트 빌드의 default 씬([`../decisions/01-default-scene.md`](../decisions/01-default-scene.md)) 안에 월드스페이스 UI로 통합 제공하며, 룸 입장 전(로비) 상태와 입장 후(멀티 합주) 상태는 같은 씬에서 UI/오브젝트 활성화 토글로 전환된다. 모든 룸은 동일한 default 씬과 그 안에 미리 배치된 오브젝트를 공유하므로, 룸 생성 UI는 룸 이름·비밀번호·정원 같은 메타 옵션만 다루고 콘텐츠 옵션은 노출하지 않는다. 룸 목록은 백엔드가 admission 가능 상태로 공개한 룸만 기준으로 표시하고, 접속자 목록은 실제 룸 세션의 실시간 상태를 기준으로 닉네임만 표시한다.

## Behavior

- **Given** 유저가 룸에 입장해 있을 때
  **When** 다른 유저가 입장하거나 퇴장하면
  **Then** 접속 유저 목록이 즉시 갱신된다.

- **Given** 로그인된 유저가 로비 화면에 있을 때
  **When** 룸 목록을 조회하면
  **Then** Spring이 내부 `RoomServerManager`의 현재 active/admission-open 상태를 반영해 공개한 룸들만 표시된다.

- **Given** 유저가 룸 UI에서 입장·퇴장 버튼을 누르면
  **Then** 세션 상태가 변경되고 UI가 즉시 반영된다.

## Out of Scope

- 유저 아바타 표시 (3D 공간 내 위치)
- 호스트/권한·연결 상태·아이콘 등 닉네임 외 부가 식별 마커
- 클라이언트 측 멀티플레이 전용 씬 분리 (현 시점은 단일 씬 토글 모델만 다룬다)
- 룸별 콘텐츠 옵션·악기/오브젝트 선택 UI (모든 룸이 default 씬과 동일한 기배치 오브젝트를 공유함)
- 채팅 / 텍스트 메시지
- 유저 초대·친구 목록

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-16 | Presence UI 로비 패널 (룸 생성 + 룸 목록) | `Ready` (2026-05-18 lobby-migration-and-ux plan 으로 supersede 예정) | [2026-05-16-namae1128-presence-ui-lobby-panel.md](../plans/2026-05-16-namae1128-presence-ui-lobby-panel.md) |
| 2026-05-16 | Presence UI 인-룸 패널 (참가자 리스트 + 퇴장) | `Ready` (stale — [c495623] 이 SampleScene 에 실현, 2026-05-18 마이그레이션 plan 으로 supersede 예정) | [2026-05-16-namae1128-presence-ui-in-room-panel.md](../plans/2026-05-16-namae1128-presence-ui-in-room-panel.md) |
| 2026-05-18 | Presence UI SampleScene → TestSceneSanyo 마이그레이션 | `Ready` (commit 20cf4c0 — auto-hard 통과, manual-hard 1건은 #6 사이클 대기) | [2026-05-18-namae1128-presence-ui-migration-to-testscenesanyo.md](../plans/2026-05-18-namae1128-presence-ui-migration-to-testscenesanyo.md) |
| 2026-05-18 | Presence UI LobbyPanel 마이그레이션 + UX 리디자인 + VR 키보드 통합 | `Ready` | [2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md](../plans/2026-05-18-namae1128-presence-ui-lobby-migration-and-ux.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/plan-new` 사용.

## Open Questions

- _없음_
