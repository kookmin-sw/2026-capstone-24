# 04 — Drum Stick / Anchor Prefab 수정 경로

**Linked Spec:** [`../specs/01-anchor-auto-attach-detach.md`](../specs/01-anchor-auto-attach-detach.md), [`../specs/02-stick-no-penetration.md`](../specs/02-stick-no-penetration.md)
**Date:** 2026-05-02
**Status:** Resolved

## 결정

drum_stick prefab(L/R variant 포함)과 drum anchor prefab의 신규 컴포넌트 추가·필드 셋업·child(stick-hand) 추가는 **`manage_prefabs` / `manage_components` MCP를 1차 도구로 사용**한다.

기존의 `DrumStickSetup.cs` 같은 Editor 스크립트 확장 안과 prefab YAML 직접 Edit 안은 채택하지 않는다.

다만 MCP가 처리하지 못하는 propertyPath나 직렬화 표면(예: SerializeReference 다형 필드, custom asset reference resolution 등)이 발견되면 plan에서 그 부분에 한해 fallback 경로를 명시하고 사용자 승인을 받는다.

## Why

- AGENTS.md "직렬화 자산 수정 MCP 우선" 정책의 정면 부합이다. plan에서 명시적 사용자 승인 없이 sub-agent가 텍스트 Edit으로 직렬화 자산을 건드리는 것을 차단한다.
- `DrumStickSetup.cs` 확장 안은 결정적이고 코드로 추적 가능한 장점이 있지만, 본 피처의 변경량(컴포넌트 1~2개·child 1개·필드 몇 개)이 Editor 스크립트 신설/확장의 비용 대비 작다. 한 번 셋업하면 다시 손댈 일이 거의 없는 변경이다.
- YAML 직접 Edit은 항상 fallback이고, 본 결정은 그 fallback의 진입 조건을 "MCP가 처리 못 하는 표면이 발견되면"으로 명시 박제한다.

## How to apply

- plan-implementer는 stick/anchor prefab 변경의 모든 단계에서 다음 순서로 시도한다.
  1. `manage_prefabs` (prefab 자체 생성/load/save·child 구조).
  2. `manage_components` (컴포넌트 추가·SerializedField 셋업).
  3. 위 둘로 처리 못 하는 표면이 발견되면 plan 본문에 fallback 경로를 명시한 뒤 사용자 승인 후 진행. 단독 판단 금지.
- 본 결정은 sub-spec `01-anchor-auto-attach-detach.md`(stick·anchor 신규 컴포넌트)와 `02-stick-no-penetration.md`(stick 콜라이더·rigidbody 셋업) 양쪽 plan-implementer에 모두 적용된다.
- plan은 자산 변경 범위(prefab asset / scene instance / 둘 다)를 unity-asset-edit skill 결정 트리에 따라 명시한다.
