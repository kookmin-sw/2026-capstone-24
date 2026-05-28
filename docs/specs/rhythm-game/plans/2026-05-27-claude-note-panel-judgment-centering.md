# Trombone Note Panel — Judgment Line Centering Offset

**Linked Spec:** [`16-note-panel-judgment-centering.md`](../specs/16-note-panel-judgment-centering.md)
**Status:** `Done`

## Goal

`TromboneNoteDisplayAdapter`에 `judgmentLineOffset` Inspector 필드를 도입하고
`ComputePanelWorldPos`가 `yawRight`(= 플레이어 left) 방향으로 그 만큼 패널 중심을
이동시키도록 하여, `Trombone.prefab`의 직렬화 값 `0.75` 적용 시 각 파셜 패널의
판정선(왼쪽 끝, scrollLength의 절반 만큼 yawLeft로 떨어진 지점)이 `PanelAnchor`의
카메라 yaw-forward 라인에 정렬되도록 한다.

## Context

현재 5개 파셜 패널은 `ComputePanelWorldPos`에서 `anchor.position + AngleAxis(pitchDeg, yawRight) * yawForward * panelRadius`로
배치되어, 패널 중심이 anchor의 카메라 yaw-forward 위에 위치한다. 패널의 노트 스폰
방향은 panel local +y이며 회전은 `LookRotation(toAnchor, yawRight)`로
panel-up = yawRight = 플레이어 left를 향한다. 즉 panel local +y는 yawLeft = 플레이어
입장 left 방향이고, 판정선은 노트가 도달하는 panel local -y 끝(yawRight = 플레이어
right) 쪽에 있다. 결과적으로 판정선이 anchor 중앙으로부터 `scrollLength/2 = 0.75m`
**플레이어 right** 방향에 있어 시야 우측으로 치우쳐 보인다.

ARD 04는 이 오프셋을 Inspector 필드로 노출하기로 결정했다(`기본값 0f`로 기존 동작
호환, prefab에서 `0.75f` 박제). 보정 방향은 패널 중심을 **yawRight** 방향으로
`+0.75` 이동시키는 것이며, 이러면 판정선(panel local -y 끝, yawRight 쪽 끝)이
anchor의 yaw-forward 라인까지 0.75m 더 yawRight로 밀려가서 정확히 anchor의
yaw-forward 위에 놓인다. 노트 스폰점(panel local +y 끝)은 거기서 yawLeft 방향으로
1.5m 떨어진 곳에 위치해 플레이어 입장 왼쪽에서 노트가 등장한다.

검증 실패 cascade가 아닌 신규 단일-plan 작업이다. spec 16의 단일 sub-spec 범위를
한 번에 풀어낸다.

## Verified Structural Assumptions

- `Trombone.prefab` 계층: `Trombone/Rig/PanelAnchor`(Transform-only 마커, `centerAnchor` 참조 대상) + `Trombone/TromboneNoteDisplay`(`RhythmGame.Runtime.TromboneNoteDisplayAdapter` 부착) 동시 존재. nested prefab 없음. — `Unity MCP (manage_prefabs.get_hierarchy) (2026-05-27)`
- `Trombone.prefab` 내 `TromboneNoteDisplayAdapter` 직렬화 필드 현황: `panelRadius: 0.8`, `panelScrollLengthMeters: 1.5`, `verticalSpacingMeters: 0.12`, `fanCenterPartialIndex: 2`, `laneHeightMeters: 0.08` (line 622–631). 신규 `judgmentLineOffset` 필드는 line 631 `panelScrollLengthMeters` 직후에 `0.75`로 직렬화. — `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab line 618-631 (2026-05-27)`
- `TromboneNoteDisplayAdapter.GetYawDirections`: `yawRight = Vector3.Cross(Vector3.up, yawForward).normalized`. Unity 좌표 (left-handed Y-up)에서 `Cross(up, forward) = -right_world`이므로 yawRight는 **카메라 forward 기준 왼쪽**이다. 즉 코드 내 `yawRight` 변수명은 수학적 right가 아닌 플레이어 입장 **왼쪽**을 가리킨다. — `Read Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs line 246-261 (2026-05-27)`
- `ComputePanelRotation`은 `LookRotation(toAnchor, yawRight)`로 panel-up = yawRight = 플레이어 left. panel local +y(스폰 끝)이 플레이어 left에, panel local -y(판정선/노트 도달 끝)가 플레이어 right에 위치. `judgmentLineOffset > 0`이면 패널 중심이 yawRight (=플레이어 left) 방향으로 이동 → 판정선이 anchor 정중앙으로 옮겨감. **부호 검증을 위한 manual-hard AC 1건 포함**. — `Read Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs line 217-239 (2026-05-27)`
- `NoteDisplayPanel` 캔버스의 판정선 Y는 본 plan 변경 범위 밖. Tech Spec Boundaries 명시. 캔버스 레이아웃 변경 없음. — `tech-specs/16-note-panel-judgment-centering.md §Boundaries (2026-05-27)`
- asmdef 의존: `Assets/RhythmGame/Scripts/Runtime/RhythmGame.Runtime.asmdef`만 수정 대상 파일 (`TromboneNoteDisplayAdapter.cs`)이 속한 어셈블리. 신규 namespace import 없음(추가 필드 1개 + 곱셈 1줄). `references` 변경 불필요. — `Read Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs line 1-8 (2026-05-27)`
- ARD 04 Consequences 박제값: 코드 기본값 `0f`, prefab 박제값 `panelScrollLengthMeters / 2 = 0.75f`. — `Read docs/specs/rhythm-game/decisions/04-panel-offset-mode.md (2026-05-27)`

## Approach

1. **필드 추가** — `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` line 46(`panelScrollLengthMeters` 직후)에 다음 추가:

   ```csharp
   [Tooltip("판정선을 PanelAnchor yaw-forward 중앙에 정렬하기 위한 패널 중심 yawRight 방향 오프셋(미터). 일반적으로 panelScrollLengthMeters/2.")]
   [SerializeField] float judgmentLineOffset = 0f;
   ```

2. **ComputePanelWorldPos 수정** — line 217-222의 `ComputePanelWorldPos` 메서드 반환문에 오프셋 항을 더한다:

   ```csharp
   internal Vector3 ComputePanelWorldPos(Transform anchor, int partialIndex)
   {
       float pitchDeg = (GetCenterPartialIndex() - partialIndex) * GetAnglePerPartial();
       GetYawDirections(anchor, out Vector3 yawForward, out Vector3 yawRight);
       Vector3 basePos = anchor.position + Quaternion.AngleAxis(pitchDeg, yawRight) * yawForward * panelRadius;
       return basePos + yawRight * judgmentLineOffset;
   }
   ```

   - `judgmentLineOffset = 0` 시 기존 식과 비트-동일 (Invariant 충족).
   - `LateUpdate`는 동일 메서드를 매 프레임 재호출하므로 별도 수정 없이 매 프레임 일관 적용.
   - `ComputePanelRotation`은 panelWorldPos를 받아 `toAnchor` 방향만 계산하므로 패널이 anchor를 계속 바라본다(약간 yaw 회전이 가해지지만 의도된 동작 — 판정선이 anchor 중앙과 일직선 정렬).

3. **Trombone.prefab 직렬화 값 박제** — `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` line 631(`panelScrollLengthMeters: 1.5`) 직후에 `  judgmentLineOffset: 0.75` 라인 추가. (TromboneNoteDisplayAdapter 컴포넌트 블록 안, fileID anchor 식별자: `m_Script: {fileID: 11500000, guid: c778b13f3d4cef04cafd3c7e4b632042}` 인 컴포넌트, line 618 기준).

4. **회귀 확인** — `unity-test-runner` sub-agent 1회 호출로 EditMode 회귀 (특히 `Tests/Editor/` 하위) 회귀 통과 확인. `TromboneNoteDisplayAdapter` 직접 EditMode 테스트는 없지만 컴파일 + 의존 테스트 통과 확인 목적.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` — `judgmentLineOffset` SerializeField 1개 추가 + `ComputePanelWorldPos` 반환식에 `+ yawRight * judgmentLineOffset` 1줄 추가.
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — `TromboneNoteDisplayAdapter` 컴포넌트의 `judgmentLineOffset: 0.75` 직렬화 라인 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` `TromboneNoteDisplayAdapter.cs`에 `[SerializeField] float judgmentLineOffset = 0f;` (또는 `0`) 선언이 정확히 1회 존재한다.
  **검증:** Grep `\[SerializeField\]\s+float\s+judgmentLineOffset` in `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` → 정확히 1건. 디폴트 값 `= 0f` 또는 `= 0` 포함.
- [ ] `[auto-hard]` `ComputePanelWorldPos` 메서드 본문에 `yawRight * judgmentLineOffset` 항이 존재하고 반환식에 더해진다.
  **검증:** Grep `yawRight \* judgmentLineOffset` in `Assets/RhythmGame/Scripts/Runtime/Display/TromboneNoteDisplayAdapter.cs` → 1건 이상. `ComputePanelWorldPos` 메서드 라인 범위(line 217–225 부근) 안에 위치.
- [ ] `[auto-hard]` `Trombone.prefab`의 `TromboneNoteDisplayAdapter` 직렬화 블록에 `judgmentLineOffset: 0.75` 라인이 존재한다.
  **검증:** Grep `judgmentLineOffset: 0.75` in `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` → 정확히 1건. 라인 컨텍스트로 `m_EditorClassIdentifier: RhythmGame.Runtime::RhythmGame.Runtime.TromboneNoteDisplayAdapter` 블록 안임을 확인.
- [ ] `[auto-hard]` Unity 컴파일 에러 0건. `read_console` 콘솔에 본 변경 관련 컴파일 에러가 없다.
  **검증:** `mcp__UnityMCP__read_console action=get types=["error"]` 호출 → 본 plan 변경 후 새 에러 0건.
- [ ] `[auto-soft]` EditMode 테스트 회귀 통과. `unity-test-runner` sub-agent 1회 호출 결과 기존 테스트 스위트 모두 통과 (특히 `TromboneNoteDisplayAdapterTests`가 있다면 우선).
  **검증:** `unity-test-runner` sub-agent 결과 보고서. 실패 시 노트에 기록하고 다음 plan 결정.
- [ ] `[manual-hard]` 헤드셋(또는 Editor Play Mode)에서 트롬본 세션 시작 시 판정선이 플레이어 시야 정중앙(카메라 yaw-forward 라인)에 위치하고, 노트가 시야 왼쪽에서 등장해 오른쪽 판정선까지 흐른다. 트롬본을 위/아래로 틸트해도 패널 yaw 정렬이 흔들리지 않는다.
  **검증:** TestSceneSanyo 로드 → 트롬본 잡기 → 나비야 차트 세션 시작 → 시야 정중앙에 판정선 + 왼쪽→오른쪽 노트 흐름 확인. 트롬본 mouthpiece 위 30°/아래 30° 틸트 시 패널 위치 유지 확인.
- [ ] `[manual-hard]` `judgmentLineOffset`을 0으로 되돌리면 spec 16 이전 동작(판정선이 시야 우측 치우침)이 그대로 재현된다(역호환·Invariant 검증).
  **검증:** Editor에서 `TromboneNoteDisplay` 컴포넌트 `Judgment Line Offset` 필드를 일시적으로 0으로 변경 후 Play → 5개 패널이 spec 15 직후 상태(오른쪽 치우침)와 동일. 그 후 0.75로 복원.

## Out of Scope

- `NoteDisplayPanel` 캔버스 내부 판정선 Y 위치 변경 (spec 10에서 처리됨).
- `panelScrollLengthMeters` 값 변경 — Tech Spec Boundaries에서 "건드리지 않는다" 박제. 0.75는 어디까지나 현재 1.5m의 절반으로 산출.
- 드럼/피아노 어댑터 patch — Tech Spec Boundaries에서 "건드리지 않는다" 박제.
- `RhythmJudge` 판정 타이밍 윈도우 변경.
- ARD 04 외 결정 재논의.

## Notes

- `yawRight` 변수명은 코드상 플레이어 left를 가리키는 수학적 함정(`Cross(up, forward) = -world_right` in left-handed Unity). 본 plan은 변수명 그대로 유지하고 offset 부호 `+` 만으로 보정. 향후 리네이밍 plan은 별도 sub-spec으로 분리 고려.
- `panelScrollLengthMeters` 변경 시 본 오프셋 `0.75`도 재계산해야 함 — ARD 04 Consequences에 박제됨. 후속 plan이 `panelScrollLengthMeters`를 만지면 함께 갱신 필요.
- prefab 변경은 단순 YAML 1라인 추가지만 Unity 직렬화 순서가 SerializeField 선언 순서와 다를 수 있으니, Unity가 prefab을 다시 저장하면서 필드 순서를 재정렬할 가능성 있음 — 그 경우 무관하게 `judgmentLineOffset: 0.75` 값만 유지되면 정상.

## Handoff

_빈 채로 둠. `/spec-build`가 plan 완료 단계에서 업데이트._
