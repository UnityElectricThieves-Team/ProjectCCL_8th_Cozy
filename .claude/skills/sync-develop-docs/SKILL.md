---
name: sync-develop-docs
description: origin/develop 의 새 커밋(문서/코드)을 현재 작업 브랜치로 머지한다. 자동으로 실행하지 않는다 — 사용자가 /sync-develop-docs 로 직접 호출하거나 develop 동기화를 요청할 때만 사용.
---

# sync-develop-docs

`origin/develop`에 쌓인 새 커밋을 현재 브랜치로 가져온다. 자동 merge는 절대 하지 않으며, 항상 변경 내역을 보여주고 사용자 확인을 받는다.

## 절차

1. **최신 상태 확인**
   - `git fetch origin develop`
   - `git rev-list --count HEAD..origin/develop` 로 뒤처짐 개수 확인.
   - 0개면 → "현재 브랜치는 origin/develop와 동기화되어 있습니다."라고 보고하고 **종료**.

2. **변경 내역 표시** (1개 이상일 때)
   - `git log --oneline HEAD..origin/develop` — 가져올 커밋 목록.
   - `git diff --stat HEAD...origin/develop` — 변경 파일 요약.
   - 현재 브랜치명도 함께 보여준다.

3. **사용자 확인 (Y/N)**
   - "origin/develop의 위 N개 커밋을 현재 브랜치(`<브랜치명>`)로 merge할까요? (Y/N)"
   - 거절 → 아무것도 하지 않고 종료.

4. **더티 트리 점검** (동의했을 때)
   - `git status --porcelain` 으로 미커밋 변경 확인.
   - 변경이 있으면 → 경고하고, 그대로 진행할지 다시 묻는다. (특히 `*.unity` / `*.prefab` / `ProjectSettings/*`가 더티면 충돌 위험이 크다고 안내.)

5. **머지 실행**
   - `git merge origin/develop` (일반 merge 커밋).
   - 성공 → 결과(머지된 커밋 수, 변경 파일) 보고.

6. **충돌 처리**
   - 충돌이 나면 충돌 파일 목록만 보고하고 **멈춘다**.
   - `git checkout --ours` / `--theirs` 로 한쪽을 통째로 버리지 **말 것**.
   - 자동으로 해결하려 하지 말고, 사용자가 직접 풀도록 안내. (`.claude/rules/git.md` 준수)

## 주의

- push는 하지 않는다. 머지까지만.
- 공유 브랜치(`develop`, `main`)로의 전파는 git.md의 합의 규칙을 따른다(이 스킬 범위 밖).