# VirtualMusicStudio Unity 에이전트 가이드

## 프로젝트
- 주요 씬: `Assets/Scenes/SampleScene.unity`
- Unity 6000.3.10f1 / URP / Unity MCP: `com.coplaydev.unity-mcp`

## 상시 규칙
- 기본적으로 한국어로 답한다.
- 정합 대상이 씬/프리팹 같은 Unity 직렬화 자산이면 `docs/agent-rules/alignment.md`와 `docs/agent-rules/serialized-assets.md`를 함께 읽고 작업한다.

## Plan 파일 규칙
- 사용자가 **plan을 저장해달라고 명시한 경우에만** 계획 전체를 `docs/plans/PLAN.md`에 저장한다. 저장 요청이 없으면 이 파일을 건드리지 않는다.
- 사용자가 **저장된 plan대로 구현해달라고 요청**하면, 먼저 `docs/plans/PLAN.md`를 읽고 그 내용을 기준으로 작업을 진행한다.

## 작업 유형별 규칙
작업 시작 전 필요한 파일을 읽을 것.

| 작업 유형 | 규칙 파일 |
|---|---|
| C# 스크립트 작성/수정 | @docs/agent-rules/coding.md |
| 정합 작업 | @docs/agent-rules/alignment.md |
| 씬/프리팹/직렬화 자산 수정 | @docs/agent-rules/serialized-assets.md |
| Git 관련 작업(브랜치명 추천/생성, 커밋, PR) | @docs/agent-rules/git.md |
