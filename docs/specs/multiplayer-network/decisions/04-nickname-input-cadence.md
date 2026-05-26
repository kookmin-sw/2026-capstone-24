# Nickname 입력 Cadence

**Sub-Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**From Tech Spec:** [`tech-specs/01-user-identity-onboarding.md`](../tech-specs/01-user-identity-onboarding.md) §Open Tech Decisions #2
**Status:** `Accepted`
**Date:** 2026-05-26

## Context

유저가 Quest 세션에 진입할 때마다 닉네임을 새로 입력해야 하는가, 아니면 한 번 설정 후 영속되어 재진입 시 입력이 생략되는가를 결정.

## Options Considered

- **매 인증마다 다시 입력** — 매 세션 진입 시 InputField 사용자 확인. UX 단순, backend 매 호출 = upsert.
- **최초 로그인 시만 입력, 이후 영속** — 첫 인증에서만 닉네임 채움. 재진입 시 backend 가 저장된 nickname 자동 사용. 별도 변경 endpoint 필요 없음.
- **항상 입력 + 변경 동기화** — 매 진입 입력 + 변경 감지 시 backend nickname 갱신. 결국 (1) 과 사실상 동일.

## Decision

**최초 로그인 시만 입력, 이후 영속** — backend 가 `metaAccountId` 조회로 신규/기존 판정의 진실원이 되며, 신규 유저는 `NICKNAME_REQUIRED` 응답을 받은 시점에서만 InputField 가 활성화된다. 기존 유저는 InputField 노출 없이 한 번의 ActivateButton 클릭으로 진입. 한 번 등록한 nickname 은 본 spec 범위에서 변경 불가. 사용자 결정 (2026-05-26).

## Spec What Coverage

- 01-user-auth `## What` "Meta 계정 기반 인증 흐름" — 만족 (1차/2차 호출 모두 동일 endpoint).
- 02-user-persistence `## What` "닉네임과 마지막 접속 시각 같은 기본 프로필이 유지" — 만족 (최초 등록 후 영속).

## Consequences

- 초기 UI 에 NicknameInput + ConfirmButton 자식 GameObject 는 `m_IsActive: 0` (비활성). 신규 판정 응답 시 ActivateButton 을 `m_IsActive: 0` 으로 토글 + NicknameInput / ConfirmButton 을 `m_IsActive: 1` 로 토글. 세 GameObject 의 활성 상태가 mutually exclusive.
- backend `users.nickname` 은 신규 등록 시점에 set, 이후 update 경로 없음.
- 닉네임 변경 endpoint / audit history 가 본 spec Out of Scope.
- 신규 유저 흐름은 RTT 2회 (1차 인증 → `NICKNAME_REQUIRED` → 닉네임 입력 → 2차 인증). 기존 유저는 RTT 1회.
- 1차 인증 호출의 모든 실패 (`NICKNAME_REQUIRED` 이외) 는 NicknameInput / ConfirmButton 노출을 트리거하지 않는다. ActivateButton 만 유지된 채 StatusLabel 에 사유가 표시되며 재시도는 동일 ActivateButton 으로.
- 향후 재평가 trigger: 닉네임 변경 빈도 데이터로 변경 endpoint 가 필요해질 때, 또는 단일 닉네임 정책이 운영상 부담이 될 때.
- mock 모드 토글 (`useMockMetaToken`, `MURANG_META_VERIFIER_MODE`) 은 기본값 `false` / `real`. 개발·검증 사이클은 Quest 실기기 + real 모드를 진실원으로 사용하며, mock 코드는 회귀 보조 / 임시 격리용 fallback 으로만 유지된다. Editor Play 환경에서의 acceptance 검증은 본 spec 책임 외 (2026-05-26 사용자 정책).
