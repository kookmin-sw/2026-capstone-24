# tools/run-stack-smoke 5시나리오 자동화 (docker-compose 기반)

**Linked Spec:** [`03-room-session.md`](../../../multiplayer-network/specs/03-room-session.md)
**Status:** `Abandoned (2026-05-25) — superseded by RoomAuthorityValidateJoinTests (단위 진실원) + quest-onsite-integration-verification (운영 환경 합류 회귀). dev 무게중심이 로컬 docker-compose 에서 EC2 Spring/MariaDB + ECS Fargate room-server 로 이동했고, plan 의 Out of Scope (AWS 환경 smoke / CI 통합) 두 가지가 plan ROI 를 0 에 수렴시킴. 같은 사이클에서 짝 산출물 tools/run-room-lifecycle-automation.ps1 도 삭제.`

## Goal

선행 plan에서 만든 docker-compose 통합 스택(spring + mariadb + dedicated-server) 위에서, 룸 세션 라이프사이클 5개 시나리오(`same-session`, `room-full`, `wrong-password`, `correct-password`, `room-cleanup`)를 헤드리스 클라이언트로 자동 실행해 통과/실패를 한 번에 보고하는 회귀 하네스를 구축한다.

## Context

- 선행 plan [`2026-05-07-namae1128-docker-compose-local-stack.md`](2026-05-07-namae1128-docker-compose-local-stack.md)이 docker-compose 통합 스택과 manual smoke까지 닫았다.
- 그 plan에서 분리된 자동화 항목이 본 plan의 단일 책임이다.
- 기존 Windows 자동화 [`tools/run-room-lifecycle-automation.ps1`](../../../../tools/run-room-lifecycle-automation.ps1)은 Dedicated Server를 Windows exe로 띄우고 클라이언트 exe도 Windows에서 spawn하는 구조였다. 본 plan은 동일 5시나리오를 **docker-compose 환경(서버·DB는 컨테이너) + 호스트 헤드리스 클라이언트(Linux Server 빌드 또는 Editor batch)** 혼합 모드로 재구성한다.
- 실 시나리오 정의·기대 결과는 `RoomAuthority.ValidateJoin` 단위 테스트와 `RoomServerAutomationMonitor` 출력 파일을 단일 진실원으로 한다(이미 선행 plan에서 검증 완료).

## Decisions

- 실행 진입점: `tools/run-stack-smoke.ps1`(PowerShell on Windows host) + `tools/run-stack-smoke.sh`(WSL2/Linux host) 듀얼 트랙. 둘 다 같은 결과 디렉터리 규약을 따른다.
- 클라이언트 측은 호스트에서 실행 — 컨테이너 내부 그래픽 의존성 회피.
- 기본 호스트 포트는 `localhost:8080`(spring), Photon Cloud(dedicated-server는 outbound). 호스트 포트가 다르면(8081 등) 환경변수 `MURANG_BACKEND_BASE_URL`로 override.
- 결과 출력 디렉터리: `TestResults/stack-smoke/<YYYYMMDD-HHMMSS>/`에 시나리오별 JSON + summary.json. 기존 `TestResults/room-lifecycle-automation/` 구조와 호환되게.
- 실패 시 즉시 fail-fast가 아니라 5개 모두 시도 후 summary.json에 누적. exit code는 5개 중 하나라도 실패하면 1.

## Approach

1. **docker-compose 가드** — 스크립트 시작 시 `docker compose ps`로 mariadb (healthy) + spring Up 확인. 실패 시 `docker compose up -d` 자동 기동 옵션 + 안내 메시지. teardown 옵션은 명시 플래그(`-Teardown` / `--teardown`)로만.
2. **헤드리스 클라이언트 진입점** — 기존 `RoomClientSmokeTest.unity` + `RoomClientSmokeProbe`의 CLI 인자(`-roomAutomationAction`, `-roomName`, `-roomPassword`, `-roomResultPath`, `-roomJoinedSignalPath`, `-roomHoldSeconds`, `-roomLeaveAfterHold`, `-roomMaxPlayers`)를 그대로 재사용. 신규 코드 없이 환경 wrapping만 추가.
3. **시나리오별 실행** — 5개 시나리오를 함수로 분리:
   - `same-session`: client A `Create murang-room` → client B `Join murang-room` → 둘 다 join 성공.
   - `room-full`: maxPlayers=2 룸에 client A·B 합류 후 client C 합류 → `RoomFull` 거절.
   - `wrong-password`: 비밀번호 룸 생성 후 잘못된 비밀번호로 합류 → `WrongPassword` 거절.
   - `correct-password`: 같은 룸에 올바른 비밀번호로 합류 → 성공.
   - `room-cleanup`: 룸 마지막 인원 leave 후 새 client가 같은 이름으로 join 시도 → `RoomNotFound`.
4. **결과 누적** — 각 시나리오 끝에 JSON 결과 파일 생성, 마지막에 summary.json으로 묶기.
5. **CI 호환** — 셸 한 번 호출 + exit code로 통과 여부 판정 가능하게.
6. **deprecated 표기** — 기존 `tools/run-room-lifecycle-automation.ps1` 상단에 본 스크립트로의 마이그레이션 안내 코멘트 추가(파일은 삭제하지 않고 한 마이너 이후 정리 chore에서 제거).

## Deliverables

- `tools/run-stack-smoke.sh` (bash, WSL2/Linux 호스트용)
- `tools/run-stack-smoke.ps1` (PowerShell, Windows 호스트용)
- `tools/lib/stack-smoke-scenarios.{sh,ps1}` 또는 동등 — 시나리오 함수 모음 (선택)
- `TestResults/stack-smoke/.gitkeep` — 결과 디렉터리 규약 표시 (선택)
- `tools/run-room-lifecycle-automation.ps1` 상단에 deprecated 주석 1~3줄 추가
- `docs/dev/local-stack.md`에 자동화 실행 절차 짧은 섹션 추가

## Acceptance Criteria

- [ ] `[auto-hard]` `tools/run-stack-smoke.{sh,ps1}` 두 진입점이 syntax error 없이 로드된다 (`bash -n`, `pwsh -NoProfile -Command "Get-Command -Syntax ..."`).
- [ ] `[manual-hard]` docker-compose 스택을 띄운 상태에서 `tools/run-stack-smoke.sh`(또는 `.ps1`) 실행 시 5개 시나리오(`same-session`, `room-full`, `wrong-password`, `correct-password`, `room-cleanup`)가 모두 통과하고 exit code 0을 반환한다.
- [ ] `[manual-hard]` 결과 디렉터리 `TestResults/stack-smoke/<YYYYMMDD-HHMMSS>/summary.json`에 5개 시나리오 결과(success/failure + reason)가 기록된다.
- [ ] `[manual-hard]` 일부러 잘못된 비밀번호 시나리오 케이스를 변경(예: `wrong-password`를 옳은 비밀번호로 호출)하면 exit code 1과 함께 해당 시나리오 fail이 summary에 기록된다 (회귀 검출 능력 확인).

## Out of Scope

- AWS EC2 배포 환경에서의 stack-smoke 실행(별도 plan).
- 5시나리오 외 추가 시나리오(매치메이킹·룸 목록 조회 smoke 등 — 별 plan).
- 실 Meta SDK verifier 모드에서의 실행.
- CI 파이프라인 통합(GitHub Actions 등).
- Photon AppId 환경 분리.

## Notes

- 기존 Windows 하네스를 폐기하지 않고 deprecated 표기만 두는 이유는, Linux 산출 의존이 어려운 환경(예: Linux Build Support 미설치)에서 fallback으로 사용 가능하기 때문이다. 한 마이너 이후 chore로 정리.
- 본 plan의 5시나리오 정의는 `RoomAuthorityValidateJoinTests`와 동일 의미를 보존해야 한다 — 단위 테스트가 진실원, 본 스크립트는 통합 회귀.
- Linux 호스트와 Windows 호스트에서 동일 결과를 얻으려면 환경변수 처리(특히 줄바꿈, 경로 구분자) 주의. `Environment.NewLine` 같은 OS 의존 값은 결과 파일에 직접 쓰지 말 것.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
