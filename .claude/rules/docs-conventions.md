---
paths:
  - "Docs/**/*.md"
  - "**/CLAUDE.md"
  - "README.md"
---

# 문서 작성 컨벤션

## 파일/폴더 명명

- **영문 사용** (한글 파일명 지양 — git 호환성).
- 각 단어 첫 글자 대문자, 단어 사이는 붙여서 (예: `GameDesign.md`, `CharacterDesign.md`).
- 본문은 한국어 OK, 파일명만 영문.

## 문서 영역

- `Docs/Planning/` — 기획
- `Docs/Development/` — 개발
- `Docs/Art/` — 아트

새 문서 추가 시 자신의 영역 폴더 안에 배치.

## CLAUDE.md 운영

이 프로젝트는 **루트 CLAUDE.md + 서브 디렉토리 CLAUDE.md + `.claude/rules/`** 세 층으로 컨텍스트를 분리한다.

### 루트 CLAUDE.md
- 프로젝트 개요, 폴더 구조, 진입점, 민감 영역 경고만.
- 항상 로드되므로 비대해지면 다른 정보가 묻힌다.

### 서브 디렉토리 CLAUDE.md
- 해당 디렉토리의 **책임·역할·신규 추가 시 컨벤션**.
- 단순 파일 목록이 아님.
- 한 디렉토리당 한 화면을 넘기지 않는 것이 이상적.
- 루트와 중복 금지.

### .claude/rules/
- **규칙**(do X / don't Y)만 담는다. 오리엔테이션이 아니다.
- `paths` frontmatter로 매칭되는 파일을 Read할 때만 로드된다.
- `paths` 없으면 CLAUDE.md처럼 항상 로드.

## 단일 진실 원천

- 동일 정보를 여러 문서에 중복 적지 않는다.
- 하위 폴더 동적 정보(파일 목록 등)는 그 폴더 CLAUDE.md에만.
- 루트 CLAUDE.md는 구조와 진입점만.

## 업데이트 의무

폴더 구조가 바뀌면 관련 CLAUDE.md / rules 파일을 함께 업데이트한다. **outdated된 문서는 잘못된 정보보다 위험하다.**
