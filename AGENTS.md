# VirtualMusicStudio Unity 에이전트 가이드

## 프로젝트
- 주요 씬: `Assets/Scenes/SampleScene.unity`
- Unity 6000.3.10f1 / URP / Unity MCP: `com.coplaydev.unity-mcp`

## 상시 규칙

- 기본적으로 한국어로 답한다.
- 새 코드는 `Assets/Hands/Scripts/`, `Assets/Instruments/_Core/Scripts/`처럼 **도메인 폴더 안의 `Scripts/` 서브폴더**에 런타임/에디터 로직을 C#으로 작성한다.
- 사용자가 더 풍부한 런타임 진단이나 디버깅 지원을 명시적으로 요청하지 않았다면 경고·오류·진단 상태 추적 로직을 추가하지 않는다.
- Windows 경로 조작은 PowerShell 문법을 따른다. 파일 검색·디렉토리 나열은 `Glob` 또는 `Grep` 도구를 우선 사용하고, `Bash`의 `find`/`ls`는 Windows 경로 escape 제약 때문에 폴백으로만 쓴다.

## Unity MCP 사용 정책
- Unity MCP로 더 효율적인 작업을 수행할 수 있지만, Unity MCP 도구가 세션에 노출되어 있지 않으면, **작업을 중단하고 사용자에게 "MCP 없이 진행할지" 묻는다.** 임의로 우회하거나 추측으로 진행하지 않는다.

### 직렬화 자산 수정 MCP 우선

`.prefab` / `.unity` / `.asset` / ScriptableObject 수정은 manage_* MCP 우선. 텍스트 직접 Edit은 plan 명시 또는 사용자 승인이 선행돼야 하며, **sub-agent 단독 판단 금지**. 결정 트리·예외 조건·YAML 보존 절차는 [`.claude/skills/unity-asset-edit/SKILL.md`](.claude/skills/unity-asset-edit/SKILL.md).

### Unity MCP 워크플로우

스크립트·씬·컴포넌트·프리팹·애니메이션·카메라·물리·UI를 Unity MCP로 변경할 때는 [`.claude/skills/unity-mcp-workflow/SKILL.md`](.claude/skills/unity-mcp-workflow/SKILL.md)을 호출해 사전 점검(Resource-First)·스크립트 컴파일 대기·`read_console`/screenshot 검증·`batch_execute` 의존성 처리·UI Toolkit/uGUI 분기·VR/리듬 도메인(Animation·Camera·Physics) 가이드·`precondition_sha256` stale-file 방지·error recovery 절차를 컨텍스트에 적재한다.

## 테스트 정책

- Unity 런타임 코드(`.cs`) 수정 후에는 `unity-test-runner` 서브에이전트를 1회 호출해 회귀를 확인한다.
- `unity-test-runner`는 **코드·자산을 절대 수정하지 않는다** — 검증 전용.
- plan-orchestrator는 `plan-implementer` 완료 직후, `plan-reviewer` 호출 전에 `unity-test-runner`를 호출한다.
- `unity-test-runner` FAIL → `next_action: test-failed`로 메인에 보고 후 대기. 자동 수정 시도 금지.
- MCP 미가용으로 테스트 실행 불가 시 → `MCP UNAVAILABLE` 리포트 후 plan-reviewer는 그대로 진행한다.

## Spec 시스템

Spec/plan 분리 구조, 파일명 규칙, `/spec-implement` 진입점(dry-run 기본, `--apply`로 실행), plan 실행 읽기 순서, 상태 보드는 [`docs/specs/README.md`](docs/specs/README.md)가 단일 진실원이다.
