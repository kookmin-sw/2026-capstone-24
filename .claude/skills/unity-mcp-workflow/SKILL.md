---
name: unity-mcp-workflow
description: Unity MCP 도구로 스크립트·씬·컴포넌트·프리팹을 수정할 때 참조한다. 컴파일 대기·`batch_execute` 의무 룰·`precondition_sha256` stale-file 방지·error recovery 절차의 단일 진실원. 자동 invoke 의무는 없으며, 호출 측이 필요할 때만 참조한다.
allowed-tools: Read, Edit, Glob, Grep, Bash, mcp__UnityMCP__read_console, mcp__UnityMCP__refresh_unity, mcp__UnityMCP__find_gameobjects, mcp__UnityMCP__manage_asset, mcp__UnityMCP__manage_components, mcp__UnityMCP__manage_gameobject, mcp__UnityMCP__manage_scene, mcp__UnityMCP__manage_prefabs, mcp__UnityMCP__manage_script
---

# Unity MCP 워크플로우 규칙

본 skill은 절차의 단일 진실원만 제공한다. 자산 수정 안전 절차는 [`unity-asset-edit`](../unity-asset-edit/SKILL.md), MCP 미가용 정책은 [`CLAUDE.md`](../../../CLAUDE.md). 호출 측이 자동 invoke하지 않으며, 필요한 섹션만 참조한다.

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
| MCP 미가용 | 즉시 STOP, 메인에 보고. Edit 우회 금지 (CLAUDE.md 정책) |
| YAML 헤더 손상 | 추가 변경 중단. 메인 보고. `unity-asset-edit` "포맷 복구" 절차 |
