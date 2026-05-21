<!--
multiplayer-network 의 "모든 룸이 동일한 default 씬을 공유" 모델이 구체적으로 어떤
Unity 씬에 매핑되는지를 단일 source of truth 로 박제한다. spec 본문·plan 본문은
이 파일을 가리키는 "default 씬" 추상 표기만 사용한다.
-->

# multiplayer 룸 default 씬 매핑

**Sub-Spec:** [`03-room-session.md`](../specs/03-room-session.md)
**Status:** `Accepted`
**Date:** 2026-05-17

## Context

multiplayer-network 피처는 "모든 룸이 동일한 default 씬을 공유, 씬에는 악기·오브젝트가 미리 배치되어 있음" 모델을 채택했다 ([`../_index.md`](../_index.md), [`../specs/02-user-persistence.md`](../specs/02-user-persistence.md), [`../specs/03-room-session.md`](../specs/03-room-session.md), [`../specs/04-presence-ui.md`](../specs/04-presence-ui.md), [`../specs/06-build-targets.md`](../specs/06-build-targets.md)). 이 default 씬이 실제로 어떤 Unity 씬에 매핑되는지는 빌드 타깃·작업 단계·아트 파이프라인에 따라 바뀐다 (예: 2026-05-16 까지 `SampleScene`, 2026-05-17 부터 `TestSceneSanyo`).

매핑이 바뀔 때마다 spec 본문 4~6개와 plan 본문 여러 개를 일괄 갱신해야 한다면 spec 안정성이 깨지고 변경 비용이 누적된다. 매핑 변경은 1줄 commit 으로 끝나야 한다.

## Options Considered

- **inline-name** — spec 본문에 구체 씬 이름(`SampleScene` 등) 직접 박제. 명시적이지만 매핑이 바뀔 때마다 모든 spec/plan 을 갈아엎어야 한다.
- **build-settings-zero** — "default = `EditorBuildSettings.scenes[0]`" 컨벤션. Unity 설정과 자동 정합이지만, 빌드 씬 순서를 dedicated server boot scene·lobby scene 등 다중 운영 의도와 무관하게 default 위치에 고정해야 해 향후 확장에 제약. spec 읽는 사람이 Unity 를 열어야 현재 매핑을 알 수 있다.
- **single-indirection-file** — spec/plan 본문은 "default 씬" 추상 용어만 쓰고, 실제 Unity 씬 매핑은 이 decision 파일에만 박제. 매핑 변경 = 이 파일 1줄 갱신. drum-stick/trombone 의 `decisions/` 컨벤션과 정합.

## Decision

**single-indirection-file** — multiplayer-network spec 본문과 plan 본문은 "default 씬" 또는 "default 씬 자산" 같은 추상 용어만 사용한다. 실제 Unity 씬 이름·자산 경로 매핑은 이 파일에만 박제한다.

### 현재 매핑 (2026-05-17 기준)

- **씬 이름**: `TestSceneSanyo`
- **씬 자산 경로**: `Assets/Scenes/TestSceneSanyo.unity`

### 매핑 이력

- 2026-04-28 ~ 2026-05-16: `SampleScene` (`Assets/Scenes/SampleScene.unity`)
- 2026-05-17 ~ : `TestSceneSanyo` (`Assets/Scenes/TestSceneSanyo.unity`)

### 변경 절차

default 씬이 다른 Unity 씬으로 바뀔 때:

1. 본 파일의 "현재 매핑" 블록을 새 씬으로 갱신하고 "매핑 이력" 에 한 줄 append.
2. Unity `EditorBuildSettings` 의 빌드 씬 목록에 새 씬이 포함되어 있고 클라이언트·dedicated server 양쪽이 같은 씬을 로드하는지 확인.
3. Spring 백엔드 / dedicated server 가 씬 이름을 환경변수·설정으로 직접 참조하는 경로가 생기면 동기화 (현 시점 없음).
4. multiplayer-network spec/plan 본문은 추상 표기 그대로 유지하므로 추가 변경 없음.
5. 이 매핑이 영향을 주는 in-flight plan 의 "Verified Structural Assumptions" 자산 fact (예: 씬 안 GameObject 존재 여부) 는 새 매핑 씬 기준으로 재검증한 뒤 plan 본문에 시점 박제를 갱신.

## Consequences

- planner / implementer 는 "default 씬" 표현을 보면 본 파일의 "현재 매핑" 블록을 source of truth 로 읽는다.
- multiplayer-network 외 도메인(rhythm-game / session-panel / drum-stick / hands 등) plan 의 `SampleScene` 박제는 본 결정 대상이 **아니다**. 그것들은 자기 도메인 작업/테스트 씬을 가리키는 것이지 multiplayer 룸 default 씬 의미가 아니다 — 별개 정책.
- 매핑이 바뀌면 archive 된 plan 본문은 갱신하지 않는다. archive 는 historical record 로 보존한다.
- 새 sub-spec/plan 작성 시 구체 씬 이름을 직접 박지 말고 "default 씬" 추상 표기를 쓰는 컨벤션을 유지한다. 자산 fact 검증(예: 씬 안 특정 GameObject 존재) 박제 시점에는 "(YYYY-MM-DD <현재 매핑 씬 이름> 기준)" 같은 시점 메타를 함께 박는다.
