# Quest 실기기 멀티 plan 통합 검증

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Caused By:** [`2026-05-08-namae1128-multiplayer-auth-gate.md`](../../_archive/multiplayer-network/plans/2026-05-08-namae1128-multiplayer-auth-gate.md)
**Status:** `Ready`

## Goal

선행 plan 3건(`auth-gate`, `real-meta-verifier`, `aws-ec2-compose-deployment`)의 Quest USB 실기기 manual-hard 5건을, 한 차례의 Quest Android 빌드 + 사이드로드 사이클로 모아 검증한다.

## Context

> **선행 plan에서 Quest 실기기 검증 책임을 이관받음.** 선행: `2026-05-08-namae1128-multiplayer-auth-gate.md`.
> 이관 사유: Quest USB 빌드 + 사이드로드 + 헤드셋 장착은 검증 비용이 큰 절차라 plan별로 반복하면 부담이 크다. auth-gate plan은 Editor mock/down 검증까지만 책임지고 Done 처리됐으며, Quest 실기기 manual-hard는 본 plan으로 흡수.
> 본 plan은 auth-gate가 다루지 못한 Quest 실기기 시나리오와, real-meta-verifier·aws-ec2-compose-deployment의 Quest manual-hard를 한 빌드/사이드로드 사이클로 일괄 검증한다.

### 왜 통합 검증인가

Quest USB 빌드 + Android 사이드로드 + 헤드셋 장착·로그 캡처는 검증 비용이 큰 절차(빌드 5~15분 + 장착·시나리오 실행). 미완료 plan들의 manual-hard 중 Quest 실기기에서만 검증 가능한 항목 5건이 흩어져 있어, plan별로 빌드를 반복하면 사용자 부담이 커진다. 본 plan은 그 5건을 한 빌드/사이드로드 사이클로 묶어 한 세션 안에 일괄 검증한다.

### 다루는 선행 plan과 흡수 AC

1. **[`2026-05-08-namae1128-multiplayer-auth-gate.md`](../../_archive/multiplayer-network/plans/2026-05-08-namae1128-multiplayer-auth-gate.md)** (sub-spec `01-user-auth`, Done — Editor 검증까지 완료. Quest 실기기 검증은 본 plan으로 이관)
   - World-Space `MultiplayerAuthGate` 버튼이 Editor에선 통과(2026-05-10/11 검증). Quest 실기기에서 같은 흐름 재검증.
2. **[`2026-05-08-namae1128-real-meta-verifier.md`](./2026-05-08-namae1128-real-meta-verifier.md)** (sub-spec `01-user-auth`, Ready — 본 plan 시드 시점에는 미구현)
   - real Meta verifier 흐름을 Quest 실기기에서 검증. real verifier 구현이 끝난 뒤에 본 plan 실행 가능.
3. **[`2026-05-07-namae1128-aws-ec2-compose-deployment.md`](./2026-05-07-namae1128-aws-ec2-compose-deployment.md)** (sub-spec `03-room-session`, Ready — 본 plan 시드 시점에는 미구현)
   - Quest 빌드 → EC2 Dedicated Server 룸 합류 검증. aws-ec2 deploy 완료 후 본 plan 실행 가능.

### 실행 전제

본 plan은 위 선행 plan 3건의 **구현이 모두 완료된 뒤**에야 실행 가능하다. 즉 `/spec-implement` 큐 재생성 알고리즘(`Caused By` 체인 + 작성일 위상정렬)에 따라 본 plan이 자연스럽게 큐 마지막에 위치한다. 본 plan을 단독 실행하면 (2)·(3) 시나리오는 real-meta-verifier 미구현 상태로 fail이 보장된다.

### Cross sub-spec 주의

`aws-ec2-compose-deployment`는 `03-room-session` 소관이라 본 plan(Linked Spec `01-user-auth`)과 sub-spec이 다르다. `/spec-implement`의 `Caused By` 자동 reflect는 같은 sub-spec 안에서만 작동하므로(`docs/specs/README.md` "Sub-spec 모드 큐 재생성 알고리즘" 박스 참조), aws-ec2 Quest AC의 재검증 reflect는 사용자가 수동으로 `03-room-session.md`의 Implementation Plans 표를 갱신하는 폴백이 필요하다.

### 보류된 외부 이슈 (본 plan 범위 외)

- `AuthBootstrap`이 던지는 한글 예외 메시지(`로그인 API 호출이 실패했습니다`)가 `LiberationSans SDF`에 없는 글자라 `MultiplayerAuthGate.StatusLabel`에서 깨짐. 2026-05-11 사용자 결정으로 본 plan 시리즈에서 제외 — 별도 plan에서 다루거나 영구히 영문 메시지만 유지하기로.

## Approach

본 plan은 **검증만 수행**한다. 코드/자산 변경 없음.

1. **사전 준비** — Unity Player Settings에서 빌드 타겟 Android, XR Plug-in Management에 OpenXR + Meta Quest feature group 활성, ARM64 권장. 이전 plan들의 빌드 환경이 이미 만들어져 있다고 가정.
2. **백엔드 부팅 (로컬 mock + real)** — 워크트리 루트의 `.env`에 `JWT_SECRET` 채운 뒤 `docker compose up spring mariadb`. real verifier 검증을 위해 `MURANG_META_VERIFIER_MODE=real`, `MURANG_META_APP_ID`/`MURANG_META_APP_SECRET` 환경 변수 추가(real verifier plan handoff 참조).
3. **EC2 백엔드 부팅** — aws-ec2 검증을 위해 EC2 인스턴스에 docker compose stack 배포 + 외부 접근 가능한 IP/도메인 확인. Unity 빌드 시 device backend URL을 EC2로 가리키도록 `BackendUrlConfig` 같은 설정 swap (aws-ec2 plan handoff 참조).
4. **Quest 빌드 & 사이드로드** — Unity `File > Build Settings > Android > Build` 또는 `Build and Run` (Quest USB 연결 상태). APK 산출 후 `adb install -r <apk>` 또는 Quest Developer Hub로 설치. 1회 빌드로 5개 시나리오를 모두 커버.
5. **시나리오 검증 (헤드셋 장착 상태)**:
   1. `MultiplayerAuthGate` 버튼을 ray로 누르기 전 콘솔/`adb logcat`에서 `[AuthBootstrap]` 로그 0건 확인, 누른 후 mock 또는 real 토큰으로 인증 진행.
   2. `MURANG_META_VERIFIER_MODE=real` + 유효한 `MURANG_META_APP_ID/SECRET` 백엔드 → Quest 버튼 클릭 → spring 로그 `is_valid: true` + 200 응답 + ULID `playerId` 확인.
   3. 같은 Meta 계정으로 두 번째 로그인 → 동일 `metaAccountId`에 대해 동일 `playerId` 반환 확인 (MariaDB 영속성).
   4. `MURANG_META_APP_SECRET`을 잘못된 값으로 주입한 백엔드 → Quest 버튼 클릭 → `AUTH_INVALID_TOKEN(401)` 응답 확인.
   5. Quest 빌드의 룸 입장 흐름 → EC2 Dedicated Server에 합류, 서버 로그에 입장 기록.
6. **로그 수집** — `adb logcat` 또는 Quest Developer Hub 로그 뷰어로 클라이언트 로그, spring 로그, dedicated server 로그를 캡처해 본 plan의 Notes(또는 선행 plan들의 Notes)에 첨부.
7. **결과 reflect** — `/spec-implement`가 본 plan의 5개 manual-hard AC 중 매칭 가능한 항목을 자동 reflect로 선행 plan(`auth-gate`)에 pass 갱신. 매칭 실패한 항목(특히 cross sub-spec의 aws-ec2)은 사용자가 수동으로 03-room-session 표/Notes를 갱신.

## Deliverables

본 plan은 **검증 plan**이라 코드/자산 변경 없음. 산출물:

- 검증 로그 스크린샷/텍스트 (본 plan의 `## Notes` 또는 선행 plan들의 `## Notes`에 첨부)
- 선행 plan들의 manual-hard pass 처리 트리거 (자동 reflect 또는 수동 폴백)

## Acceptance Criteria

- [ ] `[manual-hard]` `auth-gate` plan에서 이관된 Quest USB 실기기 검증: Quest USB 빌드(Android) Play 직후 `[AuthBootstrap]` 로그 0건 + `AuthSmokeProbe` 오버레이 미표시 확인 → `MultiplayerAuthGate` 버튼을 ray로 누른 후 mock 또는 real 토큰으로 인증 진행, StatusLabel이 `Multiplayer Active — <nickname>`(성공) 또는 `Failed: <message>`(실패)로 갱신.
- [ ] `[manual-hard]` 선행 plan `2026-05-08-namae1128-real-meta-verifier.md`의 Quest AC ("Spring MURANG_META_VERIFIER_MODE=real + 유효한 MURANG_META_APP_ID/MURANG_META_APP_SECRET으로 띄운 상태에서 Quest") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-05-08-namae1128-real-meta-verifier.md`의 Quest AC ("Quest 실기기에서 두 번째 로그인 시 동일 metaAccountId(= Oculus userId)에 대해 동일 playerId") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-05-08-namae1128-real-meta-verifier.md`의 Quest AC ("MURANG_META_APP_SECRET을 잘못된 값으로 주입한 상태에서 Quest 빌드가 인증 시도하면 AUTH_INVALID_TOKEN(401)") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-05-07-namae1128-aws-ec2-compose-deployment.md`의 Quest AC ("Quest 빌드(또는 Editor에서 device backend URL을 EC2로 가리킨 빌드)가 EC2 Dedicated Server에 룸 합류") 가 이 plan 적용 후 재검증에서 통과한다.

## Out of Scope

- 한글 메시지 깨짐 처리 (2026-05-11 사용자 결정으로 본 plan 시리즈에서 제외)
- 코드/자산 변경 (본 plan은 검증만)
- mock 모드 Editor 검증 (이미 본 auth-gate plan에서 완료)
- Quest 빌드 환경 셋업(XR Plug-in Management, signing key, OpenXR Meta Quest feature group) — 빌드 환경이 깨져 있으면 본 plan에서 분리해 별도 plan으로 처리
- 자동 reflect 매칭 실패 시 03-room-session 표 갱신의 자동화 (필요 시 별도 plan 분리)

## Notes

- 본 plan은 선행 3개 plan의 **구현 완료 후**에야 실행 가능하다. real-meta-verifier·aws-ec2-compose-deployment가 In Progress 또는 Ready로 남아 있으면 본 plan을 큐에 올려도 시나리오 (2)~(5)는 실패로 끝난다.
- aws-ec2-compose-deployment의 Quest AC reflect는 cross sub-spec(03-room-session)이라 `/spec-implement` 자동 reflect가 매칭 실패할 수 있다. 매칭 실패 시 본 plan의 `## Notes`에 한 줄 경고가 자동 append되고, 사용자는 03-room-session sub-spec의 Implementation Plans 표에서 aws-ec2 행을 수동으로 `Done` 처리한다.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
