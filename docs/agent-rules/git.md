# Git 커밋/PR 규칙

"commit 해줘", "커밋해", "PR 만들어줘" 등의 요청을 받으면 아래 절차를 따른다.

## 커밋 절차

### 1. 변경사항 수집

`git status`, `git diff`(스테이징/언스테이징 모두), `git log -5`를 병렬로 실행해 변경된 파일과 기존 커밋 컨벤션을 파악한다.

### 2. 이상 여부 점검

커밋 전에 변경 사항 중 이상한 점이 없는지 먼저 확인한다.

- 의도하지 않은 파일이 섞였는지 확인한다.
- 로컬 설정, 자격증명, 임시 파일, 생성물 같은 불필요한 변경이 포함됐는지 확인한다.
- 씬, 프리팹, 직렬화 파일에서는 우연히 생긴 churn이나 제거/추가가 이상한 오브젝트가 없는지 확인한다.
- 스크립트 변경에서는 컴파일을 깨뜨릴 가능성이 있는 수정, 미완성 코드, 디버그 로그 잔재가 없는지 확인한다.
- 여러 작업이 섞여 있으면 분리 가능한지 먼저 판단하고, 이상하거나 애매한 변경은 바로 커밋하지 말고 사용자에게 확인한다.

### 3. 논리 단위 분할

변경 파일을 다음 기준으로 그룹핑한다.

- **type**: `feat` / `fix` / `refactor` / `chore` / `docs` / `test`
- **scope**: `VR`, `scene`, `input`, `UI`, `build`, 도메인 폴더명(`hands`, `instruments` 등)
- **최소 작동 단위**: 컴파일/런타임이 깨지지 않는 묶음 기준

### 4. 분기

- **단위가 1개**이면 즉시 권장 포맷으로 커밋한다.
- **단위가 2개 이상**이면 **커밋하지 않고** 사용자에게 분할안을 제시한다.

제시 형식:

```
다음과 같이 N개의 커밋으로 나눌 수 있습니다:

1. feat(hands): <설명>
   - Assets/Hands/Scripts/Foo.cs
   - Assets/Hands/Prefabs/Physics/LeftPhysicsHand.prefab
2. docs(git): <설명>
   - AGENTS.md
   - docs/agent-rules/git.md

이대로 진행할까요? 아니면 다른 단위로 묶을까요?
```

사용자가 승인하거나 조정안을 주면 그 지시대로 순차 커밋한다.

### 5. 커밋 메시지 포맷

```
<type>(<scope>): <description>
```

- 커밋 메시지는 기본적으로 한국어로 작성한다.

예시:
- `feat(VR): add teleport locomotion`
- `fix(scene): restore missing prefab reference`
- `docs(git): add branching guide`

scope는 생략 가능: `feat: add teleport locomotion`

## PR 절차

PR 제목은 커밋과 동일한 포맷을 권장한다.

```
<type>(<scope>): <description>
```

PR 본문은 반드시 루트의 `pull_request_template.md`를 따른다.

현재 기준 템플릿은 아래 순서를 사용한다.

```md
## 이슈 번호 #1
- close #1

## 작업 사항

## 기타
```

- PR 작성 시 템플릿의 섹션 제목과 구조를 유지한다.
- 이슈 번호와 `close #번호`는 실제 작업 이슈에 맞게 채운다.
- 템플릿이 변경되면 `pull_request_template.md`를 최신 기준으로 간주한다.

## 주의사항

- `.env`, 자격증명 파일, 대용량 바이너리는 스테이징에서 제외한다.
- 사용자가 명시 요청하지 않으면 `--no-verify`, `--amend`, `push --force`는 사용하지 않는다.
- 사용자가 명시하지 않으면 `git push`는 수행하지 않는다.
