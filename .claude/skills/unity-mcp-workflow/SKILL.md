---
name: unity-mcp-workflow
description: Unity MCP 도구로 스크립트·씬·컴포넌트·프리팹·애니메이션·카메라·물리·UI를 생성·수정하기 직전에 사용한다. Resource-First 사전 점검, 스크립트 컴파일 대기, 변경 후 read_console·screenshot 검증, batch_execute 의존성 처리, UI Toolkit/uGUI 분기, VR 손/카메라/물리 도메인 가이드, precondition_sha256 stale-file 방지, 도메인 리로드·"Already Exists"·컴파일 블록 같은 error recovery 절차를 안내한다. implementer·unity-scene-writer·unity-scene-reader가 자동 invoke한다.
allowed-tools: Read, Edit, Glob, Grep, Bash, mcp__UnityMCP__read_console, mcp__UnityMCP__refresh_unity, mcp__UnityMCP__find_gameobjects, mcp__UnityMCP__manage_asset, mcp__UnityMCP__manage_components, mcp__UnityMCP__manage_gameobject, mcp__UnityMCP__manage_scene, mcp__UnityMCP__manage_prefabs, mcp__UnityMCP__manage_script
---

# Unity MCP 워크플로우 규칙

이 skill은 VirtualMusicStudio_Unity 프로젝트에서 Unity MCP 도구를 호출하기 직전에 로드되어, 사전 점검·검증·복구 절차를 컨텍스트에 박제한다. `unity-asset-edit`(직렬화 자산 수정 안전 절차)·`AGENTS.md`(MCP 사용 정책)와 짝을 이룬다.

## 0. 사용 전제

- 본 skill은 **절차**만 제공한다. 수정 범위 고정·동명 오브젝트 식별 같은 **자산 안전 절차**는 [`unity-asset-edit`](../unity-asset-edit/SKILL.md)이 단일 진실원이다.
- 본 skill은 **외부 글로벌 skill 참조 없이 self-contained**다. 다른 위치의 skill·문서를 호출하거나 경로로 참조하지 않는다.
- MCP 도구 미가용 시 정책은 [`AGENTS.md`의 "Unity MCP 사용 정책"](../../../AGENTS.md) 단일 진실원 — 작업을 멈추고 사용자/메인에 보고한다. 본 skill 절차로 우회하지 않는다.

## 1. 사전 점검 (Resource-First)

Unity MCP 도구를 처음 호출하기 직전에 다음 1~3종을 1회 읽고, plan 또는 변경 사고에 영향을 주는 사실이 있으면 보고/박제한다.

| 리소스 | 확인 항목 | 무엇에 영향 |
|---|---|---|
| `mcpforunity://editor/state` | `is_compiling`, `ready_for_tools`, play mode 여부 | 컴파일 중에는 새 타입 attach·도메인 의존 작업이 busy로 실패. play mode면 다수 변경이 임시값. |
| `mcpforunity://project/info` | 패키지 가용성(uiToolkit/ugui/textmeshpro/cinemachine), `activeInputHandler`, 렌더 파이프라인 | UI 생성 분기 결정·EventSystem 모듈 선택·머티리얼/볼륨 설정. |
| `mcpforunity://scene/gameobject/{id}/components` 또는 `find_gameobjects` 결과 | 대상 GameObject가 실제 존재하는지, 부모/자식 컨텍스트, 부착 컴포넌트 | "Already Exists" 회피·동명 오브젝트 분기·override 의도 분간. |

리소스 직접 read가 환경상 어려우면 등가 도구로 대체:

- `editor/state` → `read_console(types=["error","warning"], count=5)` + 컴파일 대기 폴 (다음 섹션).
- `project/info` → `Read Packages/manifest.json` + `Read ProjectSettings/ProjectSettings.asset`(activeInputHandler 검색).
- 씬/프리팹 사실 → `unity-scene-reader` Task 위임.

**안티패턴.** 리소스 미확인 상태에서 `manage_components(action="add")`·`manage_gameobject(action="create")`를 곧장 호출. 컴파일 중이거나 동명 오브젝트가 있으면 즉시 실패하거나 잘못된 인스턴스를 건드린다.

## 2. 스크립트 변경 워크플로우

`.cs` 파일을 새로 만들거나 수정하면 **반드시** 컴파일 통과를 확인한 후에만 새 타입을 사용한다.

표준 절차:

1. `manage_script(action="create"|"apply_edits")` 또는 `Edit`/`Write`로 `.cs` 변경.
2. `refresh_unity(wait_for_ready=True)` 호출 — 자동 import + 컴파일 트리거 + 도메인 리로드 완료까지 대기.
   - `wait_for_ready` 미지원 환경이면 `read_console`로 폴링: 30초 동안 1초 간격으로 확인, 콘솔에 `Compilation finished` 로그가 나오거나 새 error/warning이 더 이상 갱신되지 않으면 완료로 간주.
3. `read_console(types=["error"], count=20, include_stacktrace=True)` 통과 확인. error 0건이어야 다음 단계 진행.
4. 위 3단계 통과 전까지 새 타입을 `manage_components(action="add")`로 attach 금지. attach는 별도 호출/별도 batch로.

**안티패턴.**
- 같은 `batch_execute` 명령에 `manage_script(create) → manage_components(add 새 타입)`을 넣는 것. 컴파일이 끝나기 전에 attach가 실행돼 "Type not found" 또는 즉시 실패.
- `refresh_unity`를 매번 호출하는 것. 자동 import·컴파일이 이미 트리거된 상태라 중복이며, 도메인 리로드 중복으로 연결 손실 가능.
- `manage_script` 호출 후 `read_console` 생략. 컴파일 에러를 못 보고 attach 단계에서 모호한 실패.

## 3. 변경 후 검증

매 변경 단위(논리적 한 묶음) 직후 다음을 수행한다.

1. **콘솔 검증.** `read_console(types=["error","warning"], count=10)` — 새로 생긴 error/warning 없음 확인. warning은 plan에 따라 무시 가능하나 reviewer 점검 대상.
2. **시각 검증 (선택).** GameObject 배치/카메라 lens/머티리얼/UI 같은 **시각적 변화가 핵심**인 변경에는 screenshot 1장.
   - `manage_camera(action="screenshot", capture_source="game_view"|"scene_view", view_target="<이름>", batch="single", width=512, include_image=True)` 권장.
   - 해상도 256–512px로 token 절약. 다각도가 필요할 때만 `batch="surround"` 또는 `batch="orbit"`.
   - `include_image=True`는 token 부담을 인지한 후에만. 단순 파일 저장 목적이면 `include_image=False`.
   - 본 프로젝트 화이트리스트(`.claude/settings.json`)에 `manage_camera`가 없으면 호출 시 prompt 승인이 뜬다 — plan에서 미리 허용 명시 권장.
3. **자산 사실 위임.** prefab 계층/직렬화 raw/PrefabInstance override 같은 **사실 검증**은 `unity-scene-reader` Task에 위임. 메인 컨텍스트에서 직접 raw YAML을 읽지 않는다.

**안티패턴.** "fire-and-forget" — 변경만 하고 콘솔/시각 확인 없이 다음 단계로 진행. 이번 plan에서 사고가 누적돼 다음 plan에서 원인 추적 불가.

## 4. batch_execute 사용 정책

여러 MCP 호출을 하나의 명령에 묶어 보낸다. **단, 본 프로젝트 화이트리스트에 `batch_execute`가 없으므로 호출 시 prompt 승인이 뜬다** — 사용 빈도가 충분히 잦아진 시점에 다음 plan으로 화이트리스트 추가.

### 4.1 사용 시점

- 독립 작업 ≥ 2개를 묶을 수 있을 때 (예: GameObject 5개 한꺼번에 생성).
- 같은 도구를 반복 호출할 때 (grid spawn, 동일 컴포넌트 부착).
- 여러 `find_gameobjects`로 발견 단계만 처리할 때.
- **(의무)** 동일 plan/단계 안에서 같은 도구를 **3회 이상** 반복 호출하게 되면 `batch_execute`로 묶는다. 묶지 않은 채 4회째 호출은 `reviewer`가 `needs-fix` 처리한다. `find_gameobjects`류 read 도구는 timeout 영향 최소화를 위해 2회 묶기까지 허용.

### 4.2 fail_fast 결정

| 상황 | `fail_fast` |
|---|---|
| 의존성 있는 시퀀스 (예: GameObject 생성 → 같은 객체에 컴포넌트 부착) | `True` — 앞이 실패하면 뒤를 실행해도 무의미 |
| 독립 변경 (예: 동명 prefab 5개 일괄 인스턴스화) | `False` — partial success 허용 |

### 4.3 분할 한도

- 명령 ≤ 25개 단위로 분할 (서버 기본 제한). 더 큰 batch는 호스트가 잘라낸다.
- `parallel=True`는 *조언*일 뿐이다. Unity가 메인 스레드 의존 작업을 순차 실행할 수 있으므로 의존성 없을 때만 사용.

### 4.4 안티패턴

- 한 batch에 `manage_script(create) + manage_components(add 새 타입)` — 섹션 2의 컴파일 대기 룰 위반.
- 한 batch에 25개 초과 — 잘려서 silent partial 실행.
- 의존 시퀀스인데 `fail_fast=False` — 앞이 실패해도 뒤가 실행돼 카오스.

## 5. UI Toolkit / uGUI 분기

UI를 만들거나 수정할 때는 **반드시** Step 0를 먼저 한다. 본 프로젝트 기본값(2026-05-07 시점):

- Unity 6000.3.10f1 / URP
- TextMeshPro: 사용 (Built-in)
- UI 시스템: 프로젝트마다 다를 수 있어 사용 직전 `Read Packages/manifest.json` 확인.
- Input System: 활용 형태는 sub-spec별 — 사용 직전 `Read ProjectSettings/ProjectSettings.asset`에서 `activeInputHandler` 그레프.

### 5.1 Step 0 — 프로젝트 capability 확인

`mcpforunity://project/info` 또는 등가 read로 다음을 박제한다.

- `packages.uiToolkit` (true → UI Toolkit 사용 가능)
- `packages.ugui` (true → Canvas 사용 가능)
- `packages.textmeshpro` (true → `TextMeshProUGUI`, false → 레거시 `Text`)
- `activeInputHandler` ("Old" / "New" / "Both") → EventSystem 모듈 결정

### 5.2 UI Toolkit (UXML/USS)

- UXML 루트는 `<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">` 네임스페이스 헤더 포함.
- 모든 UI 요소는 `ui:` 접두 — `<ui:Label>`, `<ui:Button>`, `<ui:Style src="Assets/UI/Styles.uss" />` 등.
- **안티패턴.** 접두 없는 `<Label>`, `<Style>` — UI Builder 로드 실패.

### 5.3 uGUI (Canvas)

- Canvas + CanvasScaler + GraphicRaycaster 3종 한 GameObject에 부착.
- 자식 UI는 **반드시 `RectTransform`** — `Transform`이면 보이지 않음.
- 핵심 4 필드 누락 시 0 사이즈로 보이지 않음:
  - `m_AnchorMin`, `m_AnchorMax`
  - `m_SizeDelta`
  - `m_AnchoredPosition`

### 5.4 EventSystem 모듈 분기

| `activeInputHandler` | EventSystem 모듈 |
|---|---|
| `"Old"` (= `0`) | `StandaloneInputModule` |
| `"New"` (= `1`) | `InputSystemUIInputModule` |
| `"Both"` (= `2`) | 둘 중 하나 — sub-spec/ARD에서 결정 |

**안티패턴.** `activeInputHandler` 미확인 상태에서 임의로 `StandaloneInputModule` 부착 → New Input System 환경에서 입력 dead.

## 6. VR/리듬 도메인 가이드

본 프로젝트가 자주 건드리는 도메인만. ProBuilder/VFX/Shader/Cinemachine은 본 skill 범위 외.

### 6.1 Animation (손·악기)

- 손 애니메이션 클립/컨트롤러 조작은 `manage_animation` 또는 `manage_components`(Animator 필드 변경)로 처리.
- Animator override controller 변경은 직렬화 자산 변경이라 컴파일 의존이 없다 → `read_console`만으로 충분 (refresh_unity 불필요).
- **안티패턴.** Animator parameter를 `manage_components.set_property`로 잘못 건드림 — runtime parameter는 직렬화되지 않아 Edit 모드 변경이 무의미. Animator 자체의 `m_Controller` 같은 직렬화 필드만 변경 가능.

### 6.2 Camera (VR/메뉴)

- VR 카메라 추가/lens 변경은 `manage_camera` (현재 화이트리스트 외 → prompt 승인 필요).
- 변경 후 `screenshot(capture_source="game_view")` 1장 권장 — Edit 모드에서 GameView 미리보기로 의도 확인.
- HMD가 있으면 GameView 해상도가 비정상일 수 있으므로 screenshot 해석 시 주의.

### 6.3 Physics (드럼 스틱·악기 충돌)

- `manage_physics`로 충돌 매트릭스/Layer/Collider 일관 처리 (현재 화이트리스트 외 → prompt 승인 필요).
- **Trigger 동작 필수 조건.** `OnTriggerEnter`가 발화되려면 충돌 두 객체 중 **최소 한쪽이 Rigidbody**여야 한다. 드럼 스틱처럼 movable 객체에 `Rigidbody` 부착 (필요 시 `useGravity=false`, `isKinematic=true`).
- Collision matrix 변경은 ProjectSettings 직렬화 자산이라 plan 적용 후 `read_console`로 LayerMask 관련 warning 확인.

## 7. 안전 패턴 (precondition_sha256 / API 사전 검증)

### 7.1 precondition_sha256 (텍스트 편집 stale-file 방지)

`apply_text_edits` 또는 `script_apply_edits`로 텍스트를 정밀 편집할 때, 다중 에이전트/사용자 동시 편집으로 인한 stale-file 사고를 막는다.

표준 절차:

1. `Read <대상 파일>` 또는 `mcp__UnityMCP__get_sha(path=...)`로 현재 SHA 취득.
2. 편집 호출에 `precondition_sha256=<위 SHA>` 전달.
3. 호출이 SHA 불일치로 실패하면 → SHA 재취득 → 편집 내용 재계산(파일이 그 사이 바뀌었을 수 있음) → 재시도.

**안티패턴.** SHA 없이 편집 → 동시 편집 발생 시 silent overwrite. 특히 implementer가 파일을 수정하는 도중 사용자가 IDE에서 같은 파일을 저장한 경우.

### 7.2 API 사전 검증

새 컴포넌트의 public API를 호출하는 코드를 작성하기 전에:

- `mcp__UnityMCP__unity_reflect`로 시그니처/필드 확인 (현재 화이트리스트 외 → prompt 승인 필요), 또는
- `Read <패키지 소스 .cs>` 직접 — 본 프로젝트 planner 표준 절차. 라인 범위가 아닌 *동작 요약 리스트*로 박제.

**안티패턴.** 트레이닝 데이터 기억으로 API 시그니처 추측. Unity 6000.3 패키지가 메이저 변경된 경우 즉시 컴파일 에러.

## 8. Error Recovery 표

| 증상 | 원인 | 복구 절차 |
|---|---|---|
| 컴파일 에러 | 신택스/의미 오류 | `read_console(include_stacktrace=True)` → `find_in_file`로 라인 위치 → 수정 → `refresh_unity(mode="force", wait_for_ready=True)` → 재확인 |
| Stale file (SHA 불일치) | 동시 편집 또는 외부 변경 | SHA 재취득 → 편집 내용 재계산 → 재시도 (섹션 7.1) |
| 도메인 리로드 연결 손실 | 스크립트 컴파일 중 MCP 연결 끊김 | 2~5초 대기 → `editor/state` polling → `ready_for_tools=true`까지 exponential backoff (최대 5회). 그래도 실패하면 STOP. |
| "Already Exists" | 같은 이름의 GameObject/asset 존재 | `find_gameobjects(name=...)`로 충돌 확인 → 고유 이름 또는 기존 객체 삭제 후 재생성 |
| ProBuilder face index 변경 | 메시 편집 후 topology 재할당 | (본 skill 범위 외 — 사용 시 별도 가이드) |
| MCP 미가용 | 도구가 세션에 노출 안 됨 또는 Unity Editor 죽음 | 즉시 STOP. orchestrator/메인에 보고. Edit 우회 시도 금지 (AGENTS.md "Unity MCP 사용 정책"). |
| YAML 헤더 손상 | 잘못된 텍스트 Edit | 추가 변경 중단. 메인에 보고. `unity-asset-edit` SKILL의 "에디터 로드 실패 시 포맷 복구" 절차 적용. |

## 9. MCP 미가용 시 fallback (재명시)

**핵심.** AGENTS.md "Unity MCP 사용 정책"이 단일 진실원이다 — Unity MCP 도구가 세션에 노출되어 있지 않으면 작업을 중단하고 사용자에게 "MCP 없이 진행할지" 묻는다.

상위 에이전트별 보고 의무:

- **implementer.** MCP 끊김/실패 → `mcp_unavailable` 보고 후 STOP. Edit fallback 시도 금지. 단독 판단으로 `UNITY_YAML_OVERRIDE` 설정 금지.
- **unity-scene-writer.** MCP 미가용 → "변경 적용 불가" 보고 후 멈춘다. 추측·우회 금지.
- **unity-scene-reader.** MCP 미가용 → `Read+Grep`으로 수집 가능한 직렬화 사실만 보고 + "MCP 미가용으로 X/Y 정보 미확인" 한계 명시. 가정으로 채우지 않는다.

## 10. 작업 직전 체크리스트

Unity MCP 호출을 시작하기 직전에 1회 확인:

- [ ] `editor/state.is_compiling == false` (컴파일 중이면 대기)
- [ ] 작업 대상 GameObject/asset의 정확한 이름·경로 확보 (`find_gameobjects` 또는 reader 위임)
- [ ] UI 작업이면 `project/info`의 패키지·activeInputHandler 확인 (섹션 5.1)
- [ ] 스크립트 변경이면 컴파일 대기 + `read_console` 통과 후에만 attach (섹션 2)
- [ ] 변경 후 `read_console(types=["error","warning"])` 검증 계획 (섹션 3)
- [ ] 시각적 변화가 핵심이면 screenshot 1장 계획 (섹션 3.2)
- [ ] MCP 미가용 시 STOP·보고 의무 인지 (섹션 9)
