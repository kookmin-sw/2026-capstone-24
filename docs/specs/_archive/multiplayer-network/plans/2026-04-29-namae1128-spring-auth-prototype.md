# Spring 서버 인증 프로토타입

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Status:** `Done`

## Goal

Meta ID 토큰 검증과 JWT 발급을 처리하는 Spring Boot 인증 서버 프로토타입을 구축한다.

## Context

클라이언트가 Meta 기기에서 발급받은 ID 토큰을 서버에 제출하면 신원을 검증하고 자체 JWT를 반환해야 한다. 실 Meta SDK 연동 전에 Mock 구현으로 흐름 전체를 검증하는 것이 목적이다.

## Approach

1. Spring Boot 프로젝트 초기화 (`backend/`).
2. Meta ID 토큰 검증 인터페이스 정의 및 Mock 구현체 작성.
3. JWT 발급·검증 서비스 구현.
4. Spring Security 설정으로 보호된 엔드포인트와 공개 엔드포인트 분리.
5. 인증 컨트롤러(`POST /auth/login`) 구현.
6. 인메모리 유저 레지스트리로 유저 식별 임시 처리.
7. 전역 예외 핸들러 및 공통 응답 포맷 적용.

## Deliverables

- `backend/src/main/java/com/murang/auth/` — 인증 관련 컨트롤러·서비스·필터
- `backend/src/main/java/com/murang/user/` — 인메모리 유저 레지스트리
- `backend/src/main/java/com/murang/common/` — 예외 핸들러·응답 포맷
- `backend/src/main/resources/application.yml` — 서버 설정
- `backend/build.gradle` — 의존성 정의

## Acceptance Criteria

- [x] `[auto-hard]` `POST /auth/login` 에 유효한 Mock 토큰을 전달하면 JWT가 반환된다.
- [x] `[auto-hard]` 유효하지 않은 JWT로 보호된 엔드포인트에 요청하면 401이 반환된다.
- [x] `[manual-hard]` Spring Boot 애플리케이션이 로컬에서 정상 기동된다.

## Out of Scope

- DB 유저 정보 영속화 (→ `02-user-persistence`)
- 실 Meta Platform SDK 토큰 검증
- Refresh Token 흐름

## Notes

`MockMetaIdTokenVerifier`는 실 기기 연동 전까지 임시 사용. 이후 실 구현체로 교체 필요.
인메모리 유저 레지스트리는 서버 재시작 시 초기화되므로 DB 영속화 plan이 이를 대체해야 한다.
