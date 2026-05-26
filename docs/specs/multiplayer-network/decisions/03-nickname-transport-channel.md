# Nickname 전송 채널

**Sub-Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**From Tech Spec:** [`tech-specs/01-user-identity-onboarding.md`](../tech-specs/01-user-identity-onboarding.md) §Open Tech Decisions #1
**Status:** `Accepted`
**Date:** 2026-05-26

## Context

multiplayer 진입 시점에 사용자가 입력한 닉네임을 backend 의 `users.nickname` 컬럼에 어떻게 도달시킬지 결정한다. 인증 흐름 동봉, follow-up 호출, Photon 세션 채널 셋이 후보가 된다.

## Options Considered

- **`/auth/meta-login` payload 동봉** — 인증 POST 한 번에 metaIdToken + nickname 함께. Spring 이 verifier 통과 후 단일 트랜잭션으로 user create + nickname set. nickname 은 optional 이라 1차 호출 (탐색용) 과 2차 호출 (등록) 둘 다 같은 endpoint 로 흐름 통합.
- **별도 endpoint `POST /api/v1/users/me/nickname`** — 인증 성공 후 follow-up 호출. RTT 2회, 중간 실패 시 inconsistency 가능 ⚠.
- **Photon Custom Auth payload** — Photon 세션 입장 시 nickname 전송. 인증 시점이 아닌 룸 입장 시점이라 lobby 단계에서 닉네임을 사용할 수 없음 ⚠.

## Decision

**`/auth/meta-login` payload 동봉** (nickname optional) — 신규/기존 판정과 닉네임 등록 흐름이 한 endpoint 로 묶여 의미 정합성이 높다. 1차 호출은 `nickname=null`, backend 가 신규 유저로 판정하면 400 `NICKNAME_REQUIRED` 응답. 2차 호출은 `nickname` 동봉해 등록. 사용자 선택 (2026-05-26).

## Spec What Coverage

- 01-user-auth `## What` "Meta 계정 기반 인증 흐름" — 만족 (인증 payload 가 확장될 뿐 흐름 변화 없음, 모든 호출이 단일 endpoint).
- 02-user-persistence `## What` "닉네임과 마지막 접속 시각 같은 기본 프로필이 유지" — 만족 (최초 등록 시 nickname 박제).

## Consequences

- Spring `AuthMetaLoginRequest` DTO 에 nickname 필드 추가 (**optional**) → `AuthService.login` 시그니처 / `AuthController` / 단위 테스트 변경.
- 1차 인증 호출 시 `nickname=null`. 신규 판정 + 빈 nickname 인 경우 backend 가 400 `NICKNAME_REQUIRED` 응답을 표준으로 반환.
- 신규 유저의 2차 인증 호출 시에만 nickname 동봉.
- 기존 유저는 nickname 동봉이 발생하지 않음 (클라이언트가 1차에서 보내지 않음 / backend 응답에 등록된 nickname 을 포함해 반환).
- 별도 변경 endpoint 가 본 spec Out of Scope (`04-nickname-input-cadence` 결정의 귀결).
- 향후 재평가 trigger: 닉네임 변경 endpoint 필요성, prod 단계 인증 흐름 변경, Photon Custom Auth payload 통합 필요 시.
