# 백엔드 유저 영속화 전환

**Linked Spec:** [`02-user-persistence.md`](../specs/02-user-persistence.md)  
**Status:** `Done`

## Goal

현재 인메모리 기반인 유저 저장소를 영속 DB 기반으로 전환해, 서버 재시작 이후에도 같은 Meta 계정이 동일한 유저 레코드와 동일한 공개 `playerId`로 유지되도록 만든다.

## Context

- 현재 인증 흐름은 [AuthService.java](/D:/2026-capstone-24/backend/src/main/java/com/murang/auth/service/AuthService.java)에서 `InMemoryUserRegistry`를 직접 사용한다.
- 현재 유저 모델은 [UserProfile.java](/D:/2026-capstone-24/backend/src/main/java/com/murang/user/domain/UserProfile.java)의 내부 PK `userId`, 공개 식별자 `playerId`, 제공자 식별자 `metaAccountId`, 표시명 `nickname`, 생성/접속 시각으로 구성된다.
- 현재 `refresh` 흐름은 JWT subject로 `playerId`를 우선 사용하고, 구형 토큰 호환을 위해 `metaAccountId` fallback도 유지한다.
- 백엔드는 Spring Boot 3.4.4 / Java 21 기반이고, 아직 JPA나 DB 드라이버 의존성이 없다.
- 팀의 기준 DB는 `MariaDB`다.

## Decisions

- 개발/배포 공통 DB는 `MariaDB`를 기본으로 한다.
- 백엔드 ORM은 `Spring Data JPA`를 사용한다.
- 유저 테이블은 내부 PK `users.user_id`와 별도로 공개 영구 식별자 `users.player_id`를 가진다.
- `player_id`는 서버가 발급하는 ULID 기반 안정적인 문자열 식별자로 두고, Photon Custom Auth의 `UserId` 같은 외부 세션 식별에는 이 값을 사용한다.
- 유저 테이블은 `meta_account_id` 유니크 제약을 두고, 닉네임도 현재 정책을 유지하기 위해 유니크 제약을 둔다.
- 로그인 시 기존 유저가 있으면 `nickname`, `lastLoginAt`을 갱신한다.

## Approach

1. `spring-boot-starter-data-jpa`, `mariadb-java-client`, 로컬 개발용 DB 설정을 추가한다.
2. `UserProfile` record와 별도로 JPA 엔티티를 만들고, `playerId`/`metaAccountId`/`nickname` 제약과 생성/수정 시각 컬럼을 정의한다.
3. `UserRepository`를 추가하고 `findByPlayerId`, `findByMetaAccountId`, `findByNicknameIgnoreCase` 같은 조회 경로를 명시한다.
4. `InMemoryUserRegistry`를 대체할 `PersistentUserRegistry`를 구현하고, `registerOrUpdate`/`findByPlayerId`/`findByMetaAccountId` 계약을 유지한다.
5. `AuthService`가 구체 클래스 대신 추상화된 유저 레지스트리에 의존하도록 정리한다.
6. 로그인/재로그인/닉네임 충돌/서버 재시작 후 재조회 시나리오를 커버하는 통합 테스트를 추가한다.
7. 로컬 MariaDB 실행 방법을 문서 또는 예시 설정에 남겨 다음 작업자가 바로 띄울 수 있게 한다.

## Deliverables

- `backend/build.gradle` DB/JPA 의존성 추가
- `backend/src/main/resources/application.yml` DB 설정 추가
- `backend/src/main/java/com/murang/user/domain/` 영속 엔티티 추가
- `backend/src/main/java/com/murang/user/repository/` 리포지토리 추가
- `backend/src/main/java/com/murang/user/service/` 영속 저장소 구현 추가
- `backend/src/main/java/com/murang/auth/service/AuthService.java` 의존성 전환
- `backend/src/test/java/...` 유저 영속화/인증 연동 테스트 추가
- 로컬 MariaDB 실행 가이드 또는 예시 설정 추가

## Acceptance Criteria

- [x] `[auto-hard]` 백엔드 테스트가 모두 통과한다.
- [x] `[auto-hard]` 서버 재시작 전후 동일한 `metaAccountId` 로그인 시 같은 `playerId`가 유지된다.
- [x] `[auto-hard]` 서로 다른 `metaAccountId`가 같은 닉네임으로 가입하려 하면 충돌이 유지된다.
- [x] `[manual-hard]` 로컬 MariaDB를 띄운 뒤 백엔드를 재시작해도 `/api/v1/auth/meta-login`과 `/api/v1/users/me` 흐름이 기존과 동일하게 동작한다.

## Out of Scope

- 닉네임 변경 API
- 관리자용 유저 조회 API
- 마이그레이션 도구 도입(Flyway/Liquibase)은 필요 시 후속 plan

## Notes

- 현재 코드베이스가 아직 초기 단계라, 첫 버전은 단일 `users` 테이블 중심으로 단순하게 가는 편이 안전하다.
- 운영 전환 전에 마이그레이션 도구를 붙이는 것이 이상적이지만, 이번 단계에서는 스키마 안정화가 우선이다.
- 2026-05-01: `backend/.gradle-user-home`를 사용해 `./gradlew.bat test`를 다시 실행했고 전체 테스트가 통과했다.
- 2026-05-01: 로컬 MariaDB + `verify-local-auth.ps1`로 `health`, `meta-login`, `users/me`, 동일 Meta 계정 재로그인 시 `playerId` 유지, 닉네임 충돌(409)을 수동 확인했다.

## Handoff

- 로컬 검증은 `backend/run-dev.ps1`와 `backend/verify-local-auth.ps1` 조합으로 재현할 수 있다.
- 재시작 내구성은 `AuthServicePersistenceTest`가 파일 기반 H2(MariaDB 호환 모드)로 애플리케이션 컨텍스트를 두 번 띄워 자동 검증한다.
