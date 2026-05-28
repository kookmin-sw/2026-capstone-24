# Trombone Session Panel

**Parent:** [`_index.md`](../_index.md)

## Why

트롬본 anchor에 텔레포트해도 SessionPanel이 나타나지 않아, 트롬본으로는 리듬게임
세션을 시작하거나 볼륨을 조절할 수 없다. 트롬본에 SessionPanel을 연결해 다른 악기와
동일한 리듬게임 진입 경험을 제공한다.

## What

트롬본 anchor에 진입한 상태에서 왼손 메뉴 입력을 누르면 SessionPanel이 트롬본의
패널 앵커 위치에 나타난다.

- 트롬본 anchor에 텔레포트하면 SessionPanel을 호출할 수 있는 상태가 된다.
- 왼손 메뉴 입력(핀치 또는 동등 컨트롤러 입력)으로 SessionPanel을 열고 닫을 수 있다.
- SessionPanel 내용(곡 선택, 난이도 선택, 볼륨/티안 조절)은 기존과 동일하다.
- 트롬본 anchor에서 이탈하면 SessionPanel도 닫힌다.

## Behavior

- **Given** 플레이어가 트롬본 anchor에 텔레포트한 상태일 때
  **When** 왼손 메뉴 입력을 발화할 때
  **Then** SessionPanel이 트롬본의 패널 앵커 위치에 열린다

- **Given** SessionPanel이 열린 상태일 때
  **When** 같은 입력을 다시 발화(토글)할 때
  **Then** SessionPanel이 닫힌다

- **Given** SessionPanel이 열린 상태에서
  **When** 플레이어가 트롬본 anchor에서 이탈할 때
  **Then** SessionPanel이 자동으로 닫힌다

## Out of Scope

- SessionPanel 자체의 UI 구성 변경 (session-panel 스펙 담당)
- 왼손 메뉴 입력 방식 변경 (기존 session-panel 스펙 담당)
- 트롬본 노트 디스플레이 (13-trombone-note-display 담당)

## Implementation Plans

| 작성일 | 제목 | 상태 | 링크 |
|---|---|---|---|
| 2026-05-27 | Trombone SessionPanel 진입·토글·이탈 자동 닫힘 검증 plan | Done | [`2026-05-27-claude-trombone-session-panel.md`](../plans/2026-05-27-claude-trombone-session-panel.md) |

> 상태 값: `Ready` / `In Progress` / `Done`
> Plan 추가는 `/spec-build`가 planner sub-agent로 처리. 파일명은 날짜·작성자·slug 기반.

## Open Questions

_현재 열린 질문 없음._
