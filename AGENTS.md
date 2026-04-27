# VirtualMusicStudio Unity 에이전트 가이드

## 프로젝트
- 주요 씬: `Assets/Scenes/SampleScene.unity`
- Unity 6000.3.10f1 / URP / Unity MCP: `com.coplaydev.unity-mcp`

## 상시 규칙

- 기본적으로 한국어로 답한다.
- 새 코드는 `Assets/Hands/Scripts/`, `Assets/Instruments/_Core/Scripts/`처럼 **도메인 폴더 안의 `Scripts/` 서브폴더**에 런타임/에디터 로직을 C#으로 작성한다.
- 사용자가 더 풍부한 런타임 진단이나 디버깅 지원을 명시적으로 요청하지 않았다면 경고·오류·진단 상태 추적 로직을 추가하지 않는다.

## 작업 유형별 트리거

아래 상황에 들어가면 해당 skill이 자동 트리거되어 필요한 절차를 함께 로드한다. 항상 모든 절차를 컨텍스트에 들고 다니지 않는다.

| 상황 | 트리거되는 skill |
|---|---|
| 씬·프리팹·`.asset` 등 Unity 직렬화 자산을 수정 | `unity-asset-edit` |
| 커밋·브랜치·PR·push 같은 git/GitHub 작업 | `git-workflow` |
| Plan 구현 완료 후 Status·아카이브 갱신 | `plan-complete` |
| 새 spec 작성 / Open Questions 닫기 / plan 작성 | `/spec-new`, `/spec-resolve`, `/plan-new` (slash command) |

자동 트리거가 안 들어오면 사용자가 명시적으로 요청하거나, 메인 에이전트가 의도를 인식해 호출한다.

## Spec 시스템

What/Why를 담는 얇은 **spec**과 How를 담는 실행 가능한 **plan**을 분리해 관리한다. 폴더 구조, 파일명 규칙(sub-spec NN-prefix, plan `<YYYY-MM-DD>-<author>-<slug>`), Plan 실행 시 읽기 순서, 작성 anti-pattern, 상태 보드는 모두 [`docs/specs/README.md`](docs/specs/README.md)에 단일 진실원으로 관리한다.

사용자가 **저장된 plan대로 구현해달라고 요청**하면, 먼저 해당 plan 파일을 읽고 → `Linked Spec` → parent `_index.md` 순으로 컨텍스트를 적재한 뒤 작업을 진행한다.
