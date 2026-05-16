---
name: unity-mcp-workflow
description: Unity MCP 도구로 스크립트·씬·컴포넌트·프리팹을 생성·수정하기 직전에 사용한다. Resource-First 사전 점검, 스크립트 컴파일 대기, 변경 후 `read_console`·screenshot 검증, `batch_execute` 의존성 처리, `precondition_sha256` stale-file 방지, 도메인 리로드·"Already Exists"·컴파일 블록 같은 error recovery 절차를 안내한다. implementer·unity-scene-writer·unity-scene-reader가 자동 invoke한다.
allowed-tools: Read, Edit, Glob, Grep, Bash, mcp__UnityMCP__read_console, mcp__UnityMCP__refresh_unity, mcp__UnityMCP__find_gameobjects, mcp__UnityMCP__manage_asset, mcp__UnityMCP__manage_components, mcp__UnityMCP__manage_gameobject, mcp__UnityMCP__manage_scene, mcp__UnityMCP__manage_prefabs, mcp__UnityMCP__manage_script
---

# Unity MCP 워크플로우 규칙

본 skill은 절차만 제공한다. 자산 수정 안전 절차는 [`unity-asset-edit`](../unity-asset-edit/SKILL.md), MCP 미가용 정책은 [`CLAUDE.md`](../../../CLAUDE.md) "Unity MCP 사용 정책"이 단일 진실원이다. 다른 글로벌 skill을 호출하지 않는다.

## 1. 사전 점검 (Resource-First)

첫 MCP 호출 직전에 다음 1~3종을 1회 읽고, plan/변경에 영향이 있으면 보고·박제한다.

| 리소스 | 확인 항목 | 영향 |
|---|---|---|
| `mcpforunity://editor/state` | `is_compiling`, `ready_for_tools`, play mode | 컴파일 중엔 새 타입 attach 실패. play mode면 다수 변경이 임시값. |
| `mcpforunity://project/info` | 패키지 가용성, 렌더 파이프라인 | 머티리얼/볼륨 분기·자산 capability. |
| `find_gameobjects` / `scene/gameobject/{id}/components` | 대상 존재·부모/자식·부착 컴포넌트 | "Already Exists" 회피·동명 분기·override 의도. |

리소스 직접 read 불가 시 등가 도구: `editor/state` → `read_console(types=["error","warning"], count=5)` + 컴파일 폴 / `project/info` → `Read Packages/manifest.json` / 씬·프리팹 → `unity-scene-reader` Task 위임.

**안티패턴.** 리소스 미확인 상태에서 `manage_components(add)`·`manage_gameobject(create)` 직행 → 즉시 실패 또는 잘못된 인스턴스.

## 2. 스크립트 변경 워크플로우

`.cs` 생성/수정 후엔 **반드시** 컴파일 통과 확인 후에만 새 타입 사용.

1. `manage_script(create|apply_edits)` 또는 `Edit`/`Write`로 `.cs` 변경.
2. `refresh_unity(wait_for_ready=True)` — import + 컴파일 + 도메인 리로드까지 대기. 미지원 시 `read_console` 30초 1초 폴링.
3. `read_console(types=["error"], count=20, include_stacktrace=True)` 통과 (error 0건).
4. 위 통과 전엔 새 타입 `manage_components(add)` 금지. attach는 별도 호출.

**안티패턴.** ①같은 `batch_execute`에 `manage_script(create) + 새 타입 attach`. ②`refresh_unity` 매번 호출 (도메인 리로드 중복으로 연결 손실). ③컴파일 후 `read_console` 생략.

## 3. 변경 후 검증

### 3.1 콘솔 검증
`read_console(types=["error","warning"], count=10)` — 새 error/warning 없음 확인. warning은 reviewer 점검 대상.

### 3.2 시각 검증 (선택)
GameObject 배치·머티리얼·UI 같이 **시각이 핵심**인 변경엔 screenshot 1장. `manage_camera(action="screenshot", capture_source="game_view", view_target="<이름>", batch="single", width=512, include_image=True)`. 256–512px로 token 절약. 파일 저장 목적이면 `include_image=False`. `manage_camera`는 화이트리스트 외라 prompt 승인 필요.

### 3.3 자산 사실 위임
prefab 계층·직렬화 raw·PrefabInstance override 검증은 `unity-scene-reader` Task. 메인이 raw YAML을 직접 읽지 않는다.

**안티패턴.** fire-and-forget — 변경만 하고 검증 없이 다음 단계.

## 4. batch_execute

여러 MCP 호출을 한 명령에 묶는다. 화이트리스트 외라 prompt 승인 필요.

**사용 시점 (의무 포함).** 독립 작업 ≥ 2개 / 같은 도구 반복 / 여러 `find_gameobjects` 발견 단계. **동일 plan 안에서 같은 도구를 3회 이상 반복하면 batch로 묶는다** — 묶지 않은 4회째 호출은 reviewer가 `needs-fix`. `find_gameobjects`류 read는 2회까지 허용.

| 상황 | `fail_fast` |
|---|---|
| 의존 시퀀스 (생성 → 부착) | `True` |
| 독립 변경 (5개 일괄 인스턴스화) | `False` |

**분할 한도.** ≤ 25개 (서버 기본). `parallel=True`는 *조언* — Unity 메인 스레드 의존 작업은 순차 실행될 수 있다.

**안티패턴.** ①`manage_script(create) + 새 타입 attach` 같은 batch. ②25개 초과. ③의존 시퀀스인데 `fail_fast=False`.

## 5. precondition_sha256 (stale-file 방지)

`apply_text_edits`/`script_apply_edits` 정밀 편집 시 동시 편집 사고 방지.

1. `Read <파일>` 또는 `get_sha(path=...)`로 현재 SHA 취득.
2. 편집 호출에 `precondition_sha256=<SHA>` 전달.
3. SHA 불일치 → SHA 재취득 → 편집 재계산 → 재시도.

**안티패턴.** SHA 없이 편집 → 동시 편집 시 silent overwrite.

## 6. Error Recovery

| 증상 | 복구 |
|---|---|
| 컴파일 에러 | `read_console(include_stacktrace=True)` → `find_in_file` 위치 → 수정 → `refresh_unity(mode="force", wait_for_ready=True)` → 재확인 |
| Stale file (SHA 불일치) | SHA 재취득 → 편집 재계산 → 재시도 (§5) |
| 도메인 리로드 연결 손실 | 2~5초 대기 → `editor/state` polling → `ready_for_tools=true`까지 exponential backoff (최대 5회). 실패 시 STOP |
| "Already Exists" | `find_gameobjects(name=...)`로 충돌 확인 → 고유 이름 또는 기존 객체 삭제 후 재생성 |
| MCP 미가용 | 즉시 STOP, 메인에 보고. Edit 우회 금지 (CLAUDE.md 정책) |
| YAML 헤더 손상 | 추가 변경 중단. 메인 보고. `unity-asset-edit` "포맷 복구" 절차 |

## 7. 작업 직전 체크리스트

- [ ] `editor/state.is_compiling == false`
- [ ] 대상 GameObject/asset 정확한 이름·경로 확보
- [ ] 스크립트 변경이면 컴파일 대기 + `read_console` 통과 후 attach (§2)
- [ ] 변경 후 `read_console` 검증 계획 (§3.1)
- [ ] 시각 핵심이면 screenshot 1장 계획 (§3.2)
- [ ] MCP 미가용 시 STOP·보고 의무 (CLAUDE.md 정책)
