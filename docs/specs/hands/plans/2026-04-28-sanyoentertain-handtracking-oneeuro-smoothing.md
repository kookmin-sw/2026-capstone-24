# HandTracking 입력 떨림 흡수 (OneEuro 사전 스무딩)

**Linked Spec:** [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)
**Caused By:** [`2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md`](./2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md)
**Status:** `Ready`

## Goal

`PhysicsHandGhostFollower`가 HandTracking source를 추종할 때 raw 입력의 고주파 jitter를 OneEuro 필터로 사전 흡수해, 정상 연주 속도에서 점진 누적되는 떨림 패턴이 사라지게 한다. Controller 경로의 동작은 변경하지 않는다.

## Context

> **선행 plan 검증 실패에서 파생됨.** 선행: `2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md`.
> 실패한 Acceptance Criteria:
> - `[manual-hard]` 정상 연주 속도 떨림·박자 늦음 부재 — Controller는 OK. HandTracking에서 떨림이 점점 심해지다 원복되는 패턴이 반복됨.
>
> 본 plan은 위 항목을 다시 통과 가능하게 만드는 부속 작업을 다룬다.

- 부모 spec [`hands/_index.md`](../_index.md)는 Ghost / Physics / Play 세 핸드 분리 구조를 유지하고, Linked Spec은 그중 Physics 핸드가 비통과 동작을 유지하면서도 정상 연주 속도에서 떨림·박자 늦음 없이 동작할 것을 요구한다.
- **현재 코드/자산 사실 (2026-04-28, 선행 plan 적용 후 기준)**
  - `Assets/Hands/Prefabs/Physics/{Left,Right}PhysicsHand.prefab` — Rigidbody `m_IsKinematic: 0`, `m_UseGravity: 0`, `m_Interpolate: 1`(Interpolate), `m_CollisionDetection: 2`(ContinuousSpeculative). 본 plan에서 자산은 손대지 않는다.
  - `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — `FixedUpdate`에서 `linearVelocity = ClampMagnitude(deltaPos / fixedDeltaTime, maxLinearSpeed)` / `angularVelocity = ClampMagnitude(axis * angle/dt, maxAngularSpeed)`로 root를 추종. 손가락 본은 `LateUpdate` + `OnBeforeRender`에서 `localPosition/localRotation`을 ghost로부터 transform copy. source 자동 선택은 `ResolveSourceMode()` (handTrackingRoot.activeInHierarchy → Controller).
  - 그 결과 Controller 경로는 검증을 통과했지만 HandTracking 경로는 raw 입력 jitter가 매 FixedUpdate `deltaPos / dt`로 증폭되어 큰 진동 velocity가 발생, rigidbody 관성과 결합해 점진 누적되는 떨림이 관측되었다.
- **결정 근거**
  - **신호 자체를 정리**하는 게 정석이라는 판단. velocity 제어기를 PD로 바꾸면 Controller 경로(현재 안정 통과) 회귀 위험이 있고, jitter 폭이 큰 HandTracking은 PD만으론 lag 트레이드를 강제당한다.
  - OneEuro 필터는 손 트래킹 jitter 흡수의 사실상 표준이며, 핵심 특성 — 정지 시 강한 스무딩, 빠른 모션 시 cutoff 자동 상승 → lag 최소화 — 이 "빠른 트릴 / 16분음표 연타에서도 박자 늦음 없이"라는 spec 요구와 정확히 맞물린다.
  - **per-source 옵션으로 두고 HandTracking에만 적용** — Controller 경로는 raw 그대로 통과시켜 회귀 표면을 0으로 만든다. 향후 컨트롤러도 흔들림이 발견되면 같은 필터를 켜기만 하면 됨.
  - 위치는 `Vector3` LPF, 회전은 OneEuro 동등 구조의 slerp 기반 LPF (각속도 norm으로 cutoff 적응)로 분리해 처리. 손가락 본 transform copy 경로는 건드리지 않는다 — root만 안정시키면 본 chain은 root 좌표계 안에서 매끈히 따라온다.
  - 필터 상태는 source 전환(HandTracking ↔ Controller) 시 reset해 활성화 직후 큰 점프가 발생하지 않게 한다.

## Approach

### 1. OneEuro 필터 두 종(위치/회전)을 `PhysicsHandGhostFollower.cs` 내부 struct로 추가

`Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` 안에 private struct 두 개를 추가한다. 별도 파일로 분리하지 않는다(단일 사용처, 외부 노출 의미 없음).

- `OneEuroFilterVector3`
  - 필드: `float mincutoff, beta, dcutoff;` `Vector3 prevX, prevDx;` `bool initialized;`
  - `Vector3 Filter(Vector3 x, float dt)`
    1. `!initialized`면 `prevX = x; prevDx = zero; initialized = true; return x;`
    2. `dx = (x - prevX) / dt;`
    3. `alphaD = 1f / (1f + (1f / (2π·dcutoff)) / dt);`
    4. `dxHat = Lerp(prevDx, dx, alphaD);`
    5. `cutoff = mincutoff + beta * dxHat.magnitude;`
    6. `alpha = 1f / (1f + (1f / (2π·cutoff)) / dt);`
    7. `xHat = Lerp(prevX, x, alpha);`
    8. `prevX = xHat; prevDx = dxHat; return xHat;`
  - `void Reset()` — `initialized = false`.

- `OneEuroFilterQuaternion`
  - 필드: `float mincutoff, beta, dcutoff;` `Quaternion prevQ;` `float prevAngularSpeed;` `bool initialized;`
  - `Quaternion Filter(Quaternion q, float dt)`
    1. `!initialized`면 `prevQ = q; prevAngularSpeed = 0; initialized = true; return q;`
    2. `delta = q * Inverse(prevQ); delta.ToAngleAxis(out angleDeg, out _);` (axis 미사용)
    3. `if (angleDeg > 180f) angleDeg -= 360f;`
    4. `omega = |angleDeg| * Deg2Rad / dt;`
    5. `alphaD = ...(dcutoff, dt);` `omegaHat = Lerp(prevAngularSpeed, omega, alphaD);`
    6. `cutoff = mincutoff + beta * omegaHat;`
    7. `alpha = ...(cutoff, dt);` `qHat = Slerp(prevQ, q, alpha);`
    8. `prevQ = qHat; prevAngularSpeed = omegaHat; return qHat;`
  - `void Reset()` — `initialized = false`.

`alphaFor(cutoff, dt)` 계산은 두 struct에서 동일하므로 클래스의 private static helper로 둔다.

### 2. 직렬화 필드와 인스턴스 추가

`PhysicsHandGhostFollower` 클래스 본체에 추가:

```csharp
[Header("HandTracking Source Smoothing (OneEuro)")]
[SerializeField] bool smoothHandTrackingSource = true;
[SerializeField] float oneEuroMinCutoff = 1f;
[SerializeField] float oneEuroBeta = 0.05f;
[SerializeField] float oneEuroDCutoff = 1f;

OneEuroFilterVector3 m_PosFilter;
OneEuroFilterQuaternion m_RotFilter;
GhostSourceMode m_LastFilteredSourceMode;
```

`m_LastFilteredSourceMode`는 source 전환 감지용. 초기값 `GhostSourceMode.None`.

### 3. `FixedUpdate`에 source 전환 감지 + per-source 필터 적용 삽입

기존 `FixedUpdate`를 다음과 같이 수정한다 (의사코드):

```csharp
var sourceMode = ResolveSourceMode();
if (sourceMode == GhostSourceMode.None) return;
var sourceRoot = ResolveSourceRoot();
if (sourceRoot == null) return;

// source 전환 시 필터 상태 리셋 (활성화 직후 점프 방지)
if (sourceMode != m_LastFilteredSourceMode)
{
    m_PosFilter.Reset();
    m_RotFilter.Reset();
    m_LastFilteredSourceMode = sourceMode;
}

Vector3 targetPos = sourceRoot.position;
Quaternion targetRot = sourceRoot.rotation;

if (smoothHandTrackingSource && sourceMode == GhostSourceMode.HandTracking)
{
    // 매 FixedUpdate에서 파라미터를 struct에 복사 (인스펙터 라이브 튜닝 허용)
    m_PosFilter.mincutoff = oneEuroMinCutoff;
    m_PosFilter.beta = oneEuroBeta;
    m_PosFilter.dcutoff = oneEuroDCutoff;
    m_RotFilter.mincutoff = oneEuroMinCutoff;
    m_RotFilter.beta = oneEuroBeta;
    m_RotFilter.dcutoff = oneEuroDCutoff;

    targetPos = m_PosFilter.Filter(targetPos, Time.fixedDeltaTime);
    targetRot = m_RotFilter.Filter(targetRot, Time.fixedDeltaTime);
}

// 이하 velocity 계산은 sourceRoot.position/rotation 대신 targetPos/targetRot 사용
var deltaPos = targetPos - m_Rigidbody.position;
m_Rigidbody.linearVelocity = Vector3.ClampMagnitude(deltaPos / Time.fixedDeltaTime, maxLinearSpeed);

var deltaRot = targetRot * Quaternion.Inverse(m_Rigidbody.rotation);
deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
if (angleDeg > 180f) angleDeg -= 360f;
var angularVel = axis.normalized * (angleDeg * Mathf.Deg2Rad / Time.fixedDeltaTime);
m_Rigidbody.angularVelocity = Vector3.ClampMagnitude(angularVel, maxAngularSpeed);
```

Controller 경로(`smoothHandTrackingSource == false` 또는 `sourceMode == Controller`)는 `targetPos = sourceRoot.position; targetRot = sourceRoot.rotation;` 그대로 사용 — 회귀 표면 0.

### 4. `OnEnable`에서 필터 reset

기존 OnEnable의 첫 프레임 점프 흡수 블록(`m_Rigidbody.position/rotation = sourceRoot.*`) 직후에 다음 줄 추가:

```csharp
m_PosFilter.Reset();
m_RotFilter.Reset();
m_LastFilteredSourceMode = GhostSourceMode.None;
```

이렇게 두면 본 컴포넌트가 SetActive 토글로 재활성될 때(특히 후속 plan 2의 locomotion 토글) 필터가 깨끗한 상태에서 시작한다.

### 5. 손가락 본 transform copy / 기타 경로 그대로 유지

`SyncFingersFromActiveSource`, `LateUpdate`, `OnBeforeRender`, `BuildJointMap`, `ShouldIgnoreTransform`, `ResolveSourceMode`, `ResolveSourceRoot`, `HasRequiredReferences` 등 — 본 plan에서 손대지 않는다. root 추종이 안정되면 손가락 본은 root 좌표계 안에서 매끈히 따라온다.

### 6. 컴파일·콘솔 검증

Unity MCP `read_console` (또는 사용자가 Editor에서 직접)로 본 변경의 신규 컴파일 에러·경고가 없는지 확인. domain reload 완료까지 대기.

### 7. Play 모드 검증 (사용자 직접)

SampleScene에서 다음을 확인:

- **A** — HandTracking 모드: 자유 공간 추종에서 시각 지연 없음. 빠른 트릴, 양손 16분음표 연타에서 떨림이 점진 누적되는 패턴이 사라짐.
- **B** — HandTracking 모드: 피아노 흰 건반·드럼 패드 비통과 동작이 plan 1과 동일 수준 또는 그 이상으로 유지됨.
- **C** — Controller 모드 회귀 검증: 자유 공간/콘택트 동작이 plan 1과 동일 (시나리오 A/B/C/D 재현).
- **D** — HandTracking ↔ Controller 자동 전환(컨트롤러를 들었다 놓기)에서 손 위치가 크게 튕기지 않음.
- **E** — `oneEuroBeta` 값을 0.005 / 0.05 / 0.2로 라이브 변경했을 때 인스펙터 즉시 반영(작은 값 → 더 부드럽고 lag↑, 큰 값 → 빠른 모션 lag↓ 떨림↑)이 확인됨. 튜닝 결과로 다른 시작값이 더 적절하면 그 값으로 본 plan의 기본값 변경.

## Deliverables

- 수정: `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — OneEuro 필터 struct 두 종(위치/회전) 내부 추가, `smoothHandTrackingSource` / `oneEuroMinCutoff` / `oneEuroBeta` / `oneEuroDCutoff` 직렬화 필드 추가, `FixedUpdate`에 per-source 필터 적용과 source 전환 시 필터 reset 추가, `OnEnable`에 필터 reset 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` Unity 컴파일이 에러 없이 통과한다 (`read_console`에서 본 변경으로 인한 신규 컴파일 에러 0건).
- [ ] `[auto-soft]` `read_console`에서 본 변경으로 인한 신규 런타임 경고·예외가 없다.
- [ ] `[manual-hard]` HandTracking 모드에서 정상 연주 속도(빠른 트릴, 양손 16분음표 연타)에서 떨림이 점진 누적되는 패턴이 사라지고, 박자 늦음이 의식적으로 인지되지 않는다.
- [ ] `[manual-hard]` Controller 모드 회귀 없음 — 자유 공간 추종과 피아노/드럼 비통과 동작이 plan 1 통과 시점과 동일 수준으로 유지된다.
- [ ] `[manual-hard]` HandTracking ↔ Controller 자동 전환(컨트롤러 들었다 놓기) 시 Physics 핸드 root 위치가 크게 튕기지 않는다.
- [ ] `[manual-hard]` 손이 표면에 막힌 상태에서도 활성 Ghost(투명 파랑)가 입력의 raw 위치를 계속 보여준다 (선행 plan AC 회귀 방지).
- [ ] `[manual-hard]` 선행 plan `2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md`의 실패 AC ("정상 연주 속도 떨림·박자 늦음 부재") 가 이 plan 적용 후 재검증에서 통과한다.

## Out of Scope

- velocity 제어기를 PD로 바꾸는 변경 — 본 plan은 신호 정리만으로 떨림이 해소되는지 확인하는 것이 1차 목적. 본 plan 통과 후에도 미세 진동이 남으면 후속 plan에서 PD 도입 또는 critical damping 결합을 검토.
- Controller source에 대한 OneEuro 적용 — `smoothHandTrackingSource == true`로 두면 HandTracking에만 켜진다. Controller도 흔들림이 관찰되면 후속에서 별도 토글 추가.
- locomotion(Move/Turn/Teleport) 시작·종료 시 Physics 일시 중단·복원 — 후속 plan(`2026-04-28-sanyoentertain-locomotion-pause-and-bilateral-safety.md`)에서 다룸.
- 환경 객체(테이블/벽/바닥)에 대한 비통과 — Linked Spec의 Out of Scope.
- 손가락 본 단위 articulated physics 시뮬레이션 — 부모 spec / 선행 plan과 동일하게 본 plan에서도 다루지 않음.
- 콜라이더 형상·layer 변경.

## Notes

- OneEuro 권장 시작값(`mincutoff = 1.0`, `beta = 0.05`, `dcutoff = 1.0`)은 손 트래킹 일반 사례에서 출발점이며, 본 코드베이스 / 헤드셋의 트래킹 노이즈 특성에 따라 시나리오 E에서 튜닝한다.
- 본 plan에서 root 추종이 안정되면 손가락 본 transform copy도 자연히 안정화될 것으로 예상하나, 만약 본 단위 떨림이 별도로 관찰되면 그 케이스는 후속 plan으로 분리한다 (책임 경계).
- 본 plan 통과 확인 후에 plan 2(locomotion)로 진입하는 것이 안전. plan 2의 SetActive 토글 경로는 본 plan의 `OnEnable` 필터 reset에 의존한다.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신. 비워둠. -->
