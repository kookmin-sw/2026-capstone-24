# Photon Fusion SDK 패키지 준비

**Linked Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Done`

## Goal

Unity 프로젝트에 Photon Fusion 패키지를 추가해 룸 세션 구현의 기반을 마련한다.

## Context

멀티플레이어 룸 세션 구현을 위해 Photon Fusion SDK가 필요하다. 이 plan은 실제 룸 로직 구현 전 패키지 임포트와 프로젝트 설정 적용만을 목표로 한다.

## Approach

1. Photon Fusion SDK 패키지를 `Assets/Photon/` 아래에 배치.
2. `Packages/manifest.json`에 의존성 등록.
3. Photon App ID 초기 설정 (`NetworkProjectConfig.fusion`).

## Deliverables

- `Assets/Photon/Fusion/` — Fusion SDK 어셈블리 및 런타임
- `Assets/Photon/PhotonLibs/` — WebSocket 등 공통 라이브러리
- `Packages/manifest.json` — 의존성 항목 추가
- `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` — 네트워크 프로젝트 설정

## Acceptance Criteria

- [x] `[auto-hard]` `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll` 파일이 존재한다.
- [x] `[manual-hard]` Unity Editor에서 컴파일 오류 없이 프로젝트가 열린다.

## Out of Scope

- 룸 생성·입장 로직 구현
- Photon App ID 실 환경 연동

## Notes

Photon Fusion 1.x 버전 기준 임포트. App ID는 추후 환경변수 또는 설정 파일로 분리 필요.
