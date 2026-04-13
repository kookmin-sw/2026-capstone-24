# Agent Rules

이 폴더에는 작업 유형별로 분리된 에이전트 규칙이 들어 있습니다. 항상 로드되는 문서는 루트의 [AGENTS.md](../../AGENTS.md)이며, 이 파일들은 해당 작업을 시작하기 전에 **필요할 때만** 추가로 읽습니다.

## 룰 파일 인덱스

| 작업 유형 | 규칙 파일 | 읽어야 하는 때 |
|---|---|---|
| 씬/프리팹/에셋 수정 | [scene-assets.md](scene-assets.md) | `.unity`, `.prefab`, `.asset` 파일을 만질 때 |
| C# 스크립트 작성/수정 | [coding.md](coding.md) | `Assets/**/*.cs` 편집 시 |
| XR 리그·입력·손 상호작용 | [xr.md](xr.md) | XROrigin, XRI, OpenXR, XR Hands 관련 작업 시 |
| Unity MCP 도구 호출 | [unity-mcp.md](unity-mcp.md) | Unity MCP 도구를 호출할 때 |
| 검증 상세 | [verification.md](verification.md) | 변경 후 검증 단계에서 |
| 커밋/브랜치 작업 | [git.md](git.md) | git 커밋, PR, 리베이스 시 |

## 작성 원칙
- 각 파일은 **단일 작업 유형**의 규칙만 담습니다. 중복 금지.
- 새 규칙을 추가할 때는 먼저 이미 같은 주제의 파일이 있는지 확인하고 거기에 추가합니다.
- 모든 작업에 공통 적용되는 짧은 원칙은 이 폴더가 아니라 루트 [AGENTS.md](../../AGENTS.md)의 "상시 규칙" 섹션에 둡니다.
