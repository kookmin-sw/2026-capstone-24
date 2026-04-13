# VirtualMusicStudio Unity 에이전트 가이드

> 이 파일은 코딩 에이전트가 공유하는 프로젝트 지침의 기준 문서입니다. 도메인별 세부 규칙은 아래 표의 룰 파일에 있습니다.

## 프로젝트 요약
- VR/XR 음악 스튜디오 프로토타입을 위한 Unity 프로젝트입니다.
- 현재 스택은 Universal Render Pipeline, Input System, OpenXR, XR Hands, XR Interaction Toolkit을 중심으로 구성되어 있습니다.
- 주요 작업 씬은 `Assets/Scenes/SampleScene.unity`입니다.
- 현재 프로젝트는 대부분 Unity 샘플 에셋과 프리팹으로 구성되어 있으며, 커스텀 게임플레이 코드는 많지 않습니다.

## 프로젝트 구조
- `Assets/Characters`: VR 플레이어 캐릭터 도메인 (Prefabs/).
- `Assets/Hands`: 손 도메인 (Editor/, Materials/, Models/, Prefabs/, Scripts/).
- `Assets/Instruments`: 악기 도메인. `_Core/`(공유 인프라), `Piano/`, `Drum/`, `Test/` 하위 도메인. 각 악기 폴더에 `Sound/` 서브폴더로 오디오 클립 포함.
- `Assets/Scenes`: 씬 에셋. `SampleScene.unity`가 유일한 빌드 씬.
- `Assets/Settings/Input`: Input System 액션 에셋.
- `Assets/Samples`: XR Hands, XR Interaction Toolkit 임포트된 샘플.
- `Assets/XR`, `Assets/XRI`: XR 관련 에셋 및 설정 (패키지 자동 생성).
- `Packages/manifest.json`: 패키지 의존성.
- `ProjectSettings/`: Unity 에디터, 렌더 파이프라인, 입력, XR 설정.
- `.claude/settings.local.json`: Claude 로컬 권한 설정.

## 환경 정보
- Unity 버전: `6000.3.10f1`.
- 렌더 파이프라인: URP.
- 입력 스택: `Assets/Settings/Input/InputSystem_Actions.inputactions`를 사용하는 Input System.
- XR 스택: OpenXR + XR Interaction Toolkit + XR Hands.
- 테스트 패키지는 `com.unity.test-framework`를 통해 설치되어 있습니다.
- Unity MCP는 `com.coplaydev.unity-mcp`를 통해 설치되어 있습니다.

## 상시 규칙
모든 작업에 공통으로 적용되는 원칙입니다.
- 동작을 변경하기 전에 현재 씬, 계층 구조, 패키지 구성을 먼저 확인합니다.
- 사용자 변경 사항을 기준으로 취급합니다. 작업 트리가 이미 더러워져 있을 수 있습니다.
- 사용자가 다른 언어를 명시적으로 요청하지 않는 한, 사용자 대상 계획, 설명, 요약은 기본적으로 한국어로 작성합니다.
- 가능한 가장 작은 변경을 수행합니다.
- 검증을 생략했거나 막혔다면 그 사실을 명시합니다.

## 작업 유형별 규칙 (선택 로드)
작업을 시작하기 전에 해당되는 규칙 파일을 추가로 읽으세요. 새 규칙은 이 표의 해당 룰 파일에 추가하고, AGENTS.md에는 중복해 적지 않습니다.

| 작업 유형 | 규칙 파일 | 읽어야 하는 때 |
|---|---|---|
| 씬/프리팹/에셋 수정 | [docs/agent-rules/scene-assets.md](docs/agent-rules/scene-assets.md) | `.unity`, `.prefab`, `.asset` 파일을 만질 때 |
| C# 스크립트 작성/수정 | [docs/agent-rules/coding.md](docs/agent-rules/coding.md) | `Assets/**/*.cs` 편집 시 |
| XR 리그·입력·손 상호작용 | [docs/agent-rules/xr.md](docs/agent-rules/xr.md) | XROrigin, XRI, OpenXR, XR Hands 관련 작업 시 |
| Unity MCP 도구 호출 | [docs/agent-rules/unity-mcp.md](docs/agent-rules/unity-mcp.md) | Unity MCP 도구를 호출할 때 |
| 검증 상세 | [docs/agent-rules/verification.md](docs/agent-rules/verification.md) | 변경 후 검증 단계에서 |
| 커밋/브랜치 작업 | [docs/agent-rules/git.md](docs/agent-rules/git.md) | git 커밋, PR, 리베이스 시 |

## 에이전트 작업 절차
1. 수정 전에 이 파일을 읽고 대상 영역을 확인합니다.
2. 작업에 영향을 줄 수 있는 씬, 에셋, 패키지 맥락을 확인합니다.
3. 변경 종류에 맞는 검증을 수행합니다 (자세한 절차는 [docs/agent-rules/verification.md](docs/agent-rules/verification.md)).
4. 변경한 파일, 수행한 검증, 남아 있는 위험 요소를 보고합니다.
