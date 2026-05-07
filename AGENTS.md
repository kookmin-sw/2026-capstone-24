# VirtualMusicStudio Unity 에이전트 가이드

## 프로젝트
- 주요 씬: `Assets/Scenes/SampleScene.unity`
- Unity 6000.3.10f1 / URP / Unity MCP: `com.coplaydev.unity-mcp`

## 상시 규칙

- 기본적으로 한국어로 답한다.
- 새 코드는 `Assets/Hands/Scripts/`, `Assets/Instruments/_Core/Scripts/`처럼 **도메인 폴더 안의 `Scripts/` 서브폴더**에 런타임/에디터 로직을 C#으로 작성한다.
- 사용자가 더 풍부한 런타임 진단이나 디버깅 지원을 명시적으로 요청하지 않았다면 경고·오류·진단 상태 추적 로직을 추가하지 않는다.

## Unity MCP 사용 정책
- Unity MCP로 더 효율적인 작업을 수행할 수 있지만, Unity MCP 도구가 세션에 노출되어 있지 않으면, **작업을 중단하고 사용자에게 "MCP 없이 진행할지" 묻는다.** 임의로 우회하거나 추측으로 진행하지 않는다.

### 직렬화 자산 수정 MCP 우선

`.prefab` / `.unity` / `.asset` / ScriptableObject 수정은 manage_* MCP 우선. 텍스트 직접 Edit은 plan 명시 또는 사용자 승인이 선행돼야 하며, **sub-agent 단독 판단 금지**. 결정 트리·예외 조건·YAML 보존 절차는 [`.claude/skills/unity-asset-edit/SKILL.md`](.claude/skills/unity-asset-edit/SKILL.md).

## Spec 시스템

Spec/plan 분리 구조, 파일명 규칙, `/spec-implement` 진입점(dry-run 기본, `--apply`로 실행), plan 실행 읽기 순서, 상태 보드는 [`docs/specs/README.md`](docs/specs/README.md)가 단일 진실원이다.
