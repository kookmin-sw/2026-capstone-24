# 독립 가이드 패널 & [가이드] 버튼 연결 (sub-spec 10 plan 2/2)

**Linked Spec:** [`10-instrument-travel-list.md`](../specs/10-instrument-travel-list.md)
**Status:** `Done`

## Goal

세션 패널 외부의 독립 패널 `InstrumentGuidePanel`을 신설해, 악기 이동 목록의 항목 [가이드] 버튼 클릭 시 그 악기의 가이드 페이지(이미지 2개 가로 + 텍스트 + Prev/Next)를 보여준다. 페이지 자산은 09 sub-spec이 박았던 `Resources/Tutorial/<악기명>/NN` 폴더 규약을 그대로 따른다. plan 1/2가 `// TODO sub-spec 10 plan 2/2`로 비워둔 `InstrumentTravelSectionController.OnGuideRequested(InstrumentTravelItem)` 안에서 `InstrumentGuidePanel.Open(InstrumentBase)`을 호출해 진입점을 연결한다.

## Context

sub-spec 10 §What·Behavior 중 plan 1/2가 다루지 않은 항목 전부를 본 plan이 책임진다:

- (a) [가이드] 버튼 클릭 → 세션 패널 외부 독립 패널 오픈.
- (b) `InstrumentGuidePanel` prefab + `InstrumentGuidePanelController` 신규.
- (c) `Tutorial/<악기명>/NN` 폴더 규약 준수 페이지 로더 (이미지 2개 + 텍스트).
- (d) Prev/Next 페이지 탐색 (첫/마지막 페이지에서 각 버튼 비활성).
- (e) 패널 공간 위치 정책.
- (f) `InstrumentTravelSectionController.OnGuideRequested` → `InstrumentGuidePanel.Open(...)` 경로 연결.

**OQ-10-1 (Trombone attach 자동 트리거 누락)은 본 plan 범위가 아니다.** 이유:
- sub-spec 10 §Open Questions가 본 OQ를 "plan 2/2 착수 전 또는 sub-spec 11에서" 검토하라고 두 후보 단계 모두 허용했고, 본 plan(2/2)의 주제는 [가이드] 동작이지 [이동] 사슬 보강이 아니다.
- plan 1/2 Notes가 "본 plan에서는 단일 진입점 invariant 유지가 우선" + "별도 sub-spec(11)로 escalation"이라고 박제했다.
- attach 사슬은 본 plan의 Approach·Deliverables 어느 곳도 변경하지 않는다 (가이드 패널은 텔레포트 사슬과 직교).
- 따라서 OQ-10-1은 본 plan에서 **그대로 미해결로 두고**, 별도 sub-spec 11(또는 polish plan)에서 다루도록 sub-spec 10 §Open Questions에 그대로 유지된다. 본 plan Handoff에서도 같은 입장 명시.

**패널 공간 위치 정책 (sub-spec 10이 plan에 위임):** 본 plan은 "세션 패널의 오른쪽 옆에 같은 거리·같은 정면 방향으로 떠 있는 독립 패널"로 결정한다. 이유:
- 사용자 시야 안에서 세션 패널과 동시 가시. anchor 근처는 사용자가 anchor 진입 전에는 그 위치에 시선이 가 있지 않다.
- SessionPanel 루트가 `localScale=(0.001,0.001,1)`, sizeDelta=(400,300) → 월드 폭 0.4m. 오른쪽 오프셋 0.5m면 시각적으로 겹치지 않으면서 시야 안.
- `InstrumentGuidePanel`은 **씬에 미리 두지 않고** `InstrumentTravelSectionController.Inject` 시점에 prefab으로 lazy instantiate(자식 X, 독립 root) — 세션 패널 본체가 닫혀도 가이드 패널이 따로 떠 있을 수 있게 한다 (closing policy도 본 plan에서 결정: 본 패널의 닫기 버튼 또는 다시 [가이드] 클릭 시 같은 패널 재바인딩).
- 위치 동기: `InstrumentTravelSectionController.OnGuideRequested`가 자기 transform(=세션 패널 안)에서 카메라 forward + 오른쪽 오프셋을 계산해 `InstrumentGuidePanel.transform`에 적용. **본 plan은 카메라 기준 1회 스냅 후 추적 없음** — SessionPanelController.snapOnce 정책과 동일 사상.

**자산 로딩 진입점 = `Resources.Load`:**
- sub-spec 10 §What가 박제한 `Tutorial/<악기명>/NN` 폴더 규약은 *런타임 동적 로드*가 필요한 자산이다. Unity의 정식 동적 로드 경로는 `Resources.Load<Sprite>(path)` (Addressables 도입 흔적 없음 — 본 프로젝트는 `StreamingAssets/Songs` 외 `Resources/` 폴더 패턴이 `Assets/Resources`(빈 디렉토리), `Assets/Multiplayer/Resources`(`MultiplayerAuthConfig`), `Assets/Photon/Fusion/Resources`로 이미 통용). 본 plan은 `Assets/Resources/Tutorial/<악기명>/01.png`, `02.png`, …식으로 페이지 번호 순 sprite를 두고 `Resources.LoadAll<Sprite>($"Tutorial/{instrumentId}")`로 일괄 로딩한다.
- 단, sub-spec 10 §What "이미지 2개(가로 배치) + 텍스트 1개로 구성" + 09 §What "Tutorial/<악기명>/01, 02, …" 두 규약을 합치면 **한 페이지 = 이미지 2개**다. 본 plan은 페이지 번호 폴더 안에 `left.png` / `right.png` / `text.txt`(또는 `description.txt`) 3종을 두는 규약으로 박제한다. **단일 페이지 = `Resources/Tutorial/<악기명>/<NN>` 폴더**. (09 sub-spec 박제와 자연스럽게 일치.)
- 텍스트는 `TextAsset`. 페이지 인덱싱은 `01`, `02`, … 폴더 순차 스캔으로 결정.
- **본 plan에서는 콘텐츠 자체는 비워두지 않고 placeholder 1개 페이지 (Resources/Tutorial/Trombone/01/{left.png, right.png, description.txt}) 도 만들지 않는다.** sub-spec 10 §Out of Scope·09 sub-spec "추후 개발자가 채워 넣음" 박제 그대로. **자산 폴더 부재 시(=현재 상태)**, `InstrumentGuidePanel.Open(...)`은 "콘텐츠 준비 중" placeholder UI 1페이지(빈 이미지 + 안내 텍스트)로 표시한다.

**`OnDisable` 정합:** plan 1/2가 `InstrumentTravelSectionController.OnDisable`에서 `ClearSnapshot()`을 호출해 아이템들을 Destroy한다. 같은 메서드에서 본 plan이 띄운 `InstrumentGuidePanel` 인스턴스도 닫는다(`SetActive(false)`) — 세션 패널을 닫으면 가이드 패널도 자동으로 사라진다. (가이드 패널 instance 자체는 destroy하지 않고 비활성만 — 다음 open 때 재사용.)

## Verified Structural Assumptions

- **`InstrumentTravelSectionController.OnGuideRequested(InstrumentTravelItem)` 진입점 박제:** `Read Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` (2026-05-26) 라인 131-135 — 메서드 body가 `// TODO sub-spec 10 plan 2/2` 주석 1줄만 들어 있는 빈 상태. 본 plan이 이 메서드 안에서 `_guidePanel.Open(item.Instrument)`을 호출. `item.Instrument`(plan 1/2 `InstrumentTravelItem.Instrument` public getter, 라인 22)는 `InstrumentBase` 반환 — `InstrumentId` 추출 + 위치 기준점 양쪽 다 가능.

- **`InstrumentTravelItem.cs` 공개 API 박제:** `Read Assets/SessionPanel/Scripts/InstrumentTravelItem.cs` (2026-05-26) — `public InstrumentBase Instrument => _instrument` (라인 22), `public bool IsCurrent => _isCurrent` (라인 23). `OnGuideClick`(라인 62-66)이 `_onGuide?.Invoke(this)`를 호출하므로 `_isCurrent`와 무관하게 항상 가이드 콜백 발화 — sub-spec 10 §What "같은 항목의 [가이드] 버튼은 항상 활성" invariant 박제 일치.

- **`IActiveInstrument` 식별자 박제:** `Read Assets/Instruments/_Core/Scripts/IActiveInstrumentProvider.cs` (2026-05-26) 라인 13-15 — `Transform PanelAnchor`, `string InstrumentId`, `Transform InstrumentRoot`. `InstrumentBase`가 구현 (plan 1/2 박제). 본 plan은 `InstrumentBase.InstrumentId`(string)을 사용해 `Resources/Tutorial/<InstrumentId>/…` 경로를 만든다.

- **`SessionPanel.Runtime.asmdef` references 박제:** `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` (2026-05-26) — `Instruments` / `RhythmGame.Data` / `RhythmGame.Runtime` / `Unity.InputSystem` / `Unity.XR.Interaction.Toolkit` / `Unity.TextMeshPro`. 본 plan이 import할 namespace: `UnityEngine`(autoReferenced), `UnityEngine.UI`(autoReferenced), `TMPro`(`Unity.TextMeshPro` 있음), `Instruments`(있음), `System.Collections.Generic`(BCL). **추가 reference 불필요.**

- **신규 C# 파일 위치 = `Assets/SessionPanel/Scripts/`:** plan 1/2가 박제한 동일 위치. 신규 `InstrumentGuidePanelController.cs` 1 파일이 본 asmdef에 자동 포함. 별도 asmdef 추가 불필요. 출처: `Read Assets/SessionPanel/Scripts/SessionPanel.Runtime.asmdef` (2026-05-26).

- **신규 prefab 위치 = `Assets/SessionPanel/Prefabs/`:** sub-spec 10 §Tech Spec(`Read docs/specs/session-panel/tech-specs/10-instrument-travel-list.md` 라인 30)이 `Prefabs/` 또는 `UI/` 둘 다 허용. 본 plan은 `InstrumentGuidePanel.prefab`을 **`Assets/SessionPanel/Prefabs/`**에 둔다 — 이유: `Assets/SessionPanel/CLAUDE.md` §1 가이드 ("다른 패널에 통째로 끼워 쓸 단위면 Prefabs/, 이 패널 내부에서만 의미면 UI/")에 따라 `InstrumentGuidePanel`은 **세션 패널 외부에 독립 떠 있는 top-level 패널**이므로 `Prefabs/`가 맞다. 기존 같은 폴더에 `SessionPanel.prefab` / `InstrumentToggleButton.prefab` / `InstrumentTravelItem.prefab`이 있다. 출처: `Read Assets/SessionPanel/CLAUDE.md` (2026-05-26) §1 + 폴더 ls 결과.

- **`Assets/Resources/` 디렉토리 현재 상태:** `find Assets/Resources -maxdepth 2 -type d` 결과 `Assets/Resources` 단일 빈 디렉토리. **`Assets/Resources/Tutorial/` 부재.** 본 plan은 폴더 규약만 박제하고 실제 콘텐츠는 추가하지 않으므로 `Resources.LoadAll<Sprite>("Tutorial/<id>/<NN>")`이 null/빈 배열을 반환할 때 placeholder UI로 graceful fallback해야 한다. 출처: `Bash find Assets -maxdepth 3 -type d -name "Resources"` (2026-05-26).

- **세션 패널 월드 크기 박제:** `Read Assets/SessionPanel/Prefabs/SessionPanel.prefab` (2026-05-26) — 루트 RectTransform fileID 4247339568228231875, `m_SizeDelta=(400, 300)`, `m_LocalScale=(0.001, 0.001, 1)` → 월드 크기 0.4m × 0.3m. 본 plan의 `InstrumentGuidePanel`도 같은 사상 (sizeDelta ~400×300, localScale 0.001×0.001×1)으로 박제해 두 패널이 동일 시각 스케일.

- **`SessionPanelController._panelInstance.transform` 위치 박제 (가이드 패널 배치 기준):** `Read Assets/SessionPanel/Scripts/SessionPanelController.cs` (2026-05-26) 라인 326-356. `PositionAtWrist`/`PositionAtInstrument` 모두 `_panelInstance.transform.position = camera.position + horizontalForward * distance` + `rotation = LookRotation(horizontalForward)`. 본 plan은 `InstrumentTravelSectionController`가 자기 부모 체인을 거슬러 SessionPanel 루트 transform을 얻고, 그 transform.right * 0.5m 오프셋으로 guide panel을 배치한다 (오른쪽 옆). rotation은 SessionPanel 루트의 rotation을 그대로 복사.

- **`Resources.LoadAll<T>` 시그니처 박제:** Unity 6의 `UnityEngine.Resources.LoadAll<T>(string path)`는 `path` 폴더 내부의 `T` 타입 자산 배열 반환. 폴더 부재 또는 자산 0개면 length 0 배열 반환(null 아님). 본 plan은 length==0 분기에서 placeholder UI로 fallback. 출처: Unity Scripting API 표준 사양 + `Assets/Multiplayer/Scripts/Auth/AuthBootstrap.cs:35` 의 `Resources.Load<MultiplayerAuthConfig>("MultiplayerAuthConfig")` 본 프로젝트 동작 검증.

- **plan 1/2 호출 외부 API `InstrumentTravelSectionController` side effect 박제 (본 plan이 호출하는 외부 컴포넌트 전체 동작):** `Read Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` (2026-05-26) 전체:
  - `Awake`/`OnEnable`/`OnDisable` lifecycle: provider 이벤트 구독/해제 + `BuildSnapshotIfNeeded()` + `RefreshCurrentState(_provider?.Current)` (OnEnable), `ClearSnapshot()` (OnDisable).
  - `Inject(UnityEngine.Object)`: provider 교체 + isActiveAndEnabled 시 즉시 `BuildSnapshotIfNeeded` + `RefreshCurrentState`. **본 plan이 같은 메서드 안에서 `EnsureGuidePanel()`도 호출하도록 확장**.
  - `BuildSnapshotIfNeeded`: `FindObjectsByType<InstrumentTeleportLink>` 스캔 → anchor + LinkedInstrument 동반자만 `Instantiate(instrumentTravelItemPrefab, itemContainer)` + `InstrumentTravelItem.Setup(instrument, onTravel, onGuide)`. **onGuide 람다가 `OnGuideRequested(it)` 호출 — 본 plan이 그 메서드 body를 채운다**.
  - `ClearSnapshot`: `Destroy(item.gameObject)` 루프 → `_items.Clear()` + `_snapshotBuilt = false`. **본 plan의 `OnDisable` 가이드 패널 비활성화는 ClearSnapshot 직후 또는 OnDisable에 직접 부착**.
  - `OnTravelRequested(item, anchor)` / `OnGuideRequested(item)`: 후자는 plan 1/2에서 무동작. 본 plan이 body 채움.
  - **본 plan은 위 lifecycle 흐름을 깨지 않는 형태로 `OnGuideRequested` body만 채우고 `OnDisable`에 한 줄 추가, `Inject`에 한 줄 추가, 신규 직렬화 필드 1개(`instrumentGuidePanelPrefab`) 추가**한다. 기존 [이동] 경로 어디도 변경하지 않는다.

- **`SessionPanel.prefab` Content 노드의 `InstrumentTravelSectionController` 직렬화 박제:** plan 1/2 Handoff에 따라 `Assets/SessionPanel/Prefabs/SessionPanel.prefab`에 `SessionPanel.InstrumentTravelSectionController` MonoBehaviour 직렬화가 존재(Content 노드 부착, 직렬화 필드 `itemContainer` / `instrumentTravelItemPrefab` / `activeInstrumentProviderObject`). 본 plan은 같은 MonoBehaviour 블록에 **신규 직렬화 필드 `instrumentGuidePanelPrefab: {fileID: 100100000, guid: <new>, type: 3}`을 append**한다. 출처: plan 1/2 Handoff + `Grep "InstrumentTravelSectionController" Assets/SessionPanel/Prefabs/SessionPanel.prefab`.

- **plan 1/2 신규 자산 GUID 박제(handoff 인용):**
  - `InstrumentTravelItem.prefab`: `2917de9c7dc9f41f3929e478aab1f9f8` (확인: `Bash cat Assets/SessionPanel/Prefabs/InstrumentTravelItem.prefab.meta` 2026-05-26).
  - `InstrumentTravelSectionController.cs`: `f6fc1b52b4904498cae4940cb97474a7`.
  - `InstrumentTravelItem.cs`: `086c88c56ee1d4202bbb310c52a15153`.
  - 본 plan은 위 자산들을 **참조만** 하고 수정하지 않는다. `InstrumentTravelSectionController.cs`만 **부분 수정** (OnGuideRequested body 채움 + 직렬화 필드 1개 추가).

## Approach

### 1. 신규 C# 파일 `Assets/SessionPanel/Scripts/InstrumentGuidePanelController.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Instrument Guide Panel Controller")]
    public class InstrumentGuidePanelController : MonoBehaviour
    {
        [SerializeField] Image leftImage;
        [SerializeField] Image rightImage;
        [SerializeField] TextMeshProUGUI descriptionText;
        [SerializeField] TextMeshProUGUI titleLabel;
        [SerializeField] TextMeshProUGUI pageIndicator;   // "1 / N" 표시 (선택, null 허용)
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] Button closeButton;
        [SerializeField] string placeholderMessage = "콘텐츠 준비 중입니다.";

        readonly List<Page> _pages = new List<Page>();
        int _pageIndex;
        string _instrumentId;

        struct Page
        {
            public Sprite left;
            public Sprite right;
            public string description;
        }

        void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(OnPrev);
            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        /// <summary>InstrumentTravelSectionController.OnGuideRequested 가 호출.</summary>
        public void Open(InstrumentBase instrument)
        {
            if (instrument == null) return;
            _instrumentId = instrument.InstrumentId;
            LoadPages(_instrumentId);
            _pageIndex = 0;
            gameObject.SetActive(true);
            if (titleLabel != null)
                titleLabel.text = _instrumentId;
            Apply();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        void LoadPages(string instrumentId)
        {
            _pages.Clear();
            if (string.IsNullOrEmpty(instrumentId)) return;

            // sub-spec 10 §What + 09 sub-spec 박제 폴더 규약:
            // Resources/Tutorial/<instrumentId>/<NN>/{left.png, right.png, description.txt}
            // NN은 01부터 순차 — 없는 번호가 나오면 거기서 스캔 종료.
            int n = 1;
            while (true)
            {
                string pad = n.ToString("00");
                string folder = $"Tutorial/{instrumentId}/{pad}";
                var left = Resources.Load<Sprite>($"{folder}/left");
                var right = Resources.Load<Sprite>($"{folder}/right");
                var textAsset = Resources.Load<TextAsset>($"{folder}/description");
                if (left == null && right == null && textAsset == null) break;
                _pages.Add(new Page
                {
                    left = left,
                    right = right,
                    description = textAsset != null ? textAsset.text : string.Empty,
                });
                n++;
                if (n > 99) break; // 안전 한도
            }
        }

        void OnPrev()
        {
            if (_pages.Count == 0) return;
            if (_pageIndex <= 0) return;
            _pageIndex--;
            Apply();
        }

        void OnNext()
        {
            if (_pages.Count == 0) return;
            if (_pageIndex >= _pages.Count - 1) return;
            _pageIndex++;
            Apply();
        }

        void Apply()
        {
            bool hasPages = _pages.Count > 0;

            if (hasPages)
            {
                var page = _pages[_pageIndex];
                if (leftImage != null)
                {
                    leftImage.sprite = page.left;
                    leftImage.enabled = page.left != null;
                }
                if (rightImage != null)
                {
                    rightImage.sprite = page.right;
                    rightImage.enabled = page.right != null;
                }
                if (descriptionText != null)
                    descriptionText.text = page.description;
                if (pageIndicator != null)
                    pageIndicator.text = $"{_pageIndex + 1} / {_pages.Count}";
            }
            else
            {
                // sub-spec 10 §Out of Scope 박제: 콘텐츠 자체는 본 plan 작성 시 없음.
                if (leftImage != null) leftImage.enabled = false;
                if (rightImage != null) rightImage.enabled = false;
                if (descriptionText != null) descriptionText.text = placeholderMessage;
                if (pageIndicator != null) pageIndicator.text = "0 / 0";
            }

            // 첫/마지막 페이지에서 각 버튼 비활성 (sub-spec 10 §What·09 Behavior 박제).
            if (prevButton != null)
                prevButton.interactable = hasPages && _pageIndex > 0;
            if (nextButton != null)
                nextButton.interactable = hasPages && _pageIndex < _pages.Count - 1;
        }
    }
}
```

### 2. 신규 prefab `Assets/SessionPanel/Prefabs/InstrumentGuidePanel.prefab`

루트 GameObject `InstrumentGuidePanel`:
- `Canvas`(world-space, sortingOrder=0) + `CanvasScaler`(scaleFactor=1) + `GraphicRaycaster` — SessionPanel.prefab과 동일 사상.
- 루트 RectTransform: anchor (0.5, 0.5)~(0.5, 0.5), sizeDelta=(400, 300), localScale=(0.001, 0.001, 1) — SessionPanel과 동일 월드 크기 (0.4m × 0.3m).
- 자식 `Background` (Image, fullstretch, Sprite=`PanelRoundedRect`(GUID `371cc7f19be24cc8944b77449df386f1`), Color (0.05, 0.07, 0.12, 0.92)).
- 자식 `TitleLabel` (TextMeshProUGUI, anchor (0,1)~(1,1) anchoredPos(0,-20), sizeDelta(0,30), text "(악기명)").
- 자식 `LeftImage` (Image, anchor (0,0.3)~(0.5,0.85), sprite=null preserveAspect=true).
- 자식 `RightImage` (Image, anchor (0.5,0.3)~(1,0.85), sprite=null preserveAspect=true).
- 자식 `DescriptionText` (TextMeshProUGUI, anchor (0,0.1)~(1,0.3), sizeDelta(0,0), fontSize=14, alignment=Center).
- 자식 `PrevButton` (Button + Image=`ButtonRoundedRect`(GUID `30dbb1d077de4bb7b01b367491dbae50`) + 자식 TMP "Prev", anchor (0,0)~(0.25,0.1)).
- 자식 `NextButton` (Button + Image=`ButtonRoundedRect` + 자식 TMP "Next", anchor (0.25,0)~(0.5,0.1)).
- 자식 `PageIndicator` (TextMeshProUGUI, anchor (0.5,0)~(0.75,0.1), text "0 / 0").
- 자식 `CloseButton` (Button + Image=`ButtonRoundedRect` + 자식 TMP "Close", anchor (0.75,0)~(1,0.1)).
- 루트에 `InstrumentGuidePanelController` MonoBehaviour 부착, 직렬화 필드 8종(`leftImage`, `rightImage`, `descriptionText`, `titleLabel`, `pageIndicator`, `prevButton`, `nextButton`, `closeButton`) 모두 wire.

> **시각 polish는 본 plan의 핵심 아님.** sub-spec 10 §What·Behavior가 요구하는 (이미지 2개 가로 + 텍스트 1개 + Prev/Next 페이지 탐색) 기능만 충족하면 된다. 색상/sprite는 SessionPanel 톤을 답습.

### 3. `Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` 부분 수정

**(a) 신규 직렬화 필드 1개 추가** (기존 3종 옆):

```csharp
[SerializeField] GameObject instrumentGuidePanelPrefab;
```

**(b) 신규 private 필드 + EnsureGuidePanel 헬퍼:**

```csharp
InstrumentGuidePanelController _guidePanel;

void EnsureGuidePanel()
{
    if (_guidePanel != null) return;
    if (instrumentGuidePanelPrefab == null) return;

    // 세션 패널과 같은 시각 사상으로 독립 root에 인스턴스화.
    // 부모는 null — 세션 패널을 닫아도 가이드 패널이 독립적으로 위치 유지 가능.
    var go = Instantiate(instrumentGuidePanelPrefab);
    _guidePanel = go.GetComponent<InstrumentGuidePanelController>();
    go.SetActive(false);
    PositionGuidePanel(go.transform);
}

void PositionGuidePanel(Transform t)
{
    // SessionPanel 루트(= 가장 가까운 활성 루트 Canvas의 transform)의 오른쪽으로 0.5m 오프셋.
    // SessionPanelController.PositionAtWrist/PositionAtInstrument가 _panelInstance.transform을
    // camera 정면으로 두기 때문에 root.right * 0.5는 카메라 오른쪽으로 0.5m가 된다.
    var sessionRoot = transform.root; // SessionPanel.prefab 루트
    if (sessionRoot == null) return;
    t.position = sessionRoot.position + sessionRoot.right * 0.5f;
    t.rotation = sessionRoot.rotation;
    t.localScale = new Vector3(0.001f, 0.001f, 1f);
}
```

**(c) `Inject` 메서드 안에 한 줄 추가:**

```csharp
// 기존 BuildSnapshotIfNeeded() 호출 직전(혹은 직후)에 다음 한 줄.
EnsureGuidePanel();
```

**(d) `OnGuideRequested(InstrumentTravelItem item)` body 채움:**

```csharp
void OnGuideRequested(InstrumentTravelItem item)
{
    if (item == null || item.Instrument == null) return;
    EnsureGuidePanel();
    if (_guidePanel == null) return;
    // 클릭 시점에 매번 위치 재스냅 — 사용자가 그동안 이동/회전했을 수 있다.
    PositionGuidePanel(_guidePanel.transform);
    _guidePanel.Open(item.Instrument);
}
```

**(e) `OnDisable` 끝에 한 줄 추가:**

```csharp
// 세션 패널이 닫히면 가이드 패널도 함께 비활성 (sub-spec 10 §What·invariant 4 — 같은 패널 1개 유지).
if (_guidePanel != null) _guidePanel.gameObject.SetActive(false);
```

### 4. `SessionPanel.prefab` Content 노드 직렬화 수정

Content 노드의 `InstrumentTravelSectionController` MonoBehaviour 블록에:
- 신규 직렬화 필드 `instrumentGuidePanelPrefab: {fileID: 100100000, guid: <InstrumentGuidePanel.prefab guid>, type: 3}` append.

씬 수정 없음. SessionPanel.prefab의 다른 노드(VolumePanel / RhythmGamePanel / TabBar) 변경 없음. **plan 1/2가 추가한 InstrumentTravelPanel / TravelTabButton / TabPanelController 직렬화 변경하지 않는다.**

### 5. (선택) placeholder 페이지 자산 미부착 결정

sub-spec 10 §Out of Scope: "가이드 페이지 텍스트/이미지의 실제 콘텐츠 제작" 본 plan 범위 아님. `Resources/Tutorial/` 디렉토리 자체도 본 plan에서 생성하지 않는다 — `Resources.Load<Sprite>(...)`가 null 반환 시 `InstrumentGuidePanelController.Apply()`가 placeholder 분기로 graceful fallback. **즉 본 plan은 "콘텐츠 0건이어도 패널은 정상 열리고 닫힘"이 manual-hard AC의 1조건**.

### 6. 검증

- **컴파일 대기:** `editor_state.isCompiling==false` 폴링 후 `read_console types=["error"]` 0건 확인 (`.claude/skills/unity-mcp-workflow/SKILL.md`).
- **시각 검증(manual-hard):** TestSceneSanyo 로드 → SessionPanel pinch open → 세 번째 탭 → 한 항목 [가이드] 클릭 → 세션 패널 오른쪽 옆에 InstrumentGuidePanel이 뜨고 placeholder 텍스트 "콘텐츠 준비 중입니다." 표시 + Prev/Next 모두 비활성(콘텐츠 0개) + Close 버튼으로 닫힘. 다른 항목 [가이드] 클릭 시 같은 패널 instance가 재바인딩.

## Deliverables

- `Assets/SessionPanel/Scripts/InstrumentGuidePanelController.cs` (신규) — 가이드 패널 진입 MonoBehaviour. Open/Close/Prev/Next 페이지 탐색 + Resources 폴더 스캔 로더.
- `Assets/SessionPanel/Prefabs/InstrumentGuidePanel.prefab` (신규) — Canvas + Background + LeftImage/RightImage/DescriptionText/TitleLabel/Prev/Next/Close/PageIndicator + `InstrumentGuidePanelController` 부착.
- `Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` (수정) — 신규 직렬화 필드 `instrumentGuidePanelPrefab` 추가 + `EnsureGuidePanel`/`PositionGuidePanel` 헬퍼 추가 + `Inject`/`OnDisable`에 한 줄씩 + `OnGuideRequested` body 채움.
- `Assets/SessionPanel/Prefabs/SessionPanel.prefab` (수정) — Content 노드의 `InstrumentTravelSectionController` MonoBehaviour 블록에 `instrumentGuidePanelPrefab` 직렬화 1개 append.

## Acceptance Criteria

- [ ] `[auto-hard]` 신규 C# 파일 1종 + 수정 파일 1종이 컴파일 0 error.
  **검증:** MCP `read_console action=get types=["error"]` 0건. 절차: 파일 추가 후 `editor_state.isCompiling==false` 폴링 → console read.

- [ ] `[auto-hard]` `InstrumentGuidePanelController.cs`가 의도한 public API/serialized fields를 노출한다.
  **검증:** `Grep -n "public class InstrumentGuidePanelController\|public void Open\|public void Close\|\[SerializeField\] Image leftImage\|\[SerializeField\] Image rightImage\|\[SerializeField\] TextMeshProUGUI descriptionText\|\[SerializeField\] Button prevButton\|\[SerializeField\] Button nextButton\|\[SerializeField\] Button closeButton" Assets/SessionPanel/Scripts/InstrumentGuidePanelController.cs` — 9개 패턴 모두 1건 이상 매치.

- [ ] `[auto-hard]` `InstrumentGuidePanelController.LoadPages`가 `Resources/Tutorial/<id>/<NN>/{left,right,description}` 폴더 규약을 사용한다.
  **검증:** `Grep -nE "Resources\.Load<Sprite>\(\\\$\\\"Tutorial/" Assets/SessionPanel/Scripts/InstrumentGuidePanelController.cs` 2건 이상 매치 (`/left` + `/right`) + `Grep -n "Resources.Load<TextAsset>" Assets/SessionPanel/Scripts/InstrumentGuidePanelController.cs` 1건 매치 (`/description`).

- [ ] `[auto-hard]` Prev/Next 첫/마지막 페이지 invariant가 코드에서 보인다.
  **검증:** `Grep -n "prevButton.interactable\|nextButton.interactable" Assets/SessionPanel/Scripts/InstrumentGuidePanelController.cs` — 2개 패턴 모두 `_pageIndex > 0` / `_pageIndex < _pages.Count - 1` 조건과 함께 매치되는지 시각 확인.

- [ ] `[auto-hard]` `InstrumentTravelSectionController.OnGuideRequested` 메서드가 더 이상 빈 body가 아니라 `_guidePanel.Open(item.Instrument)`을 호출한다.
  **검증:** `Grep -nA 8 "void OnGuideRequested" Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` — body 안에 `_guidePanel.Open(item.Instrument)` 또는 `_guidePanel.Open(...)` 1건 매치 + `// TODO sub-spec 10 plan 2/2` 주석 0건 (제거 또는 갱신).

- [ ] `[auto-hard]` `InstrumentTravelSectionController.cs`에 `instrumentGuidePanelPrefab` 직렬화 필드 + `EnsureGuidePanel` 헬퍼 + `PositionGuidePanel` 헬퍼가 추가됐다.
  **검증:** `Grep -n "\[SerializeField\] GameObject instrumentGuidePanelPrefab\|void EnsureGuidePanel\|void PositionGuidePanel\|InstrumentGuidePanelController _guidePanel" Assets/SessionPanel/Scripts/InstrumentTravelSectionController.cs` — 4개 패턴 모두 1건 이상 매치.

- [ ] `[auto-hard]` `InstrumentGuidePanel.prefab` 신규 파일이 존재하고, 루트 GameObject가 `InstrumentGuidePanelController` 컴포넌트 + Canvas + `LeftImage`/`RightImage`/`DescriptionText`/`PrevButton`/`NextButton`/`CloseButton` 자식 6종을 가진다.
  **검증:** `Bash ls Assets/SessionPanel/Prefabs/InstrumentGuidePanel.prefab` 성공 + `Grep -n "m_Name: LeftImage\|m_Name: RightImage\|m_Name: DescriptionText\|m_Name: PrevButton\|m_Name: NextButton\|m_Name: CloseButton\|SessionPanel.InstrumentGuidePanelController\|m_Component:.*Canvas " Assets/SessionPanel/Prefabs/InstrumentGuidePanel.prefab` — 6개 자식 이름 + 1개 controller + Canvas 모두 매치.

- [ ] `[auto-hard]` `SessionPanel.prefab`의 `InstrumentTravelSectionController` 직렬화 블록에 `instrumentGuidePanelPrefab` 항목이 추가됐고 그 값이 신규 `InstrumentGuidePanel.prefab`을 가리킨다.
  **검증:** `Grep -nA 12 "InstrumentTravelSectionController" Assets/SessionPanel/Prefabs/SessionPanel.prefab` 결과 안 `instrumentGuidePanelPrefab:` 항목 1건 매치 + 그 값의 `guid` 가 `Bash cat Assets/SessionPanel/Prefabs/InstrumentGuidePanel.prefab.meta`의 guid와 일치.

- [ ] `[auto-soft]` 본 plan은 `TromboneAnchor` / `InstrumentTeleportLink` / `TeleportInstrumentProvider` / `InstrumentBase` / `IActiveInstrumentProvider` C# 소스를 수정하지 않는다(읽기만). 씬도 수정하지 않는다.
  **검증:** `Bash git status` 또는 `git diff --name-only` 결과에 `Assets/Instruments/**/*.cs` 0건 + `Assets/Scenes/*.unity` 0건.

- [ ] `[auto-soft]` 본 plan은 plan 1/2가 추가한 `InstrumentTravelItem.cs` / `InstrumentTravelItem.prefab` / `SessionPanel.prefab`의 TravelTabButton·InstrumentTravelPanel·TabPanelController 직렬화 영역을 수정하지 않는다.
  **검증:** `Bash git diff Assets/SessionPanel/Scripts/InstrumentTravelItem.cs Assets/SessionPanel/Prefabs/InstrumentTravelItem.prefab` 결과 변경 0건 + `git diff Assets/SessionPanel/Prefabs/SessionPanel.prefab`의 변경이 Content 노드의 `InstrumentTravelSectionController` 블록(`instrumentGuidePanelPrefab` 1줄 append)에 한정됨을 시각 확인.

- [ ] `[auto-soft]` `Assets/SessionPanel/CLAUDE.md` §1 폴더 규약 위반 없음 — `InstrumentGuidePanel.prefab`이 `Prefabs/`(독립 패널) 위치 + `InstrumentGuidePanelController.cs`가 `Scripts/` 위치.
  **검증:** `Bash ls Assets/SessionPanel/Prefabs/InstrumentGuidePanel.prefab Assets/SessionPanel/Scripts/InstrumentGuidePanelController.cs` 둘 다 존재.

- [ ] `[manual-hard]` Editor에서 `Assets/Scenes/TestSceneSanyo.unity` 로드 → SessionPanel pinch open → 세 번째 탭 → 임의 항목 [가이드] 클릭 → 세션 패널 오른쪽 옆에 `InstrumentGuidePanel`이 뜨고 (i) 콘텐츠 부재 시 placeholder 텍스트 "콘텐츠 준비 중입니다." 표시 + Prev/Next 비활성 + Close 버튼으로 닫힘 (ii) 다른 항목 [가이드] 클릭 시 같은 패널 instance가 그 악기로 재바인딩(타이틀 라벨 변경).
  **검증:** TestSceneSanyo 로드 → SessionPanel open → InstrumentTravelTab 클릭 → Trombone 항목 [가이드] 클릭 → 가이드 패널 시각 확인(스크린샷 1장) → Close 클릭 → 사라짐 확인 → Piano 항목 [가이드] 클릭 → 같은 패널 instance 재사용 + 타이틀 "Piano"로 갱신.

- [ ] `[manual-hard]` 현재 위치한 anchor의 [가이드] 버튼도 정상 동작한다 (sub-spec 10 §What "같은 항목의 [가이드] 버튼은 항상 활성"). 즉 [이동]이 비활성된 항목의 [가이드] 클릭 시 가이드 패널 정상 오픈.
  **검증:** anchor에 도달한 후 SessionPanel 재오픈 → 그 anchor 항목의 [이동]이 비활성 회색인 상태에서 같은 항목 [가이드] 클릭 → 가이드 패널 정상 오픈 확인. 시각 검증.

- [ ] `[manual-hard]` 세션 패널을 닫으면 가이드 패널도 함께 비활성(또는 사라짐)되며, 패널을 다시 열고 [가이드] 클릭 시 가이드 패널이 다시 뜬다.
  **검증:** SessionPanel pinch open → 가이드 패널 open → SessionPanel pinch close(토글) → 가이드 패널 사라짐 확인 → SessionPanel pinch open → [가이드] 클릭 → 가이드 패널 다시 뜸. 시각 검증.

## Out of Scope

- **OQ-10-1 Trombone attach 자동 트리거 누락 해소** — sub-spec 10 §Open Questions의 OQ-10-1은 sub-spec 11 또는 별도 polish plan에서 처리. 본 plan은 [가이드] 동작만 다루며 [이동] 사슬을 변경하지 않는다.
- **가이드 페이지 텍스트/이미지의 실제 콘텐츠 제작** — sub-spec 10 §Out of Scope + 09 sub-spec "추후 개발자가 채워 넣음" 박제 그대로. 본 plan은 폴더 규약(`Resources/Tutorial/<id>/<NN>/{left,right,description}`)과 placeholder 분기만 박제.
- **가이드 패널의 정확한 공간 위치 polish** — 본 plan은 "SessionPanel 루트의 오른쪽 0.5m, 동일 정면 방향, 1회 스냅" 정책을 채택했지만 시각 polish(거리/각도 미세 조정, 손 추적 등)는 후속 polish plan 후보.
- **멀티 가이드 패널 동시 노출** — sub-spec 10 §Invariants 4 "동시에 2개 가이드 패널이 떠 있지 않는다"에 따라 single instance 재사용. 멀티는 본 plan 범위 밖.
- **가이드 패널의 닫기 입력 polish** — 본 plan은 Close 버튼 + 세션 패널 닫힘 2가지만 채택. 손 제스쳐/컨트롤러 입력 매핑은 후속 plan 후보.
- **상단 탭으로 악기 선택** (09 sub-spec의 단독 UI 패턴) — 10이 09를 흡수하면서 진입점이 항목 [가이드] 버튼별로 분산됐기에 본 plan에서는 상단 탭을 두지 않는다.
- **세션 패널 본체 / `SessionPanel.prefab`의 TabBar·VolumePanel·RhythmGamePanel·InstrumentTravelPanel 변경** — plan 1/2가 박제한 영역, 본 plan은 건드리지 않는다 (`InstrumentTravelSectionController` 직렬화 1줄 append 제외).
- **씬(`TestSceneSanyo.unity`) 수정** — 본 plan은 prefab + 신규 스크립트 추가/수정만.

## Notes

- **OQ-10-1 상태 그대로 유지:** sub-spec 10 §Open Questions에 OQ-10-1이 그대로 남아 있다. 본 plan은 attach 사슬에 손대지 않는다. attach 자동 트리거가 누락된 케이스가 manual-hard 회귀로 재현되면 후속 sub-spec(11) 또는 polish plan에서 해결.
- **Tutorial 폴더 콘텐츠 추가 시 운용:** 향후 개발자가 `Assets/Resources/Tutorial/<악기명>/01/left.png` 식으로 추가하면 본 plan의 `LoadPages`가 자동 인식해 페이지로 노출. 텍스트는 같은 폴더의 `description.txt`(TextAsset). 페이지 종료 조건은 "left/right/description 셋 다 null인 폴더 발견 시". 따라서 폴더 번호는 **연속**(01, 02, 03 …)이어야 하며 중간에 빠진 번호는 무시되고 종료.
- **`Resources.Load` 성능:** 본 plan의 `LoadPages`는 패널 [가이드] 클릭 시 1회만 실행 (hot path 아님). 페이지 수가 늘어도 99 안전 한도 + 자산 부재 시 즉시 break.
- **`PositionGuidePanel`의 `transform.root`:** `InstrumentTravelSectionController`는 `SessionPanel.prefab`의 Content 자식이므로 `transform.root` = SessionPanel 루트 transform. 만약 향후 prefab 구조가 깊어지면 명시 reference로 교체 검토.
- **후속 plan 후보:**
  - **sub-spec 11 후보:** OQ-10-1 Trombone attach 자동 트리거 보강(악기별 attach API 직접 호출 보조 경로 또는 TromboneAnchor의 RequestTeleport-safe 진입점).
  - **(후속 polish):** 가이드 패널 시각 polish (sprite/색상 톤, 페이지 인디케이터 시각, 닫기 입력 매핑).
  - **(콘텐츠):** 실제 Tutorial 자산 작성 — `Resources/Tutorial/Trombone/01/{left,right,description}` 부터.

## Handoff

- **OQ-10-1 (Trombone attach 자동 트리거 누락)은 본 plan 후에도 미해결.** sub-spec 10 §Open Questions에 그대로 유지되며 sub-spec 11 또는 별도 polish plan이 처리한다. 본 plan은 attach 사슬에 손대지 않는다.
- 신규 진입점: `InstrumentGuidePanelController.Open(InstrumentBase instrument)` — `InstrumentTravelSectionController.OnGuideRequested`가 호출. 외부에서 호출할 필요 없음.
- 신규 자산 GUID(작성 후 채울 항목):
  - `InstrumentGuidePanel.prefab`: (작성 시 자동 발급)
  - `InstrumentGuidePanelController.cs`: (작성 시 자동 발급)
- sub-spec 10 §What·Behavior 전 시나리오 본 plan + plan 1/2 합쳐 cover됨 (OQ-10-1 attach 보강 제외).
- 자동 회귀: EditMode/PlayMode 회귀는 본 plan의 신규 클래스가 외부 의존 없는 단순 MonoBehaviour라 기존 테스트 변경 없이 통과 예상. unity-test-runner 1회 호출로 확인.
