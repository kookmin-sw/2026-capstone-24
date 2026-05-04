# Note Visual Fidelity 구현 — 높이 비례 표시 + 3D 프리팹 스킨

**Linked Spec:** [`../specs/02-note-visual-fidelity.md`](../specs/02-note-visual-fidelity.md)
**Status:** `In Progress`

## Goal

차트에서 정의된 음표 길이에 비례하는 노트 높이를 구현하고, 흰 건반/반음 2슬롯 3D 프리팹 스킨 시스템을 추가한다. 패널 상단 초과 시 클리핑도 적용한다.

## Context

### 현재 상태

`NoteDisplayPanel.SpawnNote()`에서 노트 높이는 `float noteH = panelHeight * 0.05f;`로 고정되어 있다. 음표 길이(`durationSec`)와 무관하게 모든 노트가 같은 높이로 표시된다.

프리팹 슬롯은 `NoteVisual noteVisualPrefab` 단일 필드만 있어 흰 건반/반음 구분이 불가능하다. `noteVisualPrefab`이 null이면 `Image` 컴포넌트를 코드로 생성(2D 사각형)한다.

현재 `NoteVisual`은 `MonoBehaviour`이며 `transform.localPosition.y`를 매 프레임 `fallSpeed`만큼 감소시키는 낙하 로직을 담고 있다. 2D UI(`RectTransform` + `Image`)와 3D 오브젝트(Mesh Renderer) 모두 동일하게 동작한다.

### 구현 접근

**높이 비례:** `fallSpeed * pn.durationSec` = 낙하 속도 × 음표 길이(초) = 그 음표가 판정선에 걸쳐 있는 시간 동안 낙하하는 픽셀 거리. 4분음표보다 2분음표가 정확히 2배 높이 나옴.

**클리핑:** `Mathf.Min(noteH, panelHeight - startY)`로 패널 상단을 초과하지 않도록 제한.

**수명 보정:** 현재 `lifetime = startY / fallSpeed + 0.5f`. 긴 노트는 바닥이 판정선을 지난 뒤에도 상단이 낙하 중이므로 `lifetime = (startY + noteH) / fallSpeed + 0.5f`로 변경.

**3D/2D 사이징 분기:**
- `RectTransform` 있음(2D UI 노트) → `rt.sizeDelta = new Vector2(noteW, noteH)` (기존 방식)
- `RectTransform` 없음(3D 노트) → `transform.localScale = new Vector3(noteW, noteH, noteW)` (폭·높이·깊이)

**2슬롯 스킨:**
- `whiteKeySkinPrefab` (흰 건반 전용) + `blackKeySkinPrefab` (반음 전용) 필드 추가
- 스폰 시 `IsWhiteKey(midiNote)`로 선택, null이면 기존 `noteVisualPrefab`(범용) → null이면 코드 생성 Image 순으로 폴백
- 스킨 슬롯은 Inspector에서 세션 시작 중에도 변경 가능; 이미 낙하 중인 노트는 스폰 시 선택된 prefab 인스턴스를 유지하므로 자동으로 "기존 유지, 이후 적용" 동작

### 관련 파일

- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — SpawnNote() 수정 대상
- `Assets/RhythmGame/Scripts/Runtime/Display/NoteVisual.cs` — 변경 없음 (낙하 로직 그대로)

## Approach

1. **`NoteDisplayPanel.cs` — Inspector 필드 갱신**
   - 기존 `[Header("Note Prefab (optional – plain RectTransform if null)")]` 및 `noteVisualPrefab` 필드를 유지하되, 아래 두 필드를 추가한다:
     ```
     [Header("Note Skin Prefabs (optional – falls back to noteVisualPrefab then Image)")]
     [SerializeField] NoteVisual whiteKeySkinPrefab;
     [SerializeField] NoteVisual blackKeySkinPrefab;
     ```

2. **`NoteDisplayPanel.cs` — `SpawnNote()` 높이 계산 변경**
   - `float noteH = panelHeight * 0.05f;` 줄을 아래로 교체:
     ```
     float noteH = Mathf.Max(fallSpeed * pn.durationSec, 4f);  // 최소 4px 보장
     noteH = Mathf.Min(noteH, panelHeight - startY);           // 패널 상단 클리핑
     ```

3. **`NoteDisplayPanel.cs` — 수명(lifetime) 보정**
   - `float noteLifetime = startY / fallSpeed + 0.5f;` 줄을 아래로 교체:
     ```
     float noteLifetime = (startY + noteH) / fallSpeed + 0.5f;
     ```

4. **`NoteDisplayPanel.cs` — 프리팹 선택 로직 교체**
   - 기존 `if (noteVisualPrefab != null) { nv = Instantiate(...) }` 블록을 아래로 교체:
     ```
     NoteVisual skinPrefab = IsWhiteKey(pn.midiNote) ? whiteKeySkinPrefab : blackKeySkinPrefab;
     if (skinPrefab == null) skinPrefab = noteVisualPrefab;  // 범용 폴백

     if (skinPrefab != null)
     {
         nv = Instantiate(skinPrefab, transform);
     }
     else
     {
         // 코드 생성 2D Image (기존 폴백)
         var go = new GameObject("Note", typeof(RectTransform), typeof(Image));
         go.transform.SetParent(transform, false);
         go.GetComponent<Image>().color = IsWhiteKey(pn.midiNote)
             ? new Color(0.25f, 0.90f, 0.25f, 1f)
             : new Color(0.10f, 0.55f, 0.10f, 1f);
         nv = go.AddComponent<NoteVisual>();
     }
     ```

5. **`NoteDisplayPanel.cs` — 사이징 분기 (2D vs 3D)**
   - 기존 `var rt = nv.GetComponent<RectTransform>(); rt.sizeDelta = ...;` 부분을 아래로 교체:
     ```
     var rt = nv.GetComponent<RectTransform>();
     if (rt != null)
     {
         // 2D UI 모드 (RectTransform 보유)
         rt.anchorMin  = new Vector2(0.5f, 0f);
         rt.anchorMax  = new Vector2(0.5f, 0f);
         rt.pivot      = new Vector2(0.5f, 0f);
         rt.sizeDelta  = new Vector2(noteW, noteH);
         rt.localPosition = new Vector3(localX, localY, 0f);
     }
     else
     {
         // 3D 오브젝트 모드
         nv.transform.localScale    = new Vector3(noteW, noteH, noteW);
         nv.transform.localPosition = new Vector3(localX, localY + noteH * 0.5f, 0f);
     }
     ```
   - 이후 `nv.Init(fallSpeed, noteLifetime); activeNotes.Add(nv);` 는 그대로 유지.

6. **(선택) 기본 3D 스킨 프리팹 생성 — Unity MCP 사용**
   - `Assets/RhythmGame/Prefabs/NoteVisual3DWhite.prefab`, `NoteVisual3DBlack.prefab` 생성.
   - 각각 Cube 메시 + `NoteVisual` 컴포넌트. 흰 건반용은 흰색 Material, 반음용은 회색 Material.
   - Inspector에서 `NoteDisplayPanel.whiteKeySkinPrefab` / `blackKeySkinPrefab`에 할당.
   - 이 단계는 Unity MCP가 사용 가능할 때만 수행; MCP 없이도 기존 2D Image 폴백으로 동작함.

## Deliverables

- `Assets/RhythmGame/Scripts/Runtime/Display/NoteDisplayPanel.cs` — 높이 비례, 클리핑, 2슬롯 스킨, 3D/2D 사이징 분기
- `Assets/RhythmGame/Prefabs/NoteVisual3DWhite.prefab` *(선택, Unity MCP 필요)* — 흰 건반용 기본 3D 노트 프리팹
- `Assets/RhythmGame/Prefabs/NoteVisual3DBlack.prefab` *(선택, Unity MCP 필요)* — 반음용 기본 3D 노트 프리팹

## Acceptance Criteria

- [ ] `[auto-hard]` 변경 후 Unity 컴파일 에러가 없다.
- [ ] `[manual-hard]` 세션 실행 시 C4 4분음표와 E4 2분음표 노트를 비교하면 E4 노트가 C4 노트의 약 2배 높이로 낙하한다.
- [ ] `[manual-hard]` 매우 긴 노트(예: 온음표)가 있을 때, 노트 높이가 패널 상단을 넘지 않고 클리핑되어 표시된다.
- [ ] `[manual-hard]` `whiteKeySkinPrefab`에 커스텀 프리팹을 할당하면 흰 건반 노트 스폰 시 해당 프리팹이 사용된다.
- [ ] `[manual-hard]` 세션 중 `whiteKeySkinPrefab`을 교체해도 이미 낙하 중인 노트는 기존 스킨을 유지하고, 이후 스폰되는 노트부터 새 스킨이 적용된다.

## Out of Scope

- 스킨 선택 UI (메뉴/설정 화면).
- 홀드 노트 판정 로직.
- 애니메이션·파티클 효과.
- 노트 가로 너비의 스킨 제어 — 너비는 레인 구성(흰 건반 80%, 반음 55%)에 따른다.
- `DrumNoteDisplayAdapter`의 노트 높이 비례 — 드럼 전용 경로는 이 plan 범위 밖이다.

## Notes

- 2026-05-03: 검증 실패에서 파생된 후속 plan `2026-05-03-linksky0311-note-visual-fidelity-clipping-fix.md` 추가. 완료 후 본 plan의 `[manual-hard]` "세션 실행 시 C4 4분음표와 E4 2분음표 노트를 비교하면 E4 노트가 C4 노트의 약" 항목 재검증 필요.

## Handoff
