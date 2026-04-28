# VirtualMusicStudio Unity 에이전트 가이드

## 프로젝트
- 주요 씬: `Assets/Scenes/SampleScene.unity`
- Unity 6000.3.10f1 / URP / Unity MCP: `com.coplaydev.unity-mcp`

## 상시 규칙

- 기본적으로 한국어로 답한다.
- 새 코드는 `Assets/Hands/Scripts/`, `Assets/Instruments/_Core/Scripts/`처럼 **도메인 폴더 안의 `Scripts/` 서브폴더**에 런타임/에디터 로직을 C#으로 작성한다.
- 사용자가 더 풍부한 런타임 진단이나 디버깅 지원을 명시적으로 요청하지 않았다면 경고·오류·진단 상태 추적 로직을 추가하지 않는다.

## Unity MCP 사용 정책

- **매 턴 MCP 연결을 확인하지 않는다.** 마크다운·C# 스크립트·프리팹/씬 YAML을 단순히 *읽기만* 하거나 grep/glob/git 작업만 한다면 MCP는 필요 없다. 직렬화 자산도 디스크 텍스트가 진실원이다.
- 다음 작업이 **실제로 필요해지는 시점**에서만 Unity MCP를 시도한다:
  - 씬·GameObject의 **에디터 시점 상태**(prefab override 해석, `SetActive`, 컴포넌트 값) 확인
  - 자산 수정 직후 AssetDatabase 동기화·컴파일 에러·콘솔 출력 확인
  - Play 모드 동작 검증
- 위 작업이 필요한데 Unity MCP 도구가 세션에 노출되어 있지 않으면, **작업을 중단하고 사용자에게 "MCP 없이 진행할지" 묻는다.** 임의로 우회하거나 추측으로 진행하지 않는다.

## Spec 시스템

What/Why를 담는 얇은 **spec**과 How를 담는 실행 가능한 **plan**을 분리해 관리한다. 폴더 구조, 파일명 규칙(sub-spec NN-prefix, plan `<YYYY-MM-DD>-<author>-<slug>`), Plan 실행 시 읽기 순서, 작성 anti-pattern, 상태 보드는 모두 [`docs/specs/README.md`](docs/specs/README.md)에 단일 진실원으로 관리한다.

사용자가 **저장된 plan대로 구현해달라고 요청**하면, 먼저 해당 plan 파일을 읽고 → `Linked Spec` → parent `_index.md` 순으로 컨텍스트를 적재한 뒤 작업을 진행한다.
