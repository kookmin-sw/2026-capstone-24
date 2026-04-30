---
name: unity-asset-edit
description: Unity 직렬화 자산(.prefab, .unity 씬, .asset, ScriptableObject 등)을 수정·생성하기 직전에 사용한다. 수정 범위 고정(prefab asset / scene instance / 둘 다), 동명 오브젝트 식별, prefab vs scene instance 값 차이 확인, 수정 전후 재확인, 큰 직렬화 자산을 grep·Read할 때의 컨텍스트 절약 패턴, YAML 헤더·구조 보존, AssetDatabase.FindAssets 인수 같은 안전 절차를 안내한다. 사용자가 프리팹/씬/asset 수정·생성·삭제, GameObject 컴포넌트 변경, MCP를 통한 씬 조작을 요청할 때 트리거한다.
allowed-tools: Read, Edit, Glob, Grep, mcp__unityMCP__manage_asset, mcp__unityMCP__manage_material, mcp__unityMCP__manage_prefabs, mcp__unityMCP__manage_components, mcp__unityMCP__manage_gameobject, mcp__unityMCP__manage_scene, mcp__unityMCP__find_gameobjects, mcp__unityMCP__refresh_unity, mcp__unityMCP__read_console
---

# Unity 직렬화 자산 수정 규칙

## 도구 선택 결정 트리

1. **자산 *생성*** (`.mat`, `.asset`, `.prefab` 신규) → `manage_material` / `manage_asset` / `manage_prefabs`. YAML hand-write 금지. `.meta` GUID는 Unity가 만들도록 한다.
2. **단일 필드 *변경*** (머터리얼 교체, `m_Enabled`, 컴포넌트 값 등) → `manage_components` / `manage_prefabs` 우선. MCP가 그 필드를 못 다룰 때만 텍스트 Edit으로 폴백.
3. **스칼라 텍스트 Edit이 불가피한 경우** → 아래 YAML 보존 규칙을 따른다.
4. **MCP 도구가 세션에 노출돼 있지 않으면** 사용자에게 묻고 진행한다 (AGENTS.md "Unity MCP 사용 정책"과 동일).

## 일반 규칙

- `.prefab`, `.unity`, `.asset` 같은 Unity 직렬화 자산은 기본적으로 Unity API, Unity MCP, 에디터 기능을 통한 수정이 우선이다.
- 직렬화 자산을 수정하기 전에는 대상 범위를 먼저 잠근다. `prefab 자산`, `scene instance`, `둘 다` 중 무엇을 수정하는지 먼저 명시한다.
- 같은 이름의 오브젝트나 인스턴스가 여러 개 있으면 이름만으로 추정하지 말고 전체 경로와 부모 오브젝트까지 확인한 뒤 대상을 고정한다.
- `scene instance`와 `prefab asset` 값은 다를 수 있으므로, 수정 전 두 대상 중 무엇이 소스 오브 트루스인지 먼저 확인한다.
- 수정 직전에는 현재 파일 상태와 Unity 쪽 현재 상태를 다시 읽어, 사용자나 Unity가 그 사이에 바꾼 내용이 없는지 재확인한다.
- Unity 직렬화 자산을 수정한 뒤에는, 저장된 prefab/scene을 다시 열어 이번 작업의 핵심 필드 값이 실제로 반영됐는지 반드시 확인한다.
- **500줄 이상의 직렬화 자산은 full Read 금지**. 다음 패턴을 따른다:
  1. 먼저 `Grep`(또는 `find_gameobjects` MCP)로 대상 라인/오브젝트 위치를 찾는다.
  2. 필요한 영역만 `Read offset/limit`로 ~30줄 윈도로 읽는다.
  3. 전체 구조 파악이 진짜 필요하면 `Explore` subagent에게 위임한다 (메인 컨텍스트로 끌어오지 않는다).
- **plan에 명시된 사실(파일 경로, GUID, 씬 경로, 식별자명)은 load-bearing이고 동시에 drift 가능성이 있을 때만 재검증한다.** plan-new skill은 self-contained plan을 보장한다 — 모든 사실을 grep으로 재확인하는 것은 plan을 신뢰하지 않는다는 신호다.
- 텍스트 수정이 불가피한 경우에도 파일 전체 재작성은 금지하고, 필요한 줄만 최소 범위로 수정한다.
- 텍스트 수정 시 Unity YAML 헤더, 문서 구분자, 직렬화 구조를 보존해야 하며 포맷을 임의로 재조합하지 않는다.
- 텍스트 수정 직후에는 Unity가 자산을 다시 읽을 수 있는지 반드시 확인한다.
- 에디터가 로드 실패, 포맷 오류, 자산 인식 실패를 내면 추가 수정 전에 포맷 복구와 자산 인식 복구를 먼저 수행한다.
- 에디터 스크립트에서 `AssetDatabase.FindAssets`로 씬·프리팹을 검색할 때는 반드시 `new[] { "Assets" }`를 두 번째 인수로 전달한다. 인수를 생략하면 `Packages/` 경로까지 포함되어 패키지 씬을 열려다 예외가 발생한다.

## enum 필드 매핑 함정

`manage_components`/`manage_gameobject`로 컴포넌트의 enum 또는 Flags 필드를 셋업할 때 인덱스 매핑이 인스펙터 표기와 어긋나는 경우가 있다. 직렬화는 통과하지만 동작이 정반대가 되는 사고를 일으킨다 (실제 사례: `TeleportationArea.m_TeleportTrigger`가 `OnSelectExited`(0) 의도였으나 `OnSelectEntered`(1)로 박혀 push 시 즉시 텔레포트 발동. base plan 검증 통과 후 manual-hard에서야 잡힘 — `docs/specs/_archive/teleport-locomotion/plans/2026-04-30-sanyoentertain-fix-push-immediate-teleport-trigger.md` 진단).

다음 두 단계로 함정을 차단한다.

1. **plan 작성 단계.** plan `## Verified Structural Assumptions`에 enum 정의(클래스명·각 값 인덱스)와 본 plan 의도 값을 박제한다. 출처는 패키지 소스 `Read <패키지 경로>/<파일>.cs`. 강제 룰 단일 진실원: `docs/specs/README.md` "작성 규칙 요약" + `/plan-new` step 1.5 Trigger (e).
2. **자산 적용 직후.** MCP 호출 결과를 직렬화 `Grep`으로 다시 읽어 의도 값과 일치하는지 대조한다. 어긋났으면 단일 propertyPath 스칼라 변경이라 직접 텍스트 Edit 예외(`AGENTS.md` 직렬화 자산 수정 MCP 우선 정책의 (b) 조건)로 우회 가능 — sub-agent 단독 판단 금지, plan 명시 또는 메인 승인 후에만.

AC는 의도 값 단일 매치 grep을 `[auto-hard]`로 둔다 (예: "`Plane TeleportationArea` 부착 + `m_TeleportTrigger == 0`을 grep 단일 매치"). AC 라벨/문구 가이드는 `docs/specs/README.md` "작성 규칙 요약"이 단일 진실원.
