# Nickname 입력 + 신규 유저 분기 + real Meta 통합 진입 흐름

**Linked Spec:** [`01-user-auth.md`](../specs/01-user-auth.md)
**Status:** `Ready`

## Goal

`MultiplayerAuthGate` 에 NicknameInput + ConfirmButton 자식 GameObject 2건을 신설(초기 비활성)하고, `AuthBootstrap.AuthenticateAsync(nickname)` 시그니처와 Spring `MetaLoginRequest.nickname` 을 optional 로 전환해 1차 인증 → backend 신규 판정 시 `NICKNAME_REQUIRED` 응답 → UI 토글 → 닉네임 입력 → 2차 인증으로 등록까지 한 plan 으로 묶는다. `LobbyNicknameInputValidator` 단위 테스트로 정규식 `^[A-Za-z0-9_-]{1,16}$` 를 가드한다. acceptance 검증 채널은 **Quest 실기기 빌드 (USB 사이드로드) + real 모드 (`useMockMetaToken=false` + `MURANG_META_VERIFIER_MODE=real`) + 유효한 Meta APP_ID/APP_SECRET 환경** 한정. `MultiplayerAuthConfig.useMockMetaToken` 기본값 `false` 와 Spring `MURANG_META_VERIFIER_MODE` 기본값 `real` 을 본 plan 에서 박제한다. mock 코드 (`MockMetaTokenProvider.cs`, `useMockMetaToken` SerializeField, Spring `MockMetaIdTokenVerifier`, `MURANG_META_VERIFIER_MODE=mock` 분기) 자체는 회귀 보조 / 임시 격리용 fallback 으로 그대로 유지.

## Context

01-user-auth `## What` 와 Behavior 9건은 2026-05-26 spec-interview 사이클에서 박제됐고, 본 plan 은 그 Behavior 9~13(신규 유저 분기 + 닉네임 규칙 + 1차 실패 분기)을 구현 사이클로 풀어낸다. Tech Spec [`tech-specs/01-user-identity-onboarding.md`](../tech-specs/01-user-identity-onboarding.md) 가 Components / Data·Control Flow / Boundaries / Invariants 를 단일 진실원으로 박제하며, 본 plan 의 Approach 는 그 5개 섹션을 그대로 풀어낸다. ARD [`decisions/03-nickname-transport-channel.md`](../decisions/03-nickname-transport-channel.md) (payload 동봉) + [`decisions/04-nickname-input-cadence.md`](../decisions/04-nickname-input-cadence.md) (최초 1회만, 이후 영속) 의 Consequences 는 plan 의 제약으로 박제.

배경 (이전 plan 들의 누적 상태):
- archive `multiplayer-auth-gate` plan 이 `MultiplayerAuthGate` (ActivateButton + StatusLabel) 골격을 만들었다 — script GUID `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6`, fileID 800001000, SerializeField 4종.
- archive `real-meta-verifier` plan 이 Spring `RealMetaIdTokenVerifier` + Graph API 호출을 구현. mock/real toggle 은 `MURANG_META_VERIFIER_MODE` 환경 변수.
- archive `backend-user-persistence` plan 이 `users.nickname` 컬럼 + `PersistentUserRegistry.registerOrUpdate` 트랜잭션 박제. 본 plan 은 신규 row 생성 시 nickname 셋업 경로만 활용 (update 경로는 04-cadence 결정으로 차단).
- `quest-onsite-integration-verification` plan 의 real-meta-verifier 3건 (#2/#3/#4) 은 본 plan 으로 인해 real 모드 backend + Quest 실기기에서 통과 가능해지지만, 그 reflect 는 본 plan 책임 아님 (별도 manual-hard 사이클).
- Comparable Sibling: `MultiplayerLobbyPanel.CreateForm.RoomNameInput` (TMP_InputField + VR 키보드) + `CreateButton` (Button + label) 패턴을 그대로 차용. label·anchor·정규식만 본 plan 의도로 교체.

핵심 제약 (Tech Spec Invariants 풀어쓰기):
- 세 GameObject 의 활성 상태는 mutually exclusive — 초기 ActivateButton 1 / 나머지 0, 신규 판정 후 0 / 1, 최종 성공 후 모두 0.
- ActivateButton → NicknameInput/ConfirmButton transition 트리거는 **400 `NICKNAME_REQUIRED` 응답 한 가지만**. 그 외 1차 실패는 transition 발생 안 함.
- nickname 은 trim 후 1~16자, 정규식 `^[A-Za-z0-9_-]{1,16}$` — Spring `MetaLoginRequest` `@Pattern` 도 동일 규칙으로 교체 (기존 `^[\\p{L}\\p{N} ]{2,32}$` 는 폐기).
- 신규 유저 등록 시점에만 `users.nickname` 셋업. 동일 metaAccountId 재로그인 시 nickname 갱신 경로 없음.

## Verified Structural Assumptions

- `MultiplayerAuthGate.cs` 는 `Murang.Multiplayer.Auth` 네임스페이스, `[SerializeField]` 4종 (`multiplayerAuthBootstrap` GameObject / `authBootstrap` AuthBootstrap / `activateButton` Button / `statusLabel` TMP_Text). `OnActivateClicked` → `ActivateAsync` → `authBootstrap.EnsureAuthenticatedAsync()` → `Session.GetCurrentUserAsync` → StatusLabel `Multiplayer Active — <nickname>` (성공) 또는 `Failed: <message>` (실패). `OnAuthenticationCompleted` 이벤트는 `MultiplayerLobbyPanel.HandleAuthenticationCompleted` 가 listen — 출처: `Read Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs (2026-05-26)`, `Read Assets/Multiplayer/Scripts/Presence/MultiplayerLobbyPanel.cs L73-77 (2026-05-26)`.
- `TestSceneSanyo.unity` 의 `MultiplayerAuthGate` GameObject (fileID 800001000) 의 MonoBehaviour (fileID 800001006, script GUID `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6`) SerializeField 와이어링: `multiplayerAuthBootstrap: {fileID: 1211601285}`, `authBootstrap: {fileID: 1211601287}`. ActivateButton/StatusLabel 자식 구조는 Tech Spec § Prefab Hierarchy 박제 동일 — 출처: `Read Assets/Scenes/TestSceneSanyo.unity L5179-5286 (2026-05-26)` + `unity-scene-reader` Pattern A 보고 (Tech Spec 2026-05-26).
- `AuthBootstrap.EnsureAuthenticatedAsync()` 는 인자 없음. 내부에서 `AuthSession.LoginAsync` → `_metaTokenProvider.GetAuthenticationResultAsync` → `_config.ResolveNickname(authenticationResult)` → `_backendApiClient.MetaLoginAsync(metaIdToken, nickname, ct)`. **현 구조에서 nickname 은 `MultiplayerAuthConfig.ResolveNickname` 이 자동 생성하며, 항상 non-null 로 backend 전송**. 본 plan 은 이 경로를 `nicknameOverride: string?` 인자로 분리해 UI 가 입력값(또는 null)을 직접 주입할 수 있게 한다. nickname=null 1차 호출은 Spring 이 `NICKNAME_REQUIRED` 응답으로 처리 — 출처: `Read Assets/Multiplayer/Scripts/Auth/AuthBootstrap.cs (2026-05-26)`, `Read Assets/Multiplayer/Scripts/Auth/AuthSession.cs (2026-05-26)`, `Read Assets/Multiplayer/Scripts/Auth/MultiplayerAuthConfig.cs L59-88 (2026-05-26)`.
- `BackendApiClient.MetaLoginAsync(metaIdToken, nickname, ct)` 는 `MetaLoginRequest { metaIdToken, nickname }` 직렬화. `JsonUtility.ToJson` 는 C# `null` string 을 빈 문자열 `""` 로 직렬화함 (Unity JsonUtility 의 `[Serializable] string` null 비-지원). 따라서 본 plan 은 nickname=null 의 의도를 wire 단계에서 빈 문자열로 변환 — Spring 측 optional 표현은 `nickname == null || nickname.isBlank()` 두 케이스 모두 신규 유저 분기 신호로 받는다 — 출처: `Read Assets/Multiplayer/Scripts/Backend/Http/BackendApiClient.cs L25-34, L74 (2026-05-26)` + Unity JsonUtility 직렬화 규칙 (`Serializable` 필드 null string → "").
- Spring `MetaLoginRequest` 는 record + `@NotBlank @Size(min=2,max=32) @Pattern("^[\\p{L}\\p{N} ]{2,32}$")`. 본 plan 은 nickname 필드 어노테이션을 optional + 새 정규식으로 교체 (`@Pattern(regexp="^[A-Za-z0-9_-]{1,16}$", message="...")` + `@Size(min=1, max=16)` + `@NotBlank` 제거). `AuthService.login` 의 `normalizeNickname` 은 nickname == null/blank 케이스를 신규 분기로 라우팅하도록 변경 — 출처: `Read backend/src/main/java/com/murang/auth/dto/MetaLoginRequest.java (2026-05-26)`, `Read backend/src/main/java/com/murang/auth/service/AuthService.java L31-55 (2026-05-26)`.
- Spring `UserRegistry.registerOrUpdate(metaAccountId, nickname)` 는 신규 row 생성 시 nickname 셋업 (기존 row 가 있으면 `recordLogin(nickname, now)` 으로 nickname 도 갱신). 본 plan 은 04-cadence 결정 ("최초 1회만") 에 맞춰 **신규 row 생성 경로만 사용**한다. 기존 row 분기는 nickname 인자 없이 처리. 따라서 `UserRegistry` 인터페이스에 신규 메서드 2개를 추가: `findOrSignal(metaAccountId)` (기존 시 UserProfile, 신규 시 빈 Optional) + `register(metaAccountId, nickname)` (신규 row 단독 생성). 기존 `registerOrUpdate` 는 호출하지 않음 — 출처: `Read backend/src/main/java/com/murang/user/service/UserRegistry.java (2026-05-26)`, `Read backend/src/main/java/com/murang/user/service/InMemoryUserRegistry.java (2026-05-26)`, `Read backend/src/main/java/com/murang/user/service/PersistentUserRegistry.java (2026-05-26)`.
- Spring `ErrorCode` 에 `AUTH_NICKNAME_REQUIRED(HttpStatus.BAD_REQUEST, "닉네임 등록이 필요합니다.")` 신규 추가. `ApiException.nicknameRequired()` 팩토리 추가 — 출처: `Read backend/src/main/java/com/murang/common/exception/ErrorCode.java (2026-05-26)`, `Read backend/src/main/java/com/murang/common/exception/ApiException.java L25-63 (2026-05-26)`.
- asmdef 의존: 본 plan 의 신규 C# 파일 (`LobbyNicknameInputValidator.cs`) 은 `Assets/Multiplayer/Scripts/Presence/` 폴더 (기존 `LobbyInputValidator.cs` 와 동일 폴더) 에 둔다. asmdef `Murang.Multiplayer` references = `["Fusion.Unity", "Unity.TextMeshPro", "Instruments"]` — `System.Text.RegularExpressions` 는 mscorlib 라 별도 reference 불필요. `MultiplayerAuthGate.cs` 수정 시 신규 import 없음 (TMPro + UnityEngine.UI 기존 사용 중). 단위 테스트 파일 (`LobbyNicknameInputValidatorTests.cs`) 은 `Assets/Multiplayer/Scripts/Room/Tests/` 에 두고 `Murang.Multiplayer.Room.Tests` asmdef (references = `["UnityEngine.TestRunner", "UnityEditor.TestRunner", "Murang.Multiplayer", "Fusion.Unity"]`, `precompiledReferences = ["nunit.framework.dll"]`) 그대로 사용 — 출처: `Read Assets/Multiplayer/Scripts/Murang.Multiplayer.asmdef (2026-05-26)`, `Read Assets/Multiplayer/Scripts/Room/Tests/Murang.Multiplayer.Room.Tests.asmdef (2026-05-26)`.
- 호출 외부 API side effect 박제 — `AuthSession.LoginAsync` 는 `_config.ResolveNickname(authenticationResult)` 의 결과를 무조건 wire 에 실어 보냄. 본 plan 의 1차 호출 (nickname=null) 흐름이 깨지지 않으려면 `LoginAsync` 자체에 nickname override 경로를 만들어야 함 (단순히 `MultiplayerAuthGate` 에서 `AuthBootstrap` 신호만 바꿔서는 안 됨, `AuthSession` 까지 내려가는 인자가 필요). `MockMetaTokenProvider` 는 `MetaAuthenticationResult(prefix+accountId, accountId, null)` 를 반환 — DisplayName=null 이지만 본 plan 흐름과는 무관 (nickname 출처가 UI 입력 또는 backend 응답으로 바뀌므로) — 출처: `Read Assets/Multiplayer/Scripts/Auth/AuthSession.cs L64-89 (2026-05-26)`, `Read Assets/Multiplayer/Scripts/Auth/MockMetaTokenProvider.cs (2026-05-26)`, `Read Assets/Multiplayer/Scripts/Auth/IMetaTokenProvider.cs (2026-05-26)`.
- 현 상태 `MultiplayerAuthConfig.asset` 직렬화: `useMockMetaToken: 1` (mock 모드). 본 plan 은 이 값을 `0` 으로 갱신해 default false 를 박제한다. `mockAccountId: quest-user-02`, `defaultNickname: Murang Quest User2`, `nicknameAccountSuffixLength: 6` 등 mock 보조 필드는 그대로 유지 (mock 회귀 경로 fallback 보존) — 출처: `Read Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset (2026-05-26)`.
- 현 상태 Spring `application.yml` 의 `app.security.meta.verifier-mode: ${MURANG_META_VERIFIER_MODE:mock}` — 환경 변수 미설정 시 기본값 `mock`. 본 plan 은 이 default 를 `real` 로 갱신해 prod/dev 진실원이 real 모드가 되게 박제. 환경 변수로 명시 override 한 경우에만 mock fallback 작동. EC2/Fargate 환경의 docker-compose 또는 task-definition 에 `MURANG_META_VERIFIER_MODE=real` + `MURANG_META_APP_ID` + `MURANG_META_APP_SECRET` 가 이미 주입되고 있으므로 yaml default 갱신만으로 정합 — 출처: `Read backend/src/main/resources/application.yml L49-55 (2026-05-26)`.
- 현 상태 `application-prod.yml` / `application-aws-dev.yml` / `application-dev.yml` 에는 `app.security.meta.verifier-mode` override 없음 (datasource / hikari / cloudwatch / room runtime 만 박제). 따라서 본 plan 의 default 변경은 모든 profile 에 일괄 적용 — 출처: `Read backend/src/main/resources/application-prod.yml (2026-05-26)`, `Read backend/src/main/resources/application-aws-dev.yml (2026-05-26)`, `Read backend/src/main/resources/application-dev.yml (2026-05-26)`.
- acceptance 검증 환경 박제: Quest 실기기 빌드 (USB 사이드로드) + 본 backend 의 real 모드 (`MURANG_META_VERIFIER_MODE=real` + `MURANG_META_APP_ID`/`APP_SECRET` 주입됨) + Meta Horizon 멤버 초대 완료된 테스트 계정. 신규 계정 = "Meta Horizon 멤버 초대 완료 + 본 backend MariaDB `users` 테이블에 처음 들어오는 metaAccountId". 검증 evidence 두 축: (1) Spring 로그 grep — CloudWatch (`aws logs filter-log-events`) 또는 EC2 stdout (`docker logs murang-backend` 또는 `journalctl -u murang-backend`) 에서 `POST /api/v1/auth/meta-login` 라인 및 `AUTH_NICKNAME_REQUIRED` 키워드 hit 카운팅, (2) Quest 헤드셋에서 StatusLabel 텍스트 + GameObject 활성 상태 시각 확인. Unity Editor `read_console` 는 본 plan acceptance 의 진실원 아님 — 출처: 01-user-auth `## What` (2026-05-26 spec), `Read backend/src/main/resources/application-aws-dev.yml + archive aws-dev-https-caddy-cloudflare plan handoff (2026-05-26)`.

## Approach

### Phase A — Spring backend (신규 유저 분기 + nickname optional)

1. **`ErrorCode.AUTH_NICKNAME_REQUIRED` 추가** — `ErrorCode` enum 에 `AUTH_NICKNAME_REQUIRED(HttpStatus.BAD_REQUEST, "닉네임 등록이 필요합니다.")` 항목 append. `ApiException.nicknameRequired()` 정적 팩토리 추가.
2. **`MetaLoginRequest` 어노테이션 교체** — record 필드 `nickname` 의 `@NotBlank` 제거. `@Size(min=1, max=16)` + `@Pattern(regexp="^[A-Za-z0-9_-]{1,16}$", message="닉네임은 영문/숫자/_/- 만 1~16자.")` 로 교체. **null 은 허용** (jakarta validation 은 null 인 필드에 `@Size`/`@Pattern` 적용 안 함, 그래서 `@NotBlank` 만 제거하면 optional). metaIdToken 어노테이션은 그대로.
3. **`UserRegistry` 인터페이스 분리** — 신규 메서드 2종 추가:
   - `Optional<UserProfile> findByMetaAccountId(String metaAccountId)` — 기존 메서드 그대로 활용 (이미 존재).
   - `UserProfile register(String metaAccountId, String nickname)` — 신규 row 단독 생성. `InMemoryUserRegistry.register` + `PersistentUserRegistry.register` 둘 다 구현. nickname duplicate 검사 + ApiException.nicknameDuplicate() 분기는 기존 `registerOrUpdate` 의 신규 분기 코드를 그대로 옮긴다. 기존 `registerOrUpdate` 는 deprecated 처리하고 본 plan 에서는 호출 안 함 (테스트가 의존하는 경우 본 plan 변경 후 회귀 점검).
4. **`AuthService.login` 분기 재작성** — 의사코드:
   ```
   MetaIdentity identity = verifier.verify(request.metaIdToken());
   Optional<UserProfile> existing = userRegistry.findByMetaAccountId(identity.metaAccountId());
   if (existing.isPresent()) {
       UserProfile user = existing.get();  // nickname 갱신 없음
       return toMetaLoginResponse(user, jwtTokenService.issueTokens(user));
   }
   // 신규 유저
   String rawNickname = request.nickname();
   if (rawNickname == null || rawNickname.isBlank()) {
       throw ApiException.nicknameRequired();  // 400 AUTH_NICKNAME_REQUIRED
   }
   String normalizedNickname = normalizeNickname(rawNickname);  // trim + 정규식 재확인
   UserProfile created = userRegistry.register(identity.metaAccountId(), normalizedNickname);
   return toMetaLoginResponse(created, jwtTokenService.issueTokens(created));
   ```
   `normalizeNickname` 메서드는 trim 만 수행하고 정규식은 `@Pattern` 어노테이션이 우선 처리하도록 보존 (record validation 통과 = 정규식 통과).
5. **`AuthServiceTest` / `AuthControllerTest` 갱신** — 기존 `metaLoginReusesPlayerIdForSameMetaAccountAfterNicknameChange` (닉네임 변경 시나리오) 는 04-cadence 결정에 따라 폐기. 신규 테스트 케이스:
   - `metaLoginReturnsNicknameRequiredForUnknownMetaAccountWithoutNickname` → POST `{ metaIdToken: "mock-meta:quest-user-new", nickname: null }` → 400 + `code: "AUTH_NICKNAME_REQUIRED"`.
   - `metaLoginReturnsNicknameRequiredForUnknownMetaAccountWithBlankNickname` → POST `{ ..., nickname: "" }` → 400 + `AUTH_NICKNAME_REQUIRED`.
   - `metaLoginRegistersNewUserWithValidNickname` → POST `{ ..., nickname: "Murang_01" }` → 200 + nickname=`"Murang_01"`.
   - `metaLoginReusesExistingUserAndIgnoresProvidedNickname` → 같은 metaAccountId 로 두 번째 호출 시 nickname 인자 무관하게 첫 등록 nickname 반환 (정확히는 클라이언트가 2번째 호출에선 nickname 보내지 않으므로 null 이지만, 명시적으로 다른 nickname 보내도 무시되는지 검증).
   - 기존 nickname 정규식 케이스 (`Bad*Nickname`, 33자 초과) 는 새 정규식 (`^[A-Za-z0-9_-]{1,16}$`) 기준으로 재작성 — 한글/공백/`*` 모두 reject, 17자 reject.

### Phase B — Unity AuthBootstrap / AuthSession signature 확장

6. **`AuthSession.LoginAsync` 시그니처 확장** — `LoginAsync(CancellationToken)` → `LoginAsync(string nicknameOverride, CancellationToken)`. nicknameOverride 가 null 이면 wire 에 빈 문자열 보내고, non-null 이면 그 값 그대로. `_config.ResolveNickname` 호출 경로는 제거 (자동 생성 nickname 정책 폐기 — 사용자 입력이 진실원).
7. **`AuthSession.EnsureAuthenticatedAsync(CancellationToken)` → `EnsureAuthenticatedAsync(string nicknameOverride, CancellationToken)`** — 캐시 토큰 hit 시 nickname 불사용 (재로그인 경로 아님). 캐시 miss + refresh 실패 분기에서만 `LoginAsync(nicknameOverride, ct)` 호출.
8. **`AuthBootstrap.EnsureAuthenticatedAsync()` → `EnsureAuthenticatedAsync(string nicknameOverride = null)`** — 기본 인자 null 로 기존 호출자 (예: `MultiplayerLobbyPanel` 이 호출하지는 않지만 `authenticateOnStart` 자동 호출 경로 보존) 호환. 자동 호출 (`Start` 의 `_ = EnsureAuthenticatedAsync()`) 은 nickname=null 1차 인증으로 그대로 동작 — 신규 유저면 `AuthFailedException` (cause: ApiException with code `AUTH_NICKNAME_REQUIRED`) 발생, `authenticationFailed` UnityEvent invoke 후 throw.
9. **`AuthFailedException` 식별 가능성 추가** — `AuthFailedException` 에 `public string ApiCode { get; }` 추가 (생성자 오버로드). `AuthSession.LoginAsync` 의 ApiException 캐치 분기에서 `exception.Code` 를 그대로 전달. 이로써 `MultiplayerAuthGate` 가 `ex.ApiCode == "AUTH_NICKNAME_REQUIRED"` 로 신규 유저 분기를 식별.
10. **`MultiplayerAuthConfig.ResolveNickname` deprecated** — 본 plan 호출자 0건이 되므로 메서드는 남기되 `[Obsolete]` 태깅. `nicknameOverride` / `defaultNickname` / `nicknameAccountSuffixLength` SerializeField 들은 후속 cleanup plan 에서 제거 (본 plan 범위 외).

### Phase C — MultiplayerAuthGate UI 토글 + Scene 자산 변경

11. **`MultiplayerAuthGate.cs` 확장** — SerializeField 2종 추가:
   ```csharp
   [SerializeField] private TMP_InputField nicknameInput;
   [SerializeField] private Button confirmButton;
   ```
   상태 머신:
   - `Awake`: `nicknameInput?.gameObject.SetActive(false)`, `confirmButton?.gameObject.SetActive(false)`, `confirmButton?.onClick.AddListener(OnConfirmClicked)`.
   - `OnActivateClicked`: `_phase = Phase.PrimaryAuth`, `ActivatePrimaryAsync()` 호출 → `authBootstrap.EnsureAuthenticatedAsync(null)` await.
     - 성공: 기존 flow (StatusLabel `Multiplayer Active — ...`, OnAuthenticationCompleted).
     - 실패 + `AuthFailedException.ApiCode == "AUTH_NICKNAME_REQUIRED"`: `ShowNicknameForm()` — ActivateButton.SetActive(false), nicknameInput.SetActive(true), confirmButton.SetActive(true), StatusLabel `"Enter a nickname (1-16 chars, letters/digits/_/-)"`. `_activated` 플래그는 false 로 되돌려 ConfirmButton 클릭 가능.
     - 실패 + 그 외: 기존 flow (`Failed: <message>`, ActivateButton 유지, NicknameInput/ConfirmButton 토글 없음).
   - `OnConfirmClicked`: nicknameInput.text 를 `LobbyNicknameInputValidator.Validate` 통과 검사 → 실패면 StatusLabel `"Invalid: <message>"` + 요청 미발사. 통과면 `_phase = Phase.SecondaryRegister`, `_pendingNickname = trimmed`, `RegisterAsync()` 호출 → `authBootstrap.EnsureAuthenticatedAsync(trimmed)` await.
     - 성공: ActivateButton/nicknameInput/confirmButton 모두 SetActive(false), 기존 flow.
     - 실패: StatusLabel `"Failed: <message>"`, nicknameInput / confirmButton 활성 유지, nicknameInput.text 보존, 사용자 재시도 가능.
   - `OnDestroy`: confirmButton onClick listener 제거.
12. **TestSceneSanyo `MultiplayerAuthGate` 자식 트리에 2개 GameObject 추가** — Unity MCP `manage_gameobject.create` 또는 prefab 우선 (sibling pattern). 단 본 자산은 scene instance 라 prefab 화 안 됨, 그러므로 MCP `manage_gameobject.add_child` 또는 `manage_scene.modify` 경로:
   - `NicknameInput` (TMP_InputField + caret + placeholder), `MultiplayerLobbyPanel.CreateForm.RoomNameInput` (fileID 310003000) 의 직렬화를 sibling 으로 복제 후 anchor 만 `(0.5, 0.6)` 중앙, SizeDelta `(240, 50)` 로 교체. ContentType=Standard, CharacterLimit=16. 초기 `m_IsActive: 0`.
   - `ConfirmButton` (Button + Image + child Label TMP), `MultiplayerLobbyPanel.CreateForm.CreateButton` sibling 복제 후 anchor `(0.5, ~0.45)`, SizeDelta `(240, 60)`, label 텍스트 `"확인"`. 초기 `m_IsActive: 0`.
   - `MultiplayerAuthGate` MonoBehaviour 의 SerializeField 와이어링 추가: `nicknameInput: {fileID: <new TMP_InputField>}`, `confirmButton: {fileID: <new Button>}`.
   - **자산 수정 절차는 `.claude/skills/unity-asset-edit/SKILL.md` 결정 트리 따라 MCP 우선**. MCP 가 anchor 미세 조정에서 막히면 사용자 승인 후 텍스트 Edit fallback.
13. **VR 키보드 호환** — `RoomNameInput` 이 VR 키보드 (XR Keyboard 컴포넌트) 와 통합되어 있으면 `NicknameInput` 도 같은 컴포넌트 부착해 동작. archive `presence-ui-lobby-migration-and-ux` plan handoff 의 VR 키보드 와이어링 절차 참고. 본 plan 의 시각 검증 단계에서 Quest 헤드셋에서 InputField 터치 시 키보드 출현 + 입력 동작 확인.

### Phase D — LobbyNicknameInputValidator 단위 테스트

14. **`LobbyNicknameInputValidator.cs` 신설** — `Assets/Multiplayer/Scripts/Presence/LobbyNicknameInputValidator.cs`. 의사코드:
   ```csharp
   namespace Murang.Multiplayer.Presence {
     public static class LobbyNicknameInputValidator {
       public const int MinLength = 1;
       public const int MaxLength = 16;
       public const string FieldNickname = "nickname";
       private static readonly Regex Pattern = new Regex("^[A-Za-z0-9_\\-]{1,16}$", RegexOptions.Compiled);

       public static LobbyValidationResult Validate(string raw) {
         string trimmed = raw == null ? "" : raw.Trim();
         if (trimmed.Length < MinLength) return LobbyValidationResult.Fail(FieldNickname, "Nickname is required.");
         if (trimmed.Length > MaxLength) return LobbyValidationResult.Fail(FieldNickname, $"Nickname must be at most {MaxLength} characters.");
         if (!Pattern.IsMatch(trimmed)) return LobbyValidationResult.Fail(FieldNickname, "Nickname allows letters, digits, _ and - only.");
         return LobbyValidationResult.Ok();
       }
     }
   }
   ```
   `LobbyValidationResult` struct 는 기존 `LobbyInputValidator.cs` 의 것을 재사용 (같은 namespace, 같은 파일에 정의된 struct).
15. **`LobbyNicknameInputValidatorTests.cs` 신설** — `Assets/Multiplayer/Scripts/Room/Tests/LobbyNicknameInputValidatorTests.cs`. NUnit `[Test]` 케이스 ≥ 8건:
   - `Validate_ValidAlnum_Succeeds` ("Murang01")
   - `Validate_UnderscoreAndHyphen_Succeeds` ("user_01-A")
   - `Validate_Empty_Fails` ("")
   - `Validate_Null_Fails` (null)
   - `Validate_WhitespaceOnly_Fails` ("   ")
   - `Validate_Length17_Fails` ("A" * 17)
   - `Validate_KoreanChar_Fails` ("무랑01")
   - `Validate_Space_Fails` ("Murang 01")
   - `Validate_Asterisk_Fails` ("Bad*Nickname")
   - `Validate_TrimsAndPasses` ("  Murang  " → trim 후 "Murang" 통과)

### Phase E — real 모드 default 박제 (mock 코드 자체는 보존)

16. **`MultiplayerAuthConfig.asset` 갱신** — `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset` 의 `useMockMetaToken: 1` 직렬화를 `useMockMetaToken: 0` 으로 변경. `mockAccountId` / `defaultNickname` / `nicknameAccountSuffixLength` / `mockMetaTokenPrefix` 등 mock 보조 필드는 그대로 보존 (회귀 fallback 경로용). 자산 수정 절차는 `.claude/skills/unity-asset-edit/SKILL.md` 결정 트리 따라 MCP 우선. MCP 미가용 시 텍스트 한 줄 Edit fallback 가능 (값 0/1 단일 라인 변경, YAML 구조 안전).
17. **`MockMetaTokenProvider.cs` / `useMockMetaToken` SerializeField / `MultiplayerAuthConfig.ResolveNickname` deprecated 처리 외 mock 분기 코드 보존** — `MockMetaTokenProvider` 는 그대로 유지 (`useMockMetaToken=true` 토글 시에만 활성 경로). `MultiplayerAuthConfig.useMockMetaToken` SerializeField 자체는 유지 (이미 1 → 0 으로 default 갱신만 함). 코드 삭제 금지.
18. **Spring `application.yml` default 갱신** — `app.security.meta.verifier-mode: ${MURANG_META_VERIFIER_MODE:mock}` → `app.security.meta.verifier-mode: ${MURANG_META_VERIFIER_MODE:real}`. 환경 변수 명시 override 시에만 mock fallback. `MockMetaIdTokenVerifier` 자체 + `MetaIdTokenVerifierConfig` 의 mock/real switch 코드는 그대로 유지.
19. **EC2/Fargate 환경 점검** — implementer 가 `backend/src/main/resources/application-prod.yml` / `application-aws-dev.yml` Read 후 `MURANG_META_VERIFIER_MODE` override 가 존재하는지 확인. 없으면 본 plan 의 default 변경만으로 정합. 있으면 그 값이 `real` 인지 확인하고 아니면 갱신 (현 시점 확인된 사실: 두 yml 모두 override 없음 → default 변경 단독으로 충분).

### Phase F — 통합 + 회귀

20. **Unity MCP 통합 검증** — `read_console` (Errors 필터) 0건. `manage_scene.find_gameobjects` 로 `NicknameInput` / `ConfirmButton` fileID 확인. SerializeField 와이어링 grep.
21. **단위/통합 테스트 실행** — `unity-test-runner` sub-agent 호출 (EditMode + PlayMode). Spring `./gradlew test`.
22. **Quest 실기기 + real 모드 manual-hard 3건 수행** — APK 빌드 → Quest USB 사이드로드 → `useMockMetaToken=false` + `MURANG_META_VERIFIER_MODE=real` + 유효한 `MURANG_META_APP_ID`/`APP_SECRET` 주입된 backend 에 접속해 신규/기존/검증 실패 3 시나리오 검증. evidence 두 축: Spring 로그 grep (CloudWatch/EC2 stdout) + Quest 헤드셋 StatusLabel 시각 확인. **본 plan 의 acceptance 검증 진실원**.
23. **(out of scope) `quest-onsite-integration-verification` plan 의 #2/#3/#4 사이클** — real-meta-verifier 의 manual-hard 3건 통과는 본 plan 의 책임 아님. 그러나 본 plan 완료 후 그 plan 의 manual-hard 가 unblock 됨을 Notes 에 기록.

절차 (컴파일 대기, screenshot 검증, `batch_execute` 의존성 처리 등) 는 [`.claude/skills/unity-mcp-workflow/SKILL.md`](../../../.claude/skills/unity-mcp-workflow/SKILL.md) 가 단일 진실원이므로 본 plan 에서 다시 옮기지 않는다.

## Deliverables

- `backend/src/main/java/com/murang/common/exception/ErrorCode.java` — `AUTH_NICKNAME_REQUIRED` 항목 추가.
- `backend/src/main/java/com/murang/common/exception/ApiException.java` — `nicknameRequired()` 정적 팩토리.
- `backend/src/main/java/com/murang/auth/dto/MetaLoginRequest.java` — nickname 어노테이션 optional + 새 정규식.
- `backend/src/main/java/com/murang/auth/service/AuthService.java` — 신규/기존 분기 로직 재작성.
- `backend/src/main/java/com/murang/user/service/UserRegistry.java` — `register(metaAccountId, nickname)` 메서드 추가.
- `backend/src/main/java/com/murang/user/service/InMemoryUserRegistry.java` — `register` 구현 (신규 row 단독).
- `backend/src/main/java/com/murang/user/service/PersistentUserRegistry.java` — `register` 구현 (트랜잭션).
- `backend/src/test/java/com/murang/auth/controller/AuthControllerTest.java` — 신규 5건 + 기존 변경 시나리오 폐기.
- `Assets/Multiplayer/Scripts/Auth/AuthBootstrap.cs` — `EnsureAuthenticatedAsync(nicknameOverride)`.
- `Assets/Multiplayer/Scripts/Auth/AuthSession.cs` — `LoginAsync(nicknameOverride, ct)` + `EnsureAuthenticatedAsync(nicknameOverride, ct)`.
- `Assets/Multiplayer/Scripts/Auth/AuthFailedException.cs` — `ApiCode` 속성.
- `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs` — SerializeField 2종 + `OnConfirmClicked` + `_phase` 상태 머신.
- `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthConfig.cs` — `ResolveNickname` `[Obsolete]` 태깅.
- `Assets/Multiplayer/Scripts/Presence/LobbyNicknameInputValidator.cs` — 신설.
- `Assets/Multiplayer/Scripts/Room/Tests/LobbyNicknameInputValidatorTests.cs` — 신설.
- `Assets/Scenes/TestSceneSanyo.unity` — `MultiplayerAuthGate` 자식에 `NicknameInput` + `ConfirmButton` 2개 GameObject 추가 + MonoBehaviour 와이어링 2종 추가.
- `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset` — `useMockMetaToken: 1` → `useMockMetaToken: 0` 직렬화 갱신 (mock 보조 필드 그대로 보존).
- `backend/src/main/resources/application.yml` — `app.security.meta.verifier-mode: ${MURANG_META_VERIFIER_MODE:mock}` → `${MURANG_META_VERIFIER_MODE:real}` default 갱신. (정확한 적용 파일은 implementer 가 `application-prod.yml` / `application-aws-dev.yml` 의 override 부재를 확인 후 확정 — 현 시점 확인 결과 default 갱신 단독으로 정합.)

## Acceptance Criteria

- [ ] `[auto-hard]` Spring 단위 테스트: `AuthControllerTest.metaLoginReturnsNicknameRequiredForUnknownMetaAccountWithoutNickname` + `metaLoginReturnsNicknameRequiredForUnknownMetaAccountWithBlankNickname` 가 400 + `code: "AUTH_NICKNAME_REQUIRED"` 응답을 단정한다. `metaLoginRegistersNewUserWithValidNickname` 가 200 + 등록된 nickname 반환을 단정한다. `metaLoginReusesExistingUserAndIgnoresProvidedNickname` 가 같은 metaAccountId 두 번째 호출 시 첫 등록 nickname 보존을 단정한다.
  **검증:** `cd backend && ./gradlew test --tests com.murang.auth.controller.AuthControllerTest` exit 0 + 위 4개 테스트 메서드 모두 PASSED.
- [ ] `[auto-hard]` Spring `MetaLoginRequest` 의 nickname 정규식이 `^[A-Za-z0-9_-]{1,16}$` 로 교체됐고, `@NotBlank` 가 제거됐다.
  **검증:** `Grep "nickname"` + `Grep "@Pattern"` 패턴으로 `backend/src/main/java/com/murang/auth/dto/MetaLoginRequest.java` 안에 `^[A-Za-z0-9_-]{1,16}$` 정규식 존재 확인 + `@NotBlank` 미존재 확인 (record `nickname` 필드 한정).
- [ ] `[auto-hard]` Unity EditMode 단위 테스트: `LobbyNicknameInputValidatorTests` 10개 케이스 (`ValidAlnum`/`UnderscoreAndHyphen`/`Empty`/`Null`/`WhitespaceOnly`/`Length17`/`KoreanChar`/`Space`/`Asterisk`/`TrimsAndPasses`) 가 모두 통과한다.
  **검증:** `unity-test-runner` sub-agent 1회 호출, EditMode 결과 `LobbyNicknameInputValidatorTests` 10/10 PASSED.
- [ ] `[auto-hard]` Unity 컴파일 정상 + `MultiplayerAuthGate.cs` 의 SerializeField 2종 (`nicknameInput`, `confirmButton`) 가 신설됐다.
  **검증:** Unity MCP `read_console` (severity=Error, since="last_compile") 결과 0건 + `Grep "private (TMP_InputField nicknameInput|Button confirmButton)"` on `Assets/Multiplayer/Scripts/Auth/MultiplayerAuthGate.cs` 2건 매칭.
- [ ] `[auto-hard]` `AuthBootstrap.EnsureAuthenticatedAsync(string nicknameOverride = null)` 시그니처 확장 + `AuthSession.LoginAsync(string nicknameOverride, CancellationToken)` 시그니처 확장이 됐고, 기존 `_config.ResolveNickname` 호출 경로가 `AuthSession.LoginAsync` 안에서 제거됐다.
  **검증:** `Grep "EnsureAuthenticatedAsync\(string nicknameOverride"` on `Assets/Multiplayer/Scripts/Auth/AuthBootstrap.cs` 1건 + `Grep "LoginAsync\(string nicknameOverride"` on `Assets/Multiplayer/Scripts/Auth/AuthSession.cs` 1건 + `Grep "_config.ResolveNickname"` on `Assets/Multiplayer/Scripts/Auth/AuthSession.cs` 0건.
- [ ] `[auto-hard]` TestSceneSanyo `MultiplayerAuthGate` 자식 트리에 `NicknameInput` (TMP_InputField) + `ConfirmButton` (Button) 2개 GameObject 가 직렬화됐고, 두 GameObject 의 `m_IsActive: 0` (초기 비활성), `MultiplayerAuthGate` MonoBehaviour 의 `nicknameInput`/`confirmButton` SerializeField 가 두 GameObject 의 fileID 로 와이어링됐다.
  **검증:** Unity MCP `manage_scene.find_gameobjects(name_contains="NicknameInput")` + `find_gameobjects(name_contains="ConfirmButton")` 각 1건 매칭 + `Grep "m_Name: NicknameInput"` + `Grep "m_Name: ConfirmButton"` on `Assets/Scenes/TestSceneSanyo.unity` 각 1건 + 해당 GameObject 블록 안 `m_IsActive: 0` 확인 + MonoBehaviour (fileID 800001006) 블록에 `nicknameInput: {fileID: <new>}` + `confirmButton: {fileID: <new>}` 라인 확인.
- [ ] `[auto-hard]` `MultiplayerAuthConfig.asset` 의 `useMockMetaToken` 직렬화가 `0` (real 모드 default) 으로 박제됐다. mock 보조 필드 (`mockAccountId`/`defaultNickname`/`mockMetaTokenPrefix`/`nicknameAccountSuffixLength`) 는 그대로 보존.
  **검증:** `Grep "^  useMockMetaToken: 0$"` on `Assets/Multiplayer/Resources/MultiplayerAuthConfig.asset` 1건 매칭 + `Grep "^  useMockMetaToken: 1$"` 0건 + `Grep "mockAccountId:"` 1건 (필드 보존 확인).
- [ ] `[auto-hard]` Spring `application.yml` 의 `app.security.meta.verifier-mode` default 값이 `real` 로 박제됐다 (`${MURANG_META_VERIFIER_MODE:real}` 형식). 환경 변수 override 가 없을 때 real 모드가 작동한다.
  **검증:** `Grep "verifier-mode: \\$\\{MURANG_META_VERIFIER_MODE:real\\}"` on `backend/src/main/resources/application.yml` 1건 매칭 + `Grep "verifier-mode: \\$\\{MURANG_META_VERIFIER_MODE:mock\\}"` 0건 매칭 (전체 backend 디렉토리).
- [ ] `[manual-hard]` **신규 흐름** — Quest 실기기 빌드 (USB 사이드로드) + real 모드 backend (`useMockMetaToken=false` + `MURANG_META_VERIFIER_MODE=real` + 유효한 `MURANG_META_APP_ID`/`APP_SECRET` 주입). 한 번도 본 backend MariaDB `users` 테이블에 들어온 적 없는 Meta Horizon 멤버 초대 완료 테스트 계정으로 헤드셋 앱 실행 → ActivateButton 누름 → 1차 `POST /api/v1/auth/meta-login` (nickname=null) 발사 → Spring 이 신규 판정 + `AUTH_NICKNAME_REQUIRED` 400 응답 → Quest 헤드셋 화면에서 ActivateButton 사라지고 NicknameInput + ConfirmButton 활성화 + StatusLabel 안내 문구 표시 → VR 키보드로 `Murang_01` 입력 → ConfirmButton 누름 → 2차 `POST /api/v1/auth/meta-login` (nickname=`Murang_01`) 발사 → 200 OK + DB `users` 테이블에 신규 row + `metaAccountId` ↔ `playerId` 매핑 생성 → Quest 헤드셋에서 StatusLabel `Multiplayer Active — Murang_01` 표시 + 세 GameObject (ActivateButton/NicknameInput/ConfirmButton) 모두 비활성화.
  **검증:** (a) Spring 로그 grep — CloudWatch (`aws logs filter-log-events --log-group <murang-backend-log-group> --filter-pattern "meta-login"`) 또는 EC2 stdout (`docker logs murang-backend | grep -E "(meta-login|AUTH_NICKNAME_REQUIRED)"`) 결과에 1차 POST 라인 + `AUTH_NICKNAME_REQUIRED` 키워드 hit 1건 + 2차 POST 라인 + `200` 응답 1건. (b) Quest 헤드셋 시각 확인 — ActivateButton 사라짐 → NicknameInput/ConfirmButton 활성 → 입력 후 StatusLabel `Multiplayer Active — Murang_01` 표시 + 세 GameObject 비활성 상태 사진/비디오 캡처. (c) DB 확인 — `mysql ... -e "SELECT meta_account_id, nickname FROM users WHERE meta_account_id='<test_account_id>'"` 결과 1행 + nickname=`Murang_01`.
- [ ] `[manual-hard]` **기존 흐름** — 같은 Quest 실기기 + 같은 Meta 테스트 계정 (위 신규 흐름 직후) 으로 앱을 종료 후 재실행 (PlayerPrefs 초기화 시뮬레이션 — Quest 앱 데이터 클리어 또는 앱 강제 종료 후 재실행). ActivateButton 한 번 누름 → 1차 `POST /api/v1/auth/meta-login` (nickname=null) 발사 → Spring 이 기존 유저 판정 + 200 OK 응답 (응답 payload 의 nickname=`Murang_01` 포함) → Quest 헤드셋에서 NicknameInput/ConfirmButton 노출 발생 안 함 → StatusLabel `Multiplayer Active — Murang_01` 직진.
  **검증:** (a) Spring 로그 grep — 같은 시간대 grep 결과 POST 라인 1건 + `200` 응답 1건 + `AUTH_NICKNAME_REQUIRED` 키워드 hit 0건. (b) Quest 헤드셋 시각 확인 — ActivateButton 한 번 클릭 후 즉시 StatusLabel `Multiplayer Active — Murang_01` 진입 + NicknameInput/ConfirmButton 한 번도 노출되지 않음 (비디오 캡처).
- [ ] `[manual-hard]` **검증 실패 흐름** — Quest 실기기 + real 모드 backend + 본 backend MariaDB 에 없는 신규 Meta 테스트 계정 (위 신규 흐름과 다른 metaAccountId). ActivateButton 누름 → 1차 POST → `AUTH_NICKNAME_REQUIRED` 응답 → NicknameInput/ConfirmButton 활성화. 잘못된 닉네임 (`Bad*Name`) 입력 → ConfirmButton 누름 → StatusLabel `Invalid: ...` 표시 + backend POST 요청 미발사 + NicknameInput.text 보존 (`Bad*Name` 그대로 유지) + NicknameInput/ConfirmButton 활성 유지. 이어서 NicknameInput.text 를 `Murang_01` 로 정정 → ConfirmButton 누름 → 2차 POST 발사 → 200 OK → 세 GameObject 비활성 + StatusLabel `Multiplayer Active — Murang_01`.
  **검증:** (a) Spring 로그 grep — 잘못된 닉네임 입력 시점에 (잘못된 입력 → ConfirmButton 클릭 → 정정 입력 → ConfirmButton 클릭 시간대) POST 라인 총 2건 만 발생 (1차 nickname=null + 최종 정정 후 nickname=`Murang_01`, 잘못된 닉네임 시점에는 POST 0건). `AUTH_NICKNAME_REQUIRED` 1건 + `200` 1건. (b) Quest 헤드셋 시각 확인 — `Bad*Name` 입력 → ConfirmButton 클릭 → StatusLabel `Invalid: ...` 표시 + NicknameInput.text 가 `Bad*Name` 그대로 보존된 화면 + NicknameInput/ConfirmButton 활성 유지 사진. 이후 정정 → 성공 진입 시각 확인.

## Out of Scope

- Quest USB 빌드 + 사이드로드 + real Meta SDK 토큰 경로 검증 — `quest-onsite-integration-verification` plan 의 #2/#3/#4 manual-hard 가 본 plan 완료 후 unblock 되지만, 그 reflect 사이클은 본 plan 책임 아님.
- 닉네임 변경 endpoint / audit history — 04-cadence 결정으로 본 spec 범위 외.
- `MultiplayerAuthConfig.ResolveNickname` / `nicknameOverride` / `defaultNickname` / `nicknameAccountSuffixLength` SerializeField 의 실제 제거 — `[Obsolete]` 태깅까지만, 물리적 제거는 후속 cleanup plan.
- AuthBootstrap `authenticateOnStart=true` 동작 변경 — 본 plan 은 호환만 보장 (자동 호출 시 nickname=null 1차 인증 → 신규면 `AuthFailedException` throw 후 `authenticationFailed` invoke, 기존이면 정상). 자동 호출 disable 토글은 본 plan 범위 외.
- Unity Editor Play 환경 자체가 본 spec 의 acceptance 검증 채널 아님 (Quest 실기기 + real 모드 한정). Editor Play 에서만 재현되는 인증 동작 이슈 (mock 모드 토글 동작 포함) 는 본 plan 의 acceptance 진실원 아님. mock 코드는 회귀 보조 / 임시 격리용 fallback 으로만 유지되며, Editor Play 에서의 mock 흐름 검증은 별도 사이클 책임. — 01-user-auth `## What` (2026-05-26 사용자 정책).
- 한글 메시지 깨짐 처리 (archive notes).
- `MultiplayerLobbyPanel` / `MultiplayerInRoomPanel` / Photon Custom Auth payload / JWT/refresh 흐름 / `users.user_id` / `users.player_id` / `users.metaAccountId` 컬럼 — Tech Spec § Boundaries 박제.

## Notes

- 본 plan 의 acceptance 진실원은 **Quest 실기기 빌드 + real 모드 backend** 한정. Editor Play 환경에서의 동작 확인은 회귀 보조용 (auto-hard AC + unit test 가 코드 단위 회귀를 막음). manual-hard 3건은 모두 Quest 헤드셋 + Spring 로그 grep 두 축 evidence.
- mock 관련 코드 (`MockMetaTokenProvider.cs`, `MultiplayerAuthConfig.useMockMetaToken` SerializeField, Spring `MockMetaIdTokenVerifier`, `MetaIdTokenVerifierConfig` 의 mock/real switch, `MURANG_META_VERIFIER_MODE=mock` 분기) 는 모두 그대로 유지. 본 plan 은 default 값만 real 쪽으로 갱신 (`useMockMetaToken: 1 → 0`, `${MURANG_META_VERIFIER_MODE:mock} → ${MURANG_META_VERIFIER_MODE:real}`). mock 토글은 회귀 디버깅 시 명시 override 로 활성화 가능.
- 본 plan 완료 후 `quest-onsite-integration-verification` plan 의 real-meta-verifier 3건 (#2/#3/#4) 의 backend 측 전제 조건 (nickname optional + 신규 분기 + Meta Graph API verifier + real 모드 default) 이 모두 만족됨. 별도 Quest USB 사이클로 manual-hard 재검증 가능.
- `AuthService.login` 의 신규/기존 분기 로직은 `02-user-persistence` sub-spec 의 Behavior ("최초 로그인 시 유저 레코드 생성", "재로그인 시 기존 레코드 조회") 와 cross-cutting 이지만 본 plan 은 01-user-auth Linked Spec 에만 reflect 한다. 02-user-persistence 표 추가는 본 plan 책임 아님 (Tech Spec 의 cross-cutting 안내만으로 충분).
- Spring `UserRegistry.registerOrUpdate` 메서드는 호출자 0건이 된 뒤에도 인터페이스에 남겨두고 deprecated 처리하지 않는다 (후속 plan 에서 제거 결정 시 사용). 본 plan 은 `register` + `findByMetaAccountId` 신규 호출 경로만 사용.
- `AuthSession.LoginAsync` 의 nickname=null wire 직렬화는 Unity JsonUtility 가 빈 문자열 `""` 로 직렬화하므로 Spring 측 `nickname` 필드는 `String` (record) 에서 `""` 또는 `null` 로 도달한다. AuthService 의 신규 분기 검사는 `rawNickname == null || rawNickname.isBlank()` 둘 다 `AUTH_NICKNAME_REQUIRED` 로 처리.

## Handoff

<!-- /spec-implement 가 plan 완료 후 채움. -->
