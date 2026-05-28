# XR Input Mode Detection Method

**Sub-Spec:** [`17-input-mode-readiness-check.md`](../specs/17-input-mode-readiness-check.md)
**From Tech Spec:** `tech-specs/17-input-mode-readiness-check.md §Open Tech Decisions #1`
**Status:** `Accepted`
**Date:** 2026-05-27

## Context

런타임에 현재 XR 입력이 핸드 트래킹인지 컨트롤러인지 구분해야 한다.
Unity는 이를 확인하는 여러 API를 제공하지만, 프로젝트 의존 패키지와 플랫폼 타깃에 따라
신뢰성이 다르다.

## Options Considered

- **XRHandSubsystem 활성 여부** — `SubsystemManager.GetSubsystems<XRHandSubsystem>()` + `subsystem.running` + 최소 한 손 `isTracked`. 프로젝트에 XR Hands 1.7.3이 이미 포함 — 추가 의존 없음.
- **InputSystem Device 타입 조회** — `InputSystem.devices`에서 `HandDevice` 타입 검색. 타입 이름 문자열에 의존해 Unity 버전 간 이름 변경 시 깨질 위험.

## Decision

**XRHandSubsystem 활성 여부** — 프로젝트에 XR Hands 1.7.3이 이미 포함되어 추가 패키지 의존이
없고, Quest 플랫폼의 핸드 트래킹 전환 이벤트에 가장 직접적인 API다.

## Consequences

- `XRInputModeDetector`는 `UnityEngine.XR.Hands` 네임스페이스에 의존한다.
- `XRHandSubsystem`이 없는 플랫폼(PC 스탠드얼론 등)에서는 항상 Controller 모드로 간주한다.
- `XRHandSubsystem.running == true && (leftHand.isTracked || rightHand.isTracked)` → HandTracking 모드로 판단.
- 이 결정을 변경해야 하는 trigger: PC VR 전용 핸드 트래킹 플랫폼 추가 지원 시.
