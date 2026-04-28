# Physics 핸드 root 추종 안정화 (critical damping + source 전환 동기화)

**Linked Spec:** [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)
**Caused By:** [`2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md`](./2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md)
**Status:** `Ready`

## Goal

`PhysicsHandGhostFollower`의 root 추종을 critical damping(`Vector3.SmoothDamp`) 기반 PD로 바꾸어 정상 연주 속도에서의 떨림을 흡수하고, HandTracking ↔ Controller source 전환 순간에는 rigidbody 위치/회전과 velocity 적분 상태를 즉시 새 source로 동기화해 root가 크게 튕기지 않게 한다. OneEuro 사전 스무딩(선행 plan)은 그대로 두고 그 위에 결합한다.

## Context

> **선행 plan 검증 실패에서 파생됨.** 선행: `2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md`.
> 실패한 Acceptance Criteria:
> - `[manual-hard]` HandTracking 모드에서 정상 연주 속도(빠른 트릴, 양손 16분음표 연타)에서 떨림이 점진 누적되는 패턴이 사라지고, 박자 늦음이 의식적으로 인지되지 않는다. — 사용자 검증: 떨림 점진 누적 패턴 잔존 + HandTracking 매우 불안정.
> - `[manual-hard]` HandTracking ↔ Controller 자동 전환(컨트롤러 들었다 놓기) 시 Physics 핸드 root 위치가 크게 튕기지 않는다. — 사용자 검증: root가 튕기며 전환 시 손 위치가 매우 불안정.
> - `[manual-hard]` 선행 plan `2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md`의 실패 AC ("정상 연주 속도 떨림·박자 늦음 부재") 가 이 plan 적용 후 재검증에서 통과한다. — 위 #1과 동일한 검증 결과로 실패.
>
> 본 plan은 위 항목을 다시 통과 가능하게 만드는 부속 작업을 다룬다.

- 부모 spec [`hands/_index.md`](../_index.md)는 Ghost / Physics / Play 세 핸드 분리 구조를 유지하고, Linked Spec [`02-instrument-no-penetration.md`](../specs/02-instrument-no-penetration.md)는 그중 Physics 핸드가 비통과 동작을 유지하면서도 정상 연주 속도에서 떨림·박자 늦음 없이 동작하고 source 전환 시 큰 튕김이 없을 것을 요구한다.
- **현재 코드 사실 (2026-04-28, 선행 plan들 적용 후 기준)**
  - `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — `FixedUpdate`에서 OneEuro 사전 스무딩이 적용된 `targetPos` / `targetRot`로부터 `linearVelocity = ClampMagnitude((targetPos - rb.position) / fixedDeltaTime, maxLinearSpeed)`, `angularVelocity = ClampMagnitude(axis * angle / dt, maxAngularSpeed)`로 추종한다. 해석적으로 **P-only 제어**(velocity가 deltaPos에 직접 비례)라 잔여 jitter가 dt로 나뉘며 증폭되어 매 FixedUpdate 큰 진동 velocity가 발생한다.
  - source 전환 감지 블록(`if (sourceMode != m_LastFilteredSourceMode)`)은 OneEuro 필터 두 종만 Reset한다. **rigidbody의 position·rotation과 velocity·angularVelocity는 그대로 둔다.** Controller↔HT 전환 순간 두 ghost root의 raw 위치 차이가 크면(컨트롤러를 손에서 놓는 순간 raw pose가 다름), 첫 FixedUpdate에서 큰 deltaPos가 maxLinearSpeed로 클램프되어 따라가는 동안 root가 visibly 튕긴다.
  - `OnEnable`은 `m_Rigidbody.position/rotation = sourceRoot.*`로 한 번 동기화하지만, 컴포넌트가 enabled 상태인 동안 일어나는 source 전환은 이 경로를 타지 않는다.
  - 손가락 본 transform copy(`SyncFingersFromActiveSource`), `LateUpdate` + `OnBeforeRender`, source 자동 선택(`ResolveSourceMode`)은 그대로 유지한다.
- **결정 근거 — 떨림**
  - OneEuro만으로는 입력단의 jitter는 줄였지만, 제어기 자체가 P-only라 잔여 jitter도 매 frame velocity로 증폭. 신호 정리 + **제어기 damping**을 결합해야 떨림이 누적되지 않는다.
  - 후보:
    - (a) `Vector3.SmoothDamp` — Unity 표준. critical damping 기반의 부드러운 추종. `smoothTime` 한 파라미터로 튜닝. 내부 상태(`Vector3` ref 1개)만 추가하면 됨.
    - (b) 직접 PD (`V = Kp·deltaPos − Kd·rb.linearVelocity`) — 두 게인을 따로 튜닝하고 critical damping 조건을 코드에서 강제해야 함.
    - (c) `maxLinearSpeed`를 더 작게 → lag 부작용 큼.
  - (a) 채택 — 코드량 최소, smoothTime 한 값으로 떨림/lag 트레이드 직관 튜닝, 회전도 angularVelocity에 동일 패러다임 적용 가능.
- **결정 근거 — source 전환 튕김**
  - source 전환 시점에 `m_Rigidbody.position/rotation`을 새 source의 raw 위치로 즉시 텔레포트하고, `linearVelocity·angularVelocity·SmoothDamp 내부 ref`를 0으로 리셋하면 첫 FixedUpdate에서 deltaPos = 0, 잔여 velocity 0으로 출발. OneEuro 필터 reset과 같은 블록에서 처리.
  - 단발성 텔레포트라 콜라이더가 환경 객체와 겹쳐도 그 직후 SmoothDamp가 0 velocity에서 시작하기 때문에 큰 충격력이 발생하지 않는다(하드 펄스 X). source 전환은 사용자 의도(컨트롤러 들었다 놓기)이므로 한 번의 시각적 점프는 자연스러운 행위.

## Approach

### 1. SmoothDamp 기반 root velocity 제어 + 직렬화 필드 추가

`Assets/Hands/Scripts/PhysicsHandGhostFollower.cs`의 클래스 본체에 다음 필드를 추가:

```csharp
[Header("Critical Damping (PD)")]
[SerializeField] float positionSmoothTime = 0.04f;
[SerializeField] float rotationSmoothTime = 0.04f;

Vector3 m_PosVelSmoothRef;
Vector3 m_AngVelSmoothRef;
```

(`smoothTime` 시작값 0.04는 ~25Hz cutoff에 해당. 떨림 흡수와 lag 트레이드의 균형점. 시나리오 G에서 튜닝.)

`FixedUpdate`의 root velocity 계산을 SmoothDamp 호출로 교체:

```csharp
// root 위치 추종 — critical damping (SmoothDamp)
var deltaPos = targetPos - m_Rigidbody.position;
var desiredLinearVel = Vector3.ClampMagnitude(deltaPos / Time.fixedDeltaTime, maxLinearSpeed);
m_Rigidbody.linearVelocity = Vector3.SmoothDamp(
    m_Rigidbody.linearVelocity,
    desiredLinearVel,
    ref m_PosVelSmoothRef,
    positionSmoothTime,
    maxLinearSpeed,
    Time.fixedDeltaTime);

// root 회전 추종 — critical damping (SmoothDamp on angular vel)
var deltaRot = targetRot * Quaternion.Inverse(m_Rigidbody.rotation);
deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
if (angleDeg > 180f) angleDeg -= 360f;
var desiredAngularVel = Vector3.ClampMagnitude(
    axis.normalized * (angleDeg * Mathf.Deg2Rad / Time.fixedDeltaTime),
    maxAngularSpeed);
m_Rigidbody.angularVelocity = Vector3.SmoothDamp(
    m_Rigidbody.angularVelocity,
    desiredAngularVel,
    ref m_AngVelSmoothRef,
    rotationSmoothTime,
    maxAngularSpeed,
    Time.fixedDeltaTime);
```

기존 단순 ClampMagnitude 단일 호출 코드는 SmoothDamp 호출로 대체된다. ClampMagnitude는 desired velocity와 SmoothDamp의 maxSpeed 양쪽에 적용해 최종 velocity가 한도를 벗어나지 않게 한다.

### 2. source 전환 시 rigidbody 위치/회전 + smooth ref 즉시 동기화

`FixedUpdate` 내 source 전환 감지 블록을 다음과 같이 확장:

```csharp
if (sourceMode != m_LastFilteredSourceMode)
{
    m_PosFilter.Reset();
    m_RotFilter.Reset();

    // rigidbody 위치/회전을 새 source로 즉시 동기화 (튕김 방지)
    m_Rigidbody.position = sourceRoot.position;
    m_Rigidbody.rotation = sourceRoot.rotation;
    m_Rigidbody.linearVelocity = Vector3.zero;
    m_Rigidbody.angularVelocity = Vector3.zero;

    // SmoothDamp 내부 상태도 0으로 리셋
    m_PosVelSmoothRef = Vector3.zero;
    m_AngVelSmoothRef = Vector3.zero;

    m_LastFilteredSourceMode = sourceMode;
}
```

`OnEnable`의 첫 프레임 점프 흡수 블록과 동일한 패러다임이며, 본 변경으로 컴포넌트가 enabled 상태에서 일어나는 자동 source 전환(HandTracking ↔ Controller)에서도 root가 한 번의 텔레포트로 새 source 위치에 정렬되고 velocity가 0에서 출발해 큰 튕김이 사라진다.

### 3. OnEnable에서 SmoothDamp ref도 함께 리셋

`OnEnable` 끝부분(기존 OneEuro 필터 reset 블록 직후)에 다음 두 줄 추가:

```csharp
m_PosVelSmoothRef = Vector3.zero;
m_AngVelSmoothRef = Vector3.zero;
```

SetActive 토글로 재활성될 때(특히 후속 plan 3의 locomotion 토글) 잔여 smooth state가 남아있지 않도록.

### 4. 컴파일·콘솔 검증

Unity MCP `read_console`로 본 변경의 신규 컴파일 에러·경고 부재 확인. domain reload 완료까지 대기.

### 5. Play 모드 검증 (사용자 직접)

SampleScene에서 다음 시나리오를 확인:

- **A** — HandTracking 모드 자유 공간 추종: 시각 지연이 의식되지 않는 수준에서 부드러움.
- **B** — HandTracking 모드 정상 연주 속도(빠른 트릴, 양손 16분음표 연타): 떨림이 점진 누적되는 패턴이 사라지고, 박자 늦음이 의식적으로 인지되지 않는다.
- **C** — HandTracking 모드 피아노 흰 건반·드럼 패드 비통과 동작이 plan 1 / plan 2 통과 시점과 동일 수준 또는 그 이상으로 유지된다.
- **D** — Controller 모드 회귀 검증: 자유 공간/콘택트 동작에 새로운 떨림·lag가 없다 (Controller는 OneEuro 미적용이라 본 plan의 SmoothDamp만 적용된 경로).
- **E** — HandTracking ↔ Controller 자동 전환(컨트롤러 들었다 놓기, 또는 그 반대): Physics 핸드 root가 한 프레임 안에 새 source 위치로 정렬되고 그 이후 부드럽게 추종. 큰 튕김·진동·끌려감 없음.
- **F** — 손이 표면에 막힌 상태에서도 활성 Ghost(투명 파랑)가 입력의 raw 위치를 계속 보여준다(선행 plan들 AC 회귀 방지).
- **G** — `positionSmoothTime` / `rotationSmoothTime`을 0.02 / 0.04 / 0.08로 인스펙터에서 라이브 변경했을 때 즉시 반영(작은 값 → 빠른 추종/잔여 떨림 가능, 큰 값 → 부드러움/lag↑). 튜닝 결과로 다른 시작값이 더 적절하면 본 plan의 기본값을 그 값으로 변경.

## Deliverables

- 수정: `Assets/Hands/Scripts/PhysicsHandGhostFollower.cs` — `positionSmoothTime` / `rotationSmoothTime` 직렬화 필드 추가, `m_PosVelSmoothRef` / `m_AngVelSmoothRef` 인스턴스 필드 추가, `FixedUpdate`의 root velocity 계산을 `Vector3.SmoothDamp` 기반으로 교체, source 전환 감지 블록에 rigidbody 위치/회전·velocity·smooth ref 즉시 동기화 추가, `OnEnable`에 smooth ref 리셋 추가.

## Acceptance Criteria

- [ ] `[auto-hard]` Unity 컴파일이 에러 없이 통과한다 (`read_console`에서 본 변경으로 인한 신규 컴파일 에러 0건).
- [ ] `[auto-soft]` `read_console`에서 본 변경으로 인한 신규 런타임 경고·예외가 없다.
- [ ] `[manual-hard]` HandTracking 모드 자유 공간 추종에서 시각 지연이 의식적으로 인지되지 않는다 (시나리오 A).
- [ ] `[manual-hard]` HandTracking 모드 피아노/드럼 비통과 동작이 plan 1·plan 2 시점과 동일 수준 또는 그 이상으로 유지된다 (시나리오 C).
- [ ] `[manual-hard]` Controller 모드 회귀 없음 — 자유 공간/콘택트 동작에 새로운 떨림·lag가 없다 (시나리오 D).
- [ ] `[manual-hard]` 손이 표면에 막힌 상태에서도 활성 Ghost(투명 파랑)가 입력의 raw 위치를 계속 보여준다 (선행 plan들 AC 회귀 방지, 시나리오 F).
- [ ] `[manual-hard]` 선행 plan `2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md`의 실패 AC ("HandTracking 모드에서 정상 연주 속도(빠른 트릴, 양손 16분음표 연타)에서 떨림이 점진 누적되는 패턴이 사라지고, 박자 늦음") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md`의 실패 AC ("HandTracking ↔ Controller 자동 전환(컨트롤러 들었다 놓기) 시 Physics 핸드 root 위치가 크게 튕기지 않는") 가 이 plan 적용 후 재검증에서 통과한다.
- [ ] `[manual-hard]` 선행 plan `2026-04-28-sanyoentertain-handtracking-oneeuro-smoothing.md`의 실패 AC ("선행 plan `2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md`의") 가 이 plan 적용 후 재검증에서 통과한다.

## Out of Scope

- OneEuro 필터 자체의 변경 — 선행 plan의 사전 스무딩은 그대로 유지. 본 plan은 그 위에 제어기 damping을 결합한다.
- locomotion(Move/Turn/Teleport) 시작·종료 시 Physics 일시 중단·복원 — 후속 plan(`2026-04-28-sanyoentertain-locomotion-pause-and-bilateral-safety.md`).
- 환경 객체(테이블/벽/바닥)에 대한 비통과 — Linked Spec의 Out of Scope.
- 손가락 본 단위 articulated physics — 부모 spec / 선행 plan과 동일.
- 콜라이더 형상·layer 변경.
- `maxLinearSpeed` / `maxAngularSpeed` 자동 튜닝 — 본 plan은 SmoothDamp 추가만, 클램프 한도는 기존 값 유지.
- physics-hand plan(`2026-04-28-sanyoentertain-physics-hand-non-kinematic-contact-tracking.md`) `[manual-hard]` "정상 연주 속도 떨림·박자 늦음 부재"의 직접 reflect — 본 plan은 OneEuro plan 통과를 통해 transitive로 처리. 자동 reflect가 매칭되지 않는 경우 사용자가 manual로 physics-hand plan 항목을 통과 처리한다(워크플로우 폴백 경로).

## Notes

- SmoothDamp의 `maxSpeed` 인자는 vector magnitude 한도다. ClampMagnitude를 desired velocity에 한 번 적용하고 SmoothDamp에도 maxSpeed로 같은 값을 넘기면, 원래 P-only 동작 시점과 동일한 한도로 수렴한다.
- 본 plan 통과 후 OneEuro plan의 fail 3건이 자동 reflect로 pass 갱신된다. physics-hand plan의 `[manual-hard]` "정상 연주 속도 떨림·박자 늦음 부재"는 OneEuro plan #5(physics-hand 재검증) 통과로 transitive 통과 의도. 자동 reflect 로직이 직접 매칭되지 않으면 사용자가 manual로 physics-hand plan을 큐에 다시 올려 통과 처리하는 폴백 경로를 사용한다.
- SetActive 토글로 재활성될 때(특히 plan 3의 locomotion 토글) `OnEnable`의 SmoothDamp ref reset이 의존성. plan 3 진행 시 본 plan의 ref reset 코드가 살아 있는지 확인.

## Handoff

<!-- /spec-implement가 plan 완료 시 자동 갱신. 비워둠. -->
