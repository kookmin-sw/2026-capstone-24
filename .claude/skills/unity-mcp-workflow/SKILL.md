---
name: unity-mcp-workflow
description: Unity MCP 도구로 스크립트·씬·컴포넌트·프리팹·직렬화 자산(.prefab/.unity/.asset/ScriptableObject)을 수정할 때 참조한다. 컴파일 대기·`batch_execute` 의무 룰·`precondition_sha256` stale-file 방지·error recovery·자산 수정 결정 트리·FBX 언팩·enum 함정의 단일 진실원. 자동 invoke 의무는 없으며, 호출 측이 필요한 §만 참조한다.
allowed-tools: Read, Edit, Glob, Grep, Bash, mcp__UnityMCP__read_console, mcp__UnityMCP__refresh_unity, mcp__UnityMCP__find_gameobjects, mcp__UnityMCP__manage_asset, mcp__UnityMCP__manage_material, mcp__UnityMCP__manage_components, mcp__UnityMCP__manage_gameobject, mcp__UnityMCP__manage_scene, mcp__UnityMCP__manage_prefabs, mcp__UnityMCP__manage_script
---

## 1. 스크립트 변경 워크플로우

`.cs` 생성/수정 후엔 **반드시** 컴파일 통과 확인 후에만 새 타입 사용.

1. `manage_script(create|apply_edits)` 또는 `Edit`/`Write`로 `.cs` 변경.
2. `refresh_unity(wait_for_ready=True)` — import + 컴파일 + 도메인 리로드까지 대기.
3. `read_console(types=["error"], count=20, include_stacktrace=True)` 통과 (error 0건).
4. 위 통과 전엔 새 타입 `manage_components(add)` 금지. attach는 별도 호출.

**안티패턴.** ①같은 `batch_execute`에 `manage_script(create) + 새 타입 attach`. ②`refresh_unity` 매번 호출 (도메인 리로드 중복). ③컴파일 후 `read_console` 생략.

## 2. batch_execute 의무 룰

| 상황 | `fail_fast` |
|---|---|
| 의존 시퀀스 (생성 → 부착) | `True` |
| 독립 변경 (5개 일괄 인스턴스화) | `False` |

**동일 plan 안에서 같은 도구를 3회 이상 반복하면 batch로 묶는다** — 묶지 않은 4회째 호출은 reviewer가 `needs-fix`. `find_gameobjects`류 read는 2회까지 허용. 분할 한도 ≤ 25개 (서버 기본). `parallel=True`는 *조언* — Unity 메인 스레드 의존 작업은 순차 실행될 수 있다. `manage_camera`/`batch_execute` 등은 화이트리스트 외라 prompt 승인 필요.

**안티패턴.** ①`manage_script(create) + 새 타입 attach` 같은 batch. ②25개 초과. ③의존 시퀀스인데 `fail_fast=False`.

## 3. precondition_sha256 (stale-file 방지)

`apply_text_edits`/`script_apply_edits` 정밀 편집 시 동시 편집 사고 방지.

1. `Read <파일>` 또는 `get_sha(path=...)`로 현재 SHA 취득.
2. 편집 호출에 `precondition_sha256=<SHA>` 전달.
3. SHA 불일치 → SHA 재취득 → 편집 재계산 → 재시도.

**안티패턴.** SHA 없이 편집 → 동시 편집 시 silent overwrite.

## 4. Error Recovery

| 증상 | 복구 |
|---|---|
| 컴파일 에러 | `read_console(include_stacktrace=True)` → `find_in_file` 위치 → 수정 → `refresh_unity(mode="force", wait_for_ready=True)` → 재확인 |
| Stale file (SHA 불일치) | SHA 재취득 → 편집 재계산 → 재시도 (§3) |
| 도메인 리로드 연결 손실 | 2~5초 대기 → `editor/state` polling → `ready_for_tools=true`까지 exponential backoff (최대 5회). 실패 시 STOP |
| "Already Exists" | `find_gameobjects(name=...)`로 충돌 확인 → 고유 이름 또는 기존 객체 삭제 후 재생성 |
| MCP 미가용 | 즉시 STOP, 메인에 보고. Edit 우회 금지. |
| YAML 헤더 손상 | 추가 변경 중단. 메인 보고. §6 일반 규칙의 포맷 복구 절차. |

## 5. 도구 선택 결정 트리 (직렬화 자산)

`.prefab`/`.unity`/`.asset`/ScriptableObject 등 Unity 직렬화 자산 수정·생성 시.

1. **자산 *생성*** (`.mat`, `.asset`, `.prefab` 신규) → `manage_material` / `manage_asset` / `manage_prefabs`. YAML hand-write 금지. `.meta` GUID는 Unity가 만들도록 한다.
2. **단일 필드 *변경*** (머터리얼 교체, `m_Enabled`, 컴포넌트 값 등) → `manage_components` / `manage_prefabs` 우선. MCP가 그 필드를 못 다룰 때만 텍스트 Edit으로 폴백.
3. **스칼라 텍스트 Edit이 불가피한 경우** → §6 일반 규칙의 YAML 보존 룰을 따른다. `precondition_sha256` 절차는 §3.
4. **MCP 도구가 세션에 노출돼 있지 않으면** 사용자에게 묻고 진행한다.
5. **`.cs` 스크립트 변경**은 §1 (컴파일 대기·`read_console` 검증·attach 순서).

## 6. 자산 수정 일반 규칙

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
- **plan에 명시된 사실(파일 경로, GUID, 씬 경로, 식별자명)은 load-bearing이고 동시에 drift 가능성이 있을 때만 재검증한다.** `planner` sub-agent는 self-contained plan을 보장한다 — 모든 사실을 grep으로 재확인하는 것은 plan을 신뢰하지 않는다는 신호다.
- 텍스트 수정이 불가피한 경우에도 파일 전체 재작성은 금지하고, 필요한 줄만 최소 범위로 수정한다.
- 텍스트 수정 시 Unity YAML 헤더, 문서 구분자, 직렬화 구조를 보존해야 하며 포맷을 임의로 재조합하지 않는다.
- 텍스트 수정 직후에는 Unity가 자산을 다시 읽을 수 있는지 반드시 확인한다.
- 에디터가 로드 실패, 포맷 오류, 자산 인식 실패를 내면 추가 수정 전에 포맷 복구와 자산 인식 복구를 먼저 수행한다.
- 에디터 스크립트에서 `AssetDatabase.FindAssets`로 씬·프리팹을 검색할 때는 반드시 `new[] { "Assets" }`를 두 번째 인수로 전달한다. 인수를 생략하면 `Packages/` 경로까지 포함되어 패키지 씬을 열려다 예외가 발생한다.

## 7. FBX / PrefabInstance 언팩 절차

`.fbx` 모델 또는 다른 prefab의 PrefabInstance에서 자식 GameObject를 새 prefab으로 추출할 때(실제 사례: 2026-05-14 Trombone.prefab 추출 — fbx PrefabInstance를 언팩 없이 raw YAML로 직렬화하다 fileID `100100000` sentinel 충돌로 씬 PPtr cast 깨짐. `docs/specs/_archive/trombone/plans/2026-05-14-sanyoentertain-trombone-prefab-extraction.md` 진단).

1. **언팩 선행**: `manage_prefabs unpack_completely`로 원본 PrefabInstance를 완전 언팩한 뒤에 새 prefab을 만든다. 언팩 없이 raw YAML로 prefab을 직접 작성하면 fileID가 Unity 내부 sentinel 범위(`100100000`, `200100000` 등)와 충돌해 PPtr cast가 깨진다.
2. **새 prefab 생성**: 언팩된 GameObject에 대해 `manage_prefabs create` 사용. 자동 발급되는 fileID는 11자리 이상 random 값이며 sentinel 범위와 겹치지 않는다.
3. **씬 인스턴스화 검증 AC 의무**: 새 prefab을 만든 plan은 "씬에 PrefabUtility로 인스턴스화 시 콘솔 에러 0" AC 1건을 `[auto-hard]`로 둔다. `docs/specs/README.md` "작성 규칙 요약"의 직렬화 정합성 AC 룰과 일치.
4. **금지 패턴**:
   - 언팩 없이 `Edit` 도구로 fbx 산하 GameObject의 fileID 직접 재작성
   - `100100000` / `200100000` 계열 fileID를 새 prefab에 사용
   - `manage_prefabs open_prefab_stage` → `modify_contents` → `save_prefab_stage` 시퀀스 사이에 다른 자산 수정 끼워넣기
5. **사고 발생 시 복구**: 이미 sentinel fileID로 prefab이 만들어졌다면 `open_prefab_stage` → `save_prefab_stage` 한 번이면 Unity가 fileID를 재발급한다. 씬 인스턴스의 PrefabInstance override target 매핑은 별도 재바인딩 필요.

## 8. enum 필드 매핑 함정

`manage_components`/`manage_gameobject`로 컴포넌트의 enum 또는 Flags 필드를 셋업할 때 인덱스 매핑이 인스펙터 표기와 어긋나는 경우가 있다. 직렬화는 통과하지만 동작이 정반대가 되는 사고를 일으킨다 (실제 사례: `TeleportationArea.m_TeleportTrigger`가 `OnSelectExited`(0) 의도였으나 `OnSelectEntered`(1)로 박혀 push 시 즉시 텔레포트 발동. base plan 검증 통과 후 manual-hard에서야 잡힘 — `docs/specs/_archive/teleport-locomotion/plans/2026-04-30-sanyoentertain-fix-push-immediate-teleport-trigger.md` 진단).

다음 두 단계로 함정을 차단한다.

1. **plan 작성 단계.** plan `## Verified Structural Assumptions`에 enum 정의(클래스명·각 값 인덱스)와 본 plan 의도 값을 박제한다. 출처는 패키지 소스 `Read <패키지 경로>/<파일>.cs`. 강제 룰 단일 진실원: `docs/specs/README.md` "작성 규칙 요약".
2. **자산 적용 직후.** MCP 호출 결과를 직렬화 `Grep`으로 다시 읽어 의도 값과 일치하는지 대조한다. 어긋났으면 단일 propertyPath 스칼라 변경이라 직접 텍스트 Edit 예외로 우회 가능 — sub-agent 단독 판단 금지, plan 명시 또는 메인 승인 후에만.

AC는 의도 값 단일 매치 grep을 `[auto-hard]`로 둔다 (예: "`Plane TeleportationArea` 부착 + `m_TeleportTrigger == 0`을 grep 단일 매치"). AC 라벨/문구 가이드는 `docs/specs/README.md` "작성 규칙 요약"이 단일 진실원.
