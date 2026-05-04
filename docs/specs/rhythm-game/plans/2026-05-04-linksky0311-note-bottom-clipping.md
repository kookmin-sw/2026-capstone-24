# Note Bottom Clipping — 판정선 하단 노트 숨김

**Linked Spec:** [`../specs/03-note-bottom-clipping.md`](../specs/03-note-bottom-clipping.md)
**Status:** `Ready`

## Goal

리듬게임 세션 중 노트가 판정선(패널 하단 황색 선)을 지나쳐 아래로 내려가는 순간 보이지 않게 처리한다.

## Context

`NoteDisplayPanel`은 World Space Canvas 위에서 낙하 노트를 표시한다. `NoteVisual`은 스폰 시
주입된 `fallSpeed`(px/s)로 매 프레임 `localPosition.y`를 감소시키고, `lifetime` 이후 자동
파괴된다.

현재 lifetime 공식:
```
noteLifetime = (startY + noteH) / fallSpeed + 0.5f
```

`+0.5f`는 노트의 상단 끝이 패널 하단을 지난 시점에서 0.5초를 더 기다렸다가 파괴하는 버퍼다.
이 버퍼 동안 노트는 패널 하단(`pr.y`) 아래에 위치하지만 Unity Canvas 클리핑이 없으므로
그대로 화면에 보인다. 판정 로직(`RhythmJudge`)은 NoteVisual과 독립적이므로 NoteVisual을
빨리 파괴해도 판정 타이밍에는 영향이 없다.

**패널 좌표계:**
- `panelRect.rect.y` = 패널 하단의 로컬 Y 좌표 (피봇 설정에 따라 음수일 수 있음)
- 2D 모드(RectTransform): `pivot=(0.5, 0)` → `localPosition.y`가 노트 하단 Y
- 3D 모드: `localScale.y = noteH`, center 위치 → 노트 하단 = `localPosition.y - localScale.y * 0.5f`

## Verified Structural Assumptions

_해당 없음 — 순수 로직 변경_

## Approach

1. **`NoteDisplayPanel`에 RectTransform 캐시 필드 추가**
   `RectTransform _panelRt;` 필드를 선언하고 `Awake()`에서 `GetComponent<RectTransform>()`으로 초기화한다.

2. **`Update()`에 하단 클리핑 루프 추가**
   기존 pending 노트 스폰 루프 직후, `activeNotes`를 역순으로 순회한다.
   - `nv == null`이면 이미 파괴된 것 → 리스트에서 제거.
   - 2D/3D 분기로 노트 하단 Y를 계산:
     - 2D(`RectTransform` 있음): `bottomY = nv.transform.localPosition.y`
     - 3D: `bottomY = nv.transform.localPosition.y - nv.transform.localScale.y * 0.5f`
   - `bottomY <= _panelRt.rect.y` 이면 `Destroy(nv.gameObject)` + 리스트에서 제거.

3. **기존 `SpawnNote`에서 `GetComponent<RectTransform>()`을 `_panelRt`로 교체 (선택 최적화)**
   동일 컴포넌트를 매 스폰마다 조회하던 비용을 제거한다. 기능 변경 없음.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — `_panelRt` 캐시 + `Update()` 하단 클리핑 루프 추가, `SpawnNote`의 `GetComponent` 교체

## Acceptance Criteria

- [ ] `[auto-hard]` 변경 후 Unity 컴파일 에러가 없다.
- [ ] `[manual-hard]` 세션 실행 시 노트가 판정선(패널 하단 황색 선)에 닿는 순간 사라지며, 그 아래로 내려가지 않는다.
- [ ] `[manual-hard]` 세션 실행 시 판정선 위에 있는 동안 노트가 정상적으로 표시되고 낙하한다.

## Out of Scope

- 판정(Perfect / Good / Miss) 타이밍 로직 변경
- 노트가 판정선을 지나는 순간 Miss를 자동 발생시키는 로직
- 상단 클리핑 동작 변경 (`noteH = Mathf.Min(noteH, panelHeight)` 기존 유지)
- 노트 투명도 감소 등 부드러운 페이드 아웃 효과

## Notes

## Handoff
