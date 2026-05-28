# Trombone Note Panel — Judgment Line Offset 방향 반전

**Linked Spec:** [`16-note-panel-judgment-centering.md`](../specs/16-note-panel-judgment-centering.md)
**Caused By:** [`2026-05-27-claude-note-panel-judgment-centering.md`](./2026-05-27-claude-note-panel-judgment-centering.md)
**Status:** `Done`

## Goal

선행 plan의 `+ yawRight * judgmentLineOffset`가 판정선을 시야 우측으로 더 밀었다.
`yawRight = Cross(up, forward)` 는 경험적으로 플레이어 **right** 방향이므로
`-` 부호로 전환하면 판정선이 왼쪽(중앙)으로 이동한다.
부호를 `- yawRight * judgmentLineOffset`으로 바꾸고 prefab 값은 `0.75` 그대로 유지한다.
Inspector에서 양수 = 중앙 방향이 직관적이다.

## Approach

1. `ComputePanelWorldPos` 반환식 부호 변경 (`TromboneNoteDisplayAdapter.cs`):

   ```csharp
   return basePos - yawRight * judgmentLineOffset;
   ```

2. Tooltip 수정 — "yawRight 방향 오프셋" → "yawLeft(판정선 중앙 방향) 오프셋":

   ```csharp
   [Tooltip("판정선을 PanelAnchor yaw-forward 중앙에 정렬하기 위한 패널 중심 오프셋(미터, 양수=중앙 방향). 일반적으로 panelScrollLengthMeters/2.")]
   ```

3. `Trombone.prefab` — `judgmentLineOffset: 0.75` 그대로 유지 (부호 변경 불필요).

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` — `+` → `-` 1글자 + Tooltip 문자열 갱신.
- `Trombone.prefab` 변경 없음.

## Acceptance Criteria

- [ ] `[auto-hard]` `ComputePanelWorldPos` 반환식에 `- yawRight * judgmentLineOffset` (마이너스 부호)가 존재한다.
  **검증:** `Grep -n "- yawRight \* judgmentLineOffset" Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` 결과 1건. 동시에 `Grep -n "\+ yawRight \* judgmentLineOffset"` 0건.
- [ ] `[manual-hard]` 트롬본 세션 시작 시 판정선이 플레이어 시야 중앙에 위치하고 노트가 왼쪽에서 오른쪽으로 흐른다.
- [ ] `[manual-hard]` Inspector에서 `judgmentLineOffset = 0`으로 변경하면 판정선이 다시 우측으로 치우치는 이전 동작이 재현된다.

## Notes

- 경험 확인: `Cross(up, forward) = +X (player right)`. 변수명 `yawRight`이 이름 그대로 player right 방향임.
- 선행 plan의 구조(필드, tooltip 제외 부분, prefab wiring)는 그대로 유지.

## Handoff

<완료 시 메인 세션이 갱신>
