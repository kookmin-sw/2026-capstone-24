# 03 — Anchor 부착 시 Stick 인스턴스 출처

**Linked Spec:** [`../specs/01-anchor-auto-attach-detach.md`](../specs/01-anchor-auto-attach-detach.md)
**Date:** 2026-05-02
**Status:** Resolved

## 결정

drum anchor에 도착하는 순간 `drum_stick_L` / `drum_stick_R` (또는 그에 대응하는 단일 prefab의 좌·우 variant)을 **Instantiate**해 양손에 부착한다. anchor 외부로 텔레포트가 확정되는 순간 두 stick 인스턴스를 모두 **Destroy**한다.

씬에 미리 배치된 drum_stick_L/R 인스턴스(현재 SampleScene의 anchor 외부 좌표)는 본 피처가 들어오는 시점에 정리한다. 정리 방법은 plan에서 확정한다.

## Why

- spawn-destroy 비용이 사실상 무시 가능한 수준이고, "도착할 때마다 항상 깨끗한 상태로 시작"이 sub-spec의 "다시 도착하면 새로 attach" Behavior를 가장 단순히 만족시킨다.
- 씬 기존 인스턴스 재사용 안은 detach 시 좌표·상태 reset 정책을 별도로 결정해야 하고, "어디에도 보이지 않음"을 SetActive=false로만 만족시키면 stick이 갖고 있던 transform·rigidbody 상태가 다음 부착 시점에 의도치 않게 새는 위험이 있다.
- holder pool 안은 "스틱이 드럼 옆에 놓여 있는" 시각 표현을 부차적으로 만들지만, 본 sub-spec의 What "어디에도 보이지 않음"과 정면 충돌하므로 holder를 비가시화해야 해 추가 비용 대비 이득이 없다.

## How to apply

- plan은 anchor 부착 트리거 컴포넌트(decision 01)가 들고 있는 좌·우 stick prefab 참조 1쌍에서 Instantiate한다.
- spawn된 stick은 stick의 신규 컴포넌트(decision 02)가 즉시 Ghost Hand 추종을 시작하도록 셋업된다. Instantiate 시 부모/초기 transform은 anchor 부착 트리거 컴포넌트가 결정한다.
- detach 시 두 인스턴스를 Destroy하고, PlayHand의 source override도 동시에 pop한다.
- 씬 기존 drum_stick_L/R 인스턴스는 sub-spec 01 plan 작업의 일부로 SampleScene에서 제거하거나, anchor 부착 트리거 컴포넌트의 prefab 참조로 흡수한다. 기존 인스턴스가 남아 있어도 sub-spec의 What을 깨지 않는지는 plan-implementer가 자산 검증 단계에서 확인한다.
- 본 결정은 sub-spec `01-anchor-auto-attach-detach.md`의 "도착 시 attach / 이탈 시 어디에도 보이지 않음 / 다시 도착하면 새로 attach" Behavior 셋과 직결된다.
