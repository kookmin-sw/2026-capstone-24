---
name: unity-agent-orchestration
description: "`unity-scene-reader`와 `unity-scene-writer` 두 개의 특화된 서브 에이전트로 Unity 씬 작업을 조율합니다. 변경 전 점검, 씬 변경 적용/검증, 읽기와 쓰기를 분리해야 할 때 사용합니다."
---

# Unity 에이전트 오케스트레이션

이 스킬은 Unity 씬 작업을 읽기 전용 점검과 확정된 변경 적용으로 분리해 다룰 때 사용한다.

## 역할 선택

- 읽기 전용 점검, 검증 중 상태 확인, 씬 사실 수집이 필요하면 `prompts/unity-scene-reader.md`를 읽고 그 규칙으로 `unity-scene-reader` 역할을 수행한다.
- 정확한 변경 내용이 확정되었고 씬 변경을 적용해야 하면 `prompts/unity-scene-writer.md`를 읽고 그 규칙으로 `unity-scene-writer` 역할을 수행한다.
- 메인 스레드가 이미 알고 있는 사실만으로 답할 수 있다면 역할을 분리하지 않는다.

## 전달 원칙

- `unity-scene-reader`에는 대상 씬, 확인할 오브젝트 범위, 필요한 사실만 좁게 넘긴다.
- `unity-scene-writer`에는 대상 씬, 대상 오브젝트, 정확한 변경, 검증 단계를 명확히 넘긴다.
- `unity-scene-writer`가 모호함을 발견하면 추측하지 말고 메인 스레드에 되돌린다.
- 반환된 요약만 메인 스레드에 통합하고, 원본 MCP 출력은 노출하지 않는다.

## 반환 형식

- Reader: `요약`, `다음 작업`, `근거`
- Writer: `적용 내용`, `검증`, `위험 요소`
