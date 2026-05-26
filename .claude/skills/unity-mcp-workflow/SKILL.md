---
name: unity-mcp-workflow
description: Unity MCP 도구로 스크립트·씬·컴포넌트·프리팹·직렬화 자산(.prefab/.unity/.asset/ScriptableObject)을 수정/Read 해야 할 때 참조한다.
allowed-tools: Read, Edit, Glob, Grep, Bash, mcp__UnityMCP__read_console, mcp__UnityMCP__refresh_unity, mcp__UnityMCP__find_gameobjects, mcp__UnityMCP__manage_asset, mcp__UnityMCP__manage_material, mcp__UnityMCP__manage_components, mcp__UnityMCP__manage_gameobject, mcp__UnityMCP__manage_scene, mcp__UnityMCP__manage_prefabs, mcp__UnityMCP__manage_script
---

## 1. 스크립트 변경 워크플로우

방금 생성/수정한 클래스·MonoBehaviour 등을 다른 도구(`manage_components(add)` 등)에서 참조하기 전에 컴파일이 통과돼야 한다.

1. `manage_script(create|apply_edits)` 또는 `Edit`/`Write`로 `.cs` 변경.
2. `refresh_unity(wait_for_ready=True)` — import + 컴파일 + 도메인 리로드까지 대기.
3. `read_console(types=["error"], count=20, include_stacktrace=True)` 통과(error 0건) 확인 후에만 attach.

## 2. batch_execute 의무 룰

| 상황 | `fail_fast` |
|---|---|
| 의존 시퀀스 (생성 → 부착) | `True` |
| 독립 변경 (5개 일괄 인스턴스화) | `False` |

동일 절차 안에서 같은 도구를 3회 이상 반복하면 batch로 묶는다. `parallel=True`는 *조언* — Unity 메인 스레드 의존 작업은 순차 실행될 수 있다.

## 3. precondition_sha256 (stale-file 방지)

`apply_text_edits`/`script_apply_edits` 정밀 편집 시 동시 편집 사고 방지.

1. `Read <파일>` 또는 `get_sha(path=...)`로 현재 SHA 취득.
2. 편집 호출에 `precondition_sha256=<SHA>` 전달.
3. SHA 불일치 → SHA 재취득 → 편집 재계산 → 재시도.

## 4. Error Recovery

| 증상 | 복구 |
|---|---|
| 컴파일 에러 | `read_console(include_stacktrace=True)` → `find_in_file` 위치 → 수정 → `refresh_unity(mode="force", wait_for_ready=True)` → 재확인 |
| Stale file (SHA 불일치) | SHA 재취득 → 편집 재계산 → 재시도 (§3) |
| 도메인 리로드 연결 손실 | `editor/state` ready 상태까지 polling. 실패 시 STOP |
| "Already Exists" | `find_gameobjects(name=...)`로 충돌 확인 → 고유 이름 또는 기존 객체 삭제 후 재생성 |
| MCP 미가용 | 즉시 STOP, 메인에 보고. Edit 우회 금지 |
| YAML 헤더 손상 | 추가 변경 중단, 메인 보고. §6 포맷 복구 절차 |

## 5. 도구 선택 결정 트리 (수정·조회 양방향)

`.prefab`/`.unity`/`.asset`/ScriptableObject 등 Unity 직렬화 자산을 다룰 때.

1. **자산 *조회*** (raw YAML 파싱 충동이 들 때) → `manage_prefabs(get_contents)` / `find_gameobjects` / `manage_components(get)` / `manage_scene(get_info)` 등 구조화 MCP가 우선. `Grep`/`Read`는 텍스트 패턴이 명확하거나 GUID 추적 등 raw 파일 단위 작업일 때.
2. **자산 *생성*** (`.mat`, `.asset`, `.prefab` 신규) → `manage_material`/`manage_asset`/`manage_prefabs`. YAML hand-write 금지. `.meta` GUID는 Unity가 만들도록 한다.
3. **단일 필드 *변경*** (머터리얼 교체, `m_Enabled`, 컴포넌트 값 등) → `manage_components` / `manage_prefabs` 우선. MCP가 그 필드를 못 다룰 때만 텍스트 Edit으로 폴백.
4. **스칼라 텍스트 Edit이 불가피한 경우** → §6 YAML 보존 룰 + §3 SHA 절차.
5. **`.cs` 스크립트 변경** → §1.
6. **MCP 도구가 세션에 노출돼 있지 않으면** 사용자에게 묻고 진행.

## 6. 자산 수정/조회 일반 규칙

- 수정/조회 전 `prefab asset` · `scene instance` · `둘 다` 중 무엇인지 잠그고, 동명 인스턴스가 여러 개면 전체 경로로 확정한다.
- 수정 직전 현재 파일·Unity 상태를 다시 read, 수정 후 핵심 필드가 실제로 반영됐는지 다시 확인.
- **500줄 이상 직렬화 자산은 full Read 금지** — `Grep`/`find_gameobjects`로 위치 먼저 찾고, 필요한 영역만 `Read offset/limit`로 ~30줄 윈도. 전체 구조 파악은 `Explore` subagent에 위임(메인 컨텍스트로 끌어오지 않는다).
- 텍스트 수정은 파일 전체 재작성 금지, 필요한 줄만 최소 범위. Unity YAML 헤더·문서 구분자·직렬화 구조 보존, 포맷 임의 재조합 금지.
- 텍스트 수정 직후 Unity가 자산을 다시 읽을 수 있는지 확인. 로드 실패·포맷 오류 시 추가 수정 전에 포맷 복구를 먼저 한다.
- 에디터 스크립트에서 `AssetDatabase.FindAssets`로 씬·프리팹 검색 시 반드시 `new[] { "Assets" }`를 두 번째 인수로 전달(생략 시 `Packages/`까지 포함되어 예외).

## 7. FBX / PrefabInstance 언팩 절차

1. **언팩 선행**: `manage_prefabs unpack_completely`로 원본 PrefabInstance를 완전 언팩한 뒤에 새 prefab을 만든다. 언팩 없이 raw YAML로 prefab을 직접 작성하면 fileID가 Unity 내부 sentinel 범위(`100100000`, `200100000` 등)와 충돌해 PPtr cast가 깨진다.
2. **새 prefab 생성**: 언팩된 GameObject에 대해 `manage_prefabs create`. 자동 발급 fileID는 sentinel 범위와 겹치지 않는다.
3. **금지 패턴**: ①언팩 없이 fbx 산하 GameObject의 fileID 직접 재작성. ②`100100000`/`200100000` 계열 fileID를 새 prefab에 사용. ③`open_prefab_stage` → `modify_contents` → `save_prefab_stage` 사이에 다른 자산 수정 끼워넣기.
4. **사고 복구**: 이미 sentinel fileID로 prefab이 만들어졌다면 `open_prefab_stage` → `save_prefab_stage` 한 번이면 Unity가 fileID를 재발급한다.
