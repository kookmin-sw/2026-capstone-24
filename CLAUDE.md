# VirtualMusicStudio Unity 에이전트 가이드

## 프로젝트
- 주요 씬: `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/TestSceneSanyo`
- Unity 6000.3.10f1 / URP / Unity MCP: `com.coplaydev.unity-mcp`

## 상시 규칙

- 기본적으로 한국어로 답한다.
- 새 코드는 `Assets/Hands/Scripts/`, `Assets/Instruments/_Core/Scripts/`처럼 **도메인 폴더 안의 `Scripts/` 서브폴더**에 런타임/에디터 로직을 C#으로 작성한다.
- 사용자가 더 풍부한 런타임 진단이나 디버깅 지원을 명시적으로 요청하지 않았다면 경고·오류·진단 상태 추적 로직을 추가하지 않는다.

### 직렬화 자산 수정 MCP 우선

`.prefab` / `.unity` / `.asset` / ScriptableObject 수정은 manage_* MCP 우선. 텍스트 직접 Edit은 plan 명시 또는 사용자 승인이 선행돼야 하며, **sub-agent 단독 판단 금지**. 결정 트리·예외 조건·YAML 보존 절차는 [`.claude/skills/unity-asset-edit/SKILL.md`](.claude/skills/unity-asset-edit/SKILL.md).

### Unity MCP 워크플로우

스크립트·씬·컴포넌트·프리팹을 Unity MCP로 변경할 때는 [`.claude/skills/unity-mcp-workflow/SKILL.md`](.claude/skills/unity-mcp-workflow/SKILL.md)을 참조한다 — 컴파일 대기·`batch_execute` 의무 룰·`precondition_sha256` stale-file 방지·error recovery가 단일 진실원. **자동 invoke 의무는 없으며, 호출 측이 필요한 섹션만 참조한다.**

## 테스트 정책

- Unity 런타임 코드(`.cs`) 수정 후에는 `unity-test-runner` 서브에이전트를 1회 호출해 회귀를 확인한다.
- MCP 미가용으로 테스트 실행 불가 시 → `MCP UNAVAILABLE` 리포트 후 `reviewer`는 그대로 진행한다.

## 도메인별 가이드

| 도메인 | 가이드 | 다룰 때 |
|---|---|---|
| Hands | [`Assets/Hands/CLAUDE.md`](Assets/Hands/CLAUDE.md) | VR 손 3-Layer(Ghost/Physics/Play), Grip Pose Override, 새 악기에 손 붙이기 |
| Instruments | [`Assets/Instruments/CLAUDE.md`](Assets/Instruments/CLAUDE.md) | 악기 prefab 골격, `InstrumentBase` 계약, MIDI 데이터 흐름, RhythmGame 연동 |
