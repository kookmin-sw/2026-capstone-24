# Trombone 슬라이드 링 — HDR 강조 대신 비활성 링 dim 처리

**Linked Spec:** [`15-trombone-slide-position-rings.md`](../specs/15-trombone-slide-position-rings.md)
**Caused By:** [`2026-05-27-claude-trombone-slide-rings.md`](./2026-05-27-claude-trombone-slide-rings.md)
**Status:** `Done`

## Goal

선행 plan `2026-05-27-claude-trombone-slide-rings.md`의 HDR-증폭 강조 방식이 실제 헤드셋 시각 회귀에서 "밝아지는 가시성이 크게 높지 않음" 피드백을 받았다. 본 plan은 강조 방향을 뒤집어 **비활성 6개 링을 어둡게 dim** 처리하고, 현재 SlideIndex 링은 기본 Palette 색 그대로 유지한다. `SlidePositionMarkers`에서 `highlightIntensityMultiplier`를 제거하고 `dimFactor`(0~1, 기본 0.3)로 교체한다.

## Context

선행 plan 인용 (피드백 발췌):

> `highlightIntensityMultiplier=4`로 현재 슬라이드 링을 HDR 밝게 강조했으나, 사용자 직접 테스트 결과 "밝아지는 가시성이 크게 높지 않았음. 기존 오른손을 누르지 않고 있는 색상을 더 어둡게 수정해야 함"이라는 피드백.

원인 분석:
- 선행 plan의 HDR 증폭 방식은 fallback URP/Unlit shader에서 `_BaseColor` 채널 값(>1)에 의존했고 URP Bloom 강도에 종속적이라 헤드셋에서는 변화가 불충분했다.
- 대비(contrast) 기반 시각 강조는 "활성을 더 밝게" 보다 "비활성을 더 어둡게" 가 일반적으로 더 안정적 — 채널이 0 방향으로 줄어드는 변화는 shader/bloom 설정 무관하게 항상 가시.

본 plan은 sub-spec 15 Behavior를 다음과 같이 재해석한다:
- "현재 SlideIndex 링이 강조" → 현재 링은 **기본 Palette 색 그대로** (1.0배), 나머지 6개 링은 `dimFactor`(0~1, 기본 0.3)로 채널이 곱해져 어두워진다.
- "Grip 해제 시 기본 색 복원" → 모든 링이 기본 Palette 색으로 복원 (dim 해제).
- "트롬본 detach 시 링 사라짐" → 기존 attach 게이팅(`SetActive` 토글) 유지.

sub-spec 15 What의 "현재 슬라이드 인덱스의 링이 HDR 밝기로 강조된다" 라는 문구는 *방법*이 아니라 *결과*(시각적 분리)를 요구한다고 해석한다. dim 방식도 동일한 시각적 분리를 제공하므로 sub-spec Behavior 6건은 모두 그대로 만족한다 — 선행 plan의 Behavior 매핑을 답습.

선행 plan의 다른 구조(prefab wiring, `TromboneSlideController.IsGripHeld` public property, `tromboneAnchor` SerializeField, `LateUpdate` attach 게이팅, `_lastIsAttached` 디바운스, `_lastHighlightedIndex` 추적)는 본 plan에서 그대로 유지한다. 단, 강조 적용 방향이 "1개 링에 ApplyHighlight" 에서 "**모든 비활성 링에 ApplyDim**" 으로 뒤집힌다.

ARD `decisions/03-slide-ring-grip-detection.md`("IsGripHeld 폴링" 채택) 결정은 본 plan에서도 유효 — IsGripHeld + SlideIndex 폴링 기반은 변경 없음.

## Verified Structural Assumptions

- **선행 plan 적용 후 `SlidePositionMarkers.cs` 현 상태** — `[Header("Attach Gating & Highlight")]` 아래에 `[SerializeField] TromboneAnchor tromboneAnchor;` (line 33), `[SerializeField, Min(1f)] float highlightIntensityMultiplier = 4f;` (line 35) 직렬화 필드 2개. `_spawnedRenderers` / `_baseColors` / `_lastIsAttached` / `_lastHighlightedIndex` 캐시 필드(line 42-45). `LateUpdate`(line 74-110)에서 attach 게이팅 + IsGripHeld 폴링 + `RestoreHighlight(_last) → ApplyHighlight(desired)` 2단계 전환. `ApplyHighlight`(line 112-121)이 `baseC * highlightIntensityMultiplier`를 1개 링 material에 set. `RestoreHighlight`(line 123-129)가 `_baseColors[idx]`로 복원. `Rebuild` 끝에 Play 모드 시 자식 7개 SetActive(false) 시작(line 178-182). — `Read Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs (2026-05-27)`
- **`Trombone.prefab` 직렬화 박제** — `SlidePositionMarkers` MonoBehaviour 블록(line 1685-1705)에 `tromboneAnchor: {fileID: 6798659558522323895}` (line 1703) + `highlightIntensityMultiplier: 4` (line 1704) 박혀 있음. `slideController: {fileID: 4069711244863020258}` (line 1697) 기존 wiring. `tromboneAnchor`는 prefab 다른 라인(478, 993, 1022, 2759)에서도 동일 fileID로 참조 — TromboneAnchor 컴포넌트 단일 instance. 본 plan은 `highlightIntensityMultiplier: 4` 라인을 `dimFactor: 0.3` 라인으로 치환한다 (Unity SerializeField rename 시 직렬화 키도 같이 변경됨 — 기존 값 4는 새 필드명에 자동 매핑되지 않으므로 prefab을 명시 Edit). — `Read Assets/Instruments/Trombone/Prefabs/Trombone.prefab (2026-05-27) lines 1685-1705` + `Grep tromboneAnchor:|highlightIntensityMultiplier|SlidePositionMarkers`
- **`TromboneSlideController.IsGripHeld` 박제 (선행 plan 적용 후)** — `public bool IsGripHeld => m_IsGripHeld;` 이미 존재. `m_IsGripHeld` write 시점: attach 해제 시 false 강제 + grip rising/falling edge 토글. 본 plan은 이 API 변경 0 — read-only 폴링만 한다. — `Read Assets/Instruments/Trombone/Scripts/TromboneSlideController.cs (2026-05-27)`
- **`TromboneSlideColors.Palette` 박제** — `static readonly Color[7]`. 인덱스 0 보라 `(0.55, 0, 0.85, 1)` → 6 빨강 `(1, 0, 0, 1)`. **모든 채널이 0~1 정상 범위** — dim 곱(`× 0.3`)을 적용해도 채널이 음수가 되지 않고 단순히 어두워짐 (0 방향). alpha는 보존 대상. — `Read Assets/Instruments/Trombone/Scripts/TromboneSlideColors.cs (2026-05-27)`
- **호출 외부 API side effect** — 본 plan은 `SlidePositionMarkers.cs` 한 파일만 수정. 외부에서 본 컴포넌트의 public API(없음 — 컴포넌트는 sealed + 인스펙터 직렬화만)에 의존하는 곳 없음. `TromboneSlideController.IsGripHeld` 사용 방식 변경 없음 (read-only 폴링). `TromboneAnchor.IsAttached` 사용 방식 변경 없음. → sibling 컴포넌트에 부작용 0. — `Grep "SlidePositionMarkers\." -r Assets/Instruments` 결과 self-reference 외 0건 (사전 검증 시).
- **asmdef 의존** — `SlidePositionMarkers.cs`는 `Instruments` 어셈블리. 본 plan은 namespace import 변경 0 → asmdef reference 추가 불필요. — `Read Assets/Instruments/Instruments.asmdef (2026-05-27)`

## Approach

1. **`SlidePositionMarkers.cs`의 SerializeField 교체** — `Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` line 34-35:

   삭제:
   ```csharp
   [Tooltip("HDR 강조 시 기본 색에 곱할 강도. URP Bloom과 결합해 발광 효과를 낸다.")]
   [SerializeField, Min(1f)] float highlightIntensityMultiplier = 4f;
   ```

   추가:
   ```csharp
   [Tooltip("Grip이 눌린 동안 비활성 6개 링의 채널에 곱하는 dim 계수. 0=완전 검정, 1=원본 그대로. 기본 0.3으로 현재 링과 명확한 대비.")]
   [SerializeField, Range(0f, 1f)] float dimFactor = 0.3f;
   ```

   `highlightIntensityMultiplier`는 **완전 제거** (unused로 남기지 않음 — 메인 요청).

2. **`ApplyHighlight` / `RestoreHighlight` 의미 반전** — 단일 인덱스가 아니라 *전체 7개 링*에 dim/restore를 적용하도록 변환:

   - **`ApplyHighlight(int activeIdx)` → `ApplyDimExceptActive(int activeIdx)`** 로 rename + 본문 교체:
     ```csharp
     void ApplyDimExceptActive(int activeIdx)
     {
         // activeIdx가 -1(grip 해제 또는 detach)이면 모두 기본 색.
         for (int i = 0; i < _spawnedRenderers.Count; i++)
         {
             MeshRenderer mr = _spawnedRenderers[i];
             if (mr == null) continue;
             Color baseC = _baseColors[i];
             Color target;
             if (activeIdx < 0 || i == activeIdx)
             {
                 target = baseC; // 현재 링 또는 grip 해제 상태 — 원본 색
             }
             else
             {
                 target = baseC * dimFactor;
                 target.a = baseC.a; // 알파 보존
             }
             SetMaterialColor(mr.sharedMaterial, target);
         }
     }
     ```

   - **`RestoreHighlight(int idx)` 제거** — 호출처가 모두 `ApplyDimExceptActive(-1)`로 단일화 가능 (전체 기본 복원 = activeIdx=-1).

3. **`LateUpdate` 호출 갱신** — `RestoreHighlight(_last) → ApplyHighlight(desired)` 2단계를 `ApplyDimExceptActive(desired)` 단일 호출로 단순화. `_lastHighlightedIndex` 캐시는 그대로 유지(중복 호출 회피용):

   ```csharp
   if (desiredIdx != _lastHighlightedIndex)
   {
       ApplyDimExceptActive(desiredIdx);
       _lastHighlightedIndex = desiredIdx;
   }
   ```

   detach 분기에서도 `RestoreHighlight(_lastHighlightedIndex)` 호출을 `ApplyDimExceptActive(-1)`로 교체.

4. **`OnDisable` 갱신** — `RestoreHighlight(_lastHighlightedIndex)`를 `ApplyDimExceptActive(-1)`로 교체 (모든 마커를 기본 색으로 복원 후 ClearSpawned).

5. **`Trombone.prefab` 직렬화 갱신** — `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` line 1704:

   삭제: `  highlightIntensityMultiplier: 4`
   추가: `  dimFactor: 0.3`

   YAML 들여쓰기·콜론·공백 정확히 보존 (앞 두 칸 스페이스, 콜론 뒤 한 칸). Unity는 SerializeField 신규 필드명에 대응하는 키가 prefab YAML에 있으면 그 값을 채택, 없으면 코드 기본값 사용 — prefab에 명시 키를 두면 inspector 튜닝 가능.

6. **컴파일 + 회귀 검증** — `unity-mcp-workflow` skill에 따라 컴파일 대기 후 `read_console types=["error"]` 0건 확인. `unity-test-runner` sub-agent 1회 호출. MCP 미가용 시 `MCP UNAVAILABLE` 박제.

7. **수동 재현 시나리오 박제** (Editor Play `TestSceneSanyo`):
   - (a) anchor 진입 전: 링 7개 비활성 (선행 plan 게이팅 유지).
   - (b) attach 직후 + grip OFF: 7개 링 모두 Palette 기본 색 (dim 없음).
   - (c) grip ON, SlideIndex=0: SlideMarker_0는 기본 색, SlideMarker_1~6은 채널 30%로 어두워짐 → 현재 위치가 시각적으로 두드러짐.
   - (d) slide 이동으로 SlideIndex=3: SlideMarker_3가 기본 색, 나머지(0,1,2,4,5,6) dim. 이전 SlideMarker_0는 자동으로 dim으로 전환.
   - (e) grip OFF: 7개 모두 기본 색 복원.
   - (f) detach: 7개 모두 SetActive(false).

## Deliverables

- `Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` — `highlightIntensityMultiplier` 필드 제거, `dimFactor` SerializeField 추가, `ApplyHighlight`/`RestoreHighlight`를 `ApplyDimExceptActive(int)` 단일 메서드로 통합, `LateUpdate` 호출 단순화.
- `Assets/Instruments/Trombone/Prefabs/Trombone.prefab` — `SlidePositionMarkers` MonoBehaviour 블록 line 1704의 `highlightIntensityMultiplier: 4`를 `dimFactor: 0.3`으로 치환.

## Acceptance Criteria

- [ ] `[auto-hard]` `SlidePositionMarkers.cs`에 `highlightIntensityMultiplier` 식별자가 0건 (코드·주석·tooltip 어디에도) 남아 있지 않다.
  **검증:** `Grep -n "highlightIntensityMultiplier" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 0건.
- [ ] `[auto-hard]` `SlidePositionMarkers.cs`에 `dimFactor` SerializeField가 존재하며 `[Range(0f, 1f)]` 어트리뷰트와 기본값 `0.3f`가 박혀 있다.
  **검증:** `Grep -n "SerializeField.*Range\(0f.*1f\).*dimFactor.*= 0\.3f" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 1건. (또는 multiline grep으로 `Range\(0f, 1f\)` 라인과 `dimFactor = 0.3f` 라인이 인접).
- [ ] `[auto-hard]` `SlidePositionMarkers.cs`에 `ApplyDimExceptActive` 메서드가 존재하며 `dimFactor`를 곱셈으로 사용한다.
  **검증:** `Grep -n "ApplyDimExceptActive\|\* dimFactor" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 3건 이상 (선언 1 + 호출 ≥1 + dimFactor 곱 1).
- [ ] `[auto-hard]` `SlidePositionMarkers.cs`의 `LateUpdate`가 `ApplyDimExceptActive(desiredIdx)` 형태로 단일 호출 분기를 가진다 (이전의 `RestoreHighlight + ApplyHighlight` 2단계 호출이 남아있지 않음).
  **검증:** `Grep -n "RestoreHighlight\|ApplyHighlight\b" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 0건. 동시에 `Grep -n "ApplyDimExceptActive" Assets/Instruments/Trombone/Scripts/SlidePositionMarkers.cs` 결과 ≥2건 (선언 + 호출).
- [ ] `[auto-hard]` `Trombone.prefab`의 `SlidePositionMarkers` MonoBehaviour 블록에 `dimFactor: 0.3` 라인이 존재하고 `highlightIntensityMultiplier` 키는 0건이다.
  **검증:** `Grep -n -A 20 "m_EditorClassIdentifier: Instruments::Instruments.SlidePositionMarkers" Assets/Instruments/Trombone/Prefabs/Trombone.prefab` 결과 안에 `dimFactor: 0.3` 1줄 + `highlightIntensityMultiplier` 0줄.
- [ ] `[auto-hard]` 프로젝트 전체에서 `highlightIntensityMultiplier` 식별자가 0건이다 (다른 컴포넌트·prefab·테스트에 잔존 없음).
  **검증:** `Grep -r "highlightIntensityMultiplier" Assets/` 결과 0건.
- [ ] `[auto-hard]` EditMode 테스트 스위트 컴파일 errors 0이고, `SlidePositionMarkers` / `TromboneSlideController` 회귀 PASS.
  **검증:** `unity-test-runner` sub-agent 호출 결과 EditMode pass + Unity MCP `read_console types=["error"]` 0건. MCP 미가용 시 `MCP UNAVAILABLE` 박제 후 본 AC를 `pass(skip)` 처리.
- [ ] `[auto-soft]` Unity Editor 콘솔에 `SlidePositionMarkers` / `TromboneSlideController` 관련 NullReferenceException / MissingComponent / ArgumentOutOfRange 에러가 0건.
  **검증:** Unity MCP `read_console types=["error","warning"] filter_text="SlidePositionMarkers|TromboneSlideController"` 결과 0건.
- [ ] `[manual-hard]` Editor Play Mode → 트롬본 anchor 진입 + 오른손 grip OFF 상태에서 7개 링 모두 `Palette[i]` 기본 색으로 동일 밝기 표시 (dim 없음).
  **검증:** Editor Play → 트롬본 attach → grip 누르지 않음 → Scene/Game 뷰에서 7개 링 색이 사양 Palette 순(보라→빨강) 그대로, 어떤 링도 어둡지 않은지 시각 확인.
- [ ] `[manual-hard]` 오른손 Grip을 누르는 동안 현재 `SlideIndex` 위치 링은 기본 Palette 색 그대로, 나머지 6개 링은 `dimFactor=0.3`으로 채널 30%만큼 어둡게 표시되어 현재 위치가 명확히 두드러진다.
  **검증:** Editor Play → 트롬본 attach → 오른손 grip 누름 → 7개 링 중 현재 SlideIndex 1개만 밝은 기본 색, 나머지 6개는 어두운 색으로 즉시 가시적 대비. 슬라이드 이동으로 SlideIndex 바뀔 때마다 강조 위치가 정확히 새 인덱스로 이동.
- [ ] `[manual-hard]` 오른손 Grip을 떼면 7개 링이 모두 기본 Palette 색으로 즉시 복원 (dim 해제, 어떤 링도 어둡지 않음).
  **검증:** Editor Play → grip 누른 dim 상태에서 grip 해제 → 같은 프레임 안에 7개 링 모두 동일 기본 밝기로 복원되는지 시각 확인.
- [ ] `[manual-hard]` 트롬본 detach 시 7개 링이 모두 비활성(SetActive false)으로 사라지며, 재attach 시 grip OFF로 7개 모두 기본 색으로 다시 등장한다.
  **검증:** Editor Play → grip ON dim 상태에서 다른 anchor로 텔레포트 → 링 7개 사라짐 → 다시 트롬본 anchor로 텔레포트 → 7개 링 기본 색으로 재등장 (dim 잔여 없음).
- [ ] `[manual-hard]` 선행 plan `2026-05-27-claude-trombone-slide-rings.md`의 manual-hard AC 5건(attach 게이팅·색 매핑·링 위치 고정·grip 강조 동작·detach 해제)이 본 plan 적용 후 재검증에서 통과한다. 단, "HDR 밝기 강조" 항목은 "현재 링 기본 / 나머지 dim" 동작으로 재해석해 검증한다.
  **검증:** 위 manual-hard 항목 (a)~(f) 시나리오 통과 + 선행 plan 본문의 manual-hard 5건을 본 plan 시나리오와 매칭해 모두 시각 확인.

## Out of Scope

- `dimFactor` 미세 튜닝 — 기본값 0.3 제출. 시각 회귀에서 너무 어둡거나 약하면 inspector에서 prefab 값만 조정 (코드 변경 없음).
- `markerMaterialTemplate`을 URP Lit/Emission 머티리얼로 교체 — fallback URP/Unlit `_BaseColor` 채널 곱셈 방식 유지. dim은 채널을 0 방향으로 줄이는 변화라 emission 슬롯이 없어도 안정 가시.
- 노트 접근 시 추가 강조(slide guide) — sub-spec 15 Out of Scope 답습.
- 이벤트 기반 `SlideIndexChanged` 구독 — 폴링 채택 유지 (ARD 03).
- 링 크기/두께/위치 튜닝 — 선행 plan에서 결정된 값 유지.
- attach 게이팅 / `IsGripHeld` 공개 / `_lastIsAttached` 디바운스 등 선행 plan 구조 — 본 plan 변경 0.
- 다른 악기(Drum 등)의 시각 강조 — 본 plan은 Trombone 전용.

## Notes

- **방향 반전 결정 박제**: HDR 증폭(채널 >1, Bloom 의존)이 헤드셋에서 가시성 부족. 대안 = dim(채널 <1, shader/Bloom 무관) — 항상 안정 가시. 현재 링과 비활성 링의 채널 차이(`1.0` vs `0.3`)는 사람 눈에 70% contrast로 인지되어 즉시 식별 가능.
- **`ApplyDimExceptActive(-1)` 의미 통일**: `activeIdx < 0`이면 "활성 없음" → 모든 링 기본 색. grip 해제·detach 시작 시점에 호출하면 dim 잔여 0 보장. `RestoreHighlight` 별도 메서드 불필요 — 호출처 단일화.
- **`_lastHighlightedIndex` 캐시 그대로 유지**: 매 프레임 7개 material set을 회피하기 위해 desired 인덱스 변화 시에만 `ApplyDimExceptActive` 호출. attach/detach 변화 시에도 `ApplyDimExceptActive(-1)` 후 `_lastHighlightedIndex = -1`로 캐시 초기화.
- **prefab serialized 키 rename 주의**: Unity는 SerializeField 필드명을 직렬화 키로 직접 사용 → `highlightIntensityMultiplier`를 `dimFactor`로 rename하면 prefab YAML의 기존 키는 매칭 불능 (값 4가 무시되고 코드 기본 0.3 채택). 본 plan은 prefab YAML 라인도 명시 Edit해 의도 값(0.3) 박제 — `[FormerlySerializedAs]` 어트리뷰트는 의미가 반대(증폭 ↔ dim)라 사용 부적절.
- **선행 plan과의 관계**: 본 plan은 선행 plan의 *방법*만 교체 — sub-spec 15 Behavior 6건은 같은 plan(선행)이 이미 처리 완료. 본 plan은 강조 방법 한 가지만 재구현.
- **후속 plan 후보**: (a) `dimFactor` 사용자 피드백 반영 미세 튜닝, (b) dim 전환 부드러운 보간(현재는 즉시 set — 인지에 충분하지만 미감을 위해 0.05초 lerp 가능), (c) `markerMaterialTemplate`을 URP Lit Emission 머티리얼로 교체해 활성 링 추가 발광 (dim + emission 둘 다 적용 시 가장 명확한 대비).

## Handoff

<완료 시 메인 세션이 갱신>
