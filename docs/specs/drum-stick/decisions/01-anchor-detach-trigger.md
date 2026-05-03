# 01 — Anchor 도착·이탈 트리거 신호 소스

**Linked Spec:** [`../specs/01-anchor-auto-attach-detach.md`](../specs/01-anchor-auto-attach-detach.md)
**Date:** 2026-05-02
**Status:** Resolved

## 결정

drum anchor에 **자체 컴포넌트**를 두어 자기 자신의 `TeleportationAnchor.teleporting` (또는 `selectExited`) 이벤트로 attach 트리거를 만들고, detach는 "현재 부착 상태에서 다음 텔레포트가 시작되는 순간" 한 번 끊어주는 flag 방식으로 추적한다.

중앙 broker나 `TeleportationProvider.endLocomotion` 구독은 채택하지 않는다.

## Why

- broker는 일반화 가능하지만 본 피처 범위(드럼킷 anchor 1곳) 대비 신규 컴포넌트 1개 추가가 과한 비용이다.
- `TeleportationProvider.endLocomotion`은 anchor 비종속이지만 모든 stick controller가 provider 참조를 들고 있어야 하고, "이번 도착이 drum인지" 비교 로직이 결정 시점마다 반복된다.
- drum anchor 자기 자신에 컴포넌트를 두면 "도착=내 이벤트 들어옴 / 이탈=내가 부착 상태일 때 외부 텔레포트 시작" 두 신호로 충분하며, 코드 표면이 가장 좁다.

## How to apply

- plan에서 drum anchor prefab(`InstrumentAnchor.prefab` 또는 그 variant)에 신규 MonoBehaviour를 한 개 추가하고, 그 컴포넌트가 자기 자신의 `TeleportationAnchor` 이벤트를 구독해 attach 트리거를 발행한다.
- detach 트리거는 같은 컴포넌트가 "내가 attach 상태"를 들고 있다가, anchor 정렬을 끝낸 사용자가 다음 텔레포트(라인 표시가 아닌 *확정*)를 시작하는 순간 한 번 끊는 방식으로 구현한다. 텔레포트 라인 표시와 확정의 구분은 plan에서 XRI API 표면을 점검해 정한다.
- 본 결정은 sub-spec `01-anchor-auto-attach-detach.md`의 "도착 / 이탈" Behavior 두 항목을 묶는 단일 신호 소스다.
