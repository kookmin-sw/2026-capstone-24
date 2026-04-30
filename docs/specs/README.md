# Specs

이 폴더는 **Spec-Driven Development** 워크플로우의 진입점이다.
What/Why를 담는 얇은 **spec**과 How를 담는 실행 가능한 **plan**을 분리해서 관리한다.

## 폴더 구조

```
docs/specs/
├── README.md                       # 이 파일 — 인덱스 + 상태 보드
├── _templates/                     # 새 spec/plan 작성 시 베이스
│   ├── root-spec.md                # 큰 피처의 _index.md 템플릿
│   ├── sub-spec.md                 # 하위 spec 템플릿
│   └── plan.md                     # 구현 plan 템플릿
├── _archive/                       # 완료된 spec·plan 보관
│   └── <feature-kebab>/
│       ├── specs/
│       └── plans/
└── <feature-kebab>/                # 큰 피처 = 폴더 하나
    ├── _index.md                   # 루트 spec (What/Why + 하위 spec 링크)
    ├── specs/
    │   └── <sub-name>.md           # 하위 spec (각자 독립적인 What 단위)
    └── plans/
        └── NNN-<kebab>.md          # 구현 plan (전역 일련번호 NNN)
```

## 워크플로우

| 작업 | 명령 |
|---|---|
| 새 피처의 spec 시작 | `/spec-new` |
| 기존 spec에 plan 추가 | `/plan-new` |
| 작성된 plan대로 구현 | plan 파일 경로를 Claude에게 전달 (Linked Spec → parent `_index.md` 순으로 자동 컨텍스트 적재) |

## 작성 규칙 요약

- **Spec은 얇게.** What/Why만. 함수명·파일 경로·자료구조 같은 구현 디테일 금지.
- **방대하면 쪼갠다.** 한 spec에 무관한 피처를 섞지 않는다.
- **Plan은 self-contained.** 다른 plan이나 이전 세션 컨텍스트를 가정하지 않는다.
- **링크 양방향 유지.** sub-spec ↔ plan은 서로 링크되어야 한다.

자세한 규칙은 루트 [`AGENTS.md`](../../AGENTS.md)의 **Spec 시스템** 섹션 참고.

## 상태 보드

| Feature | Status | Sub-Specs | Plans (Done/Total) | 비고 |
|---|---|---|---|---|
| [rhythm-game](rhythm-game/_index.md) | Active | 7 | 5/5 | |

> Status 값: `Draft` / `Active` / `Done` / `Abandoned`
> 새 피처 추가 시 이 표에 행을 직접 갱신한다.
