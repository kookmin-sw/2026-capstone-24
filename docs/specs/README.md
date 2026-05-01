# Specs

이 디렉터리는 **Spec-Driven Development** 워크플로우의 진입점이다.  
What/Why를 담는 얇은 **spec**과 How를 담는 실행 가능한 **plan**을 분리해 관리한다.

## 디렉터리 구조

```text
docs/specs/
├─ README.md
├─ _templates/
├─ _archive/
└─ <feature-kebab>/
   ├─ _index.md
   ├─ specs/
   └─ plans/
```

## 워크플로우

| 작업 | 명령 |
|---|---|
| 새 루트 spec 시작 | `/spec-new` |
| 기존 spec의 open question 정리 | `/spec-resolve <spec-path>` |
| 기존 spec에 plan 추가 | `/plan-new <spec-path>` |
| 저장된 plan 구현 | `/spec-implement <plan-path>` |

## 파일명 규칙

- sub-spec: `docs/specs/<feature>/specs/<NN>-<sub-name>.md`
- plan: `docs/specs/<feature>/plans/<YYYY-MM-DD>-<author>-<slug>.md`

## 작성 규칙 요약

- spec은 What/Why 중심으로 짧게 쓴다.
- plan은 self-contained하게 작성한다.
- spec, plan, index 간 링크를 항상 맞춘다.
- acceptance criteria에는 `[auto-hard]`, `[auto-soft]`, `[manual-hard]` 중 하나를 붙인다.

## Plan 실행 시 읽기 순서

1. Plan 파일
2. Linked Spec
3. Parent `_index.md`
4. 구현 시작

## 상태 보드

| Feature | Status | Sub-Specs | Plans (Done/Total) | 비고 |
|---|---|---|---|---|
| [rhythm-game](rhythm-game/_index.md) | Active | 6 | 5/5 | |
| [hands](hands/_index.md) | Active | 2 | 1/1 | |
| [multiplayer-network](multiplayer-network/_index.md) | Active | 4 | 6/8 | |

> Status 값은 `Draft` / `Active` / `Done` / `Abandoned`
