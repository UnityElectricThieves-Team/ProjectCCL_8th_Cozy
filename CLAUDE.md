# CLAUDE.md

Claude Code의 진입 파일입니다. 이 저장소의 공통 원칙은 아래에서 불러오는 `AI_INSTRUCTIONS.md`에 있고, 이 파일에는 Claude Code에만 해당하는 것만 둡니다. 왜 이렇게 나눴는지는 `AI_INSTRUCTIONS.md` §4에 있습니다.

@AI_INSTRUCTIONS.md

---

## Claude Code에서 문서가 로드되는 방식

- **세션 시작:** 이 파일과, 위에서 `@`로 불러온 `AI_INSTRUCTIONS.md`, 그리고 `paths`가 없는 rules 파일이 자동으로 들어옵니다.
- **`.claude/rules/`:** `paths`가 있는 규칙은 그 글로브에 걸리는 파일을 **도구로 열 때** 자동으로 들어옵니다. 세션 시작에 자동으로 주입되는 문서는 `paths` 매칭을 발화시키지 않습니다. 그래서 새로 만드는 순간처럼 열 파일이 없을 때 필요한 규칙은 `AI_INSTRUCTIONS.md`에 한 줄 둡니다(§4의 함정).
- **서브 디렉토리 `CLAUDE.md`:** 그 폴더의 파일을 열 때 자동으로 들어옵니다.
- **`@` 불러오기는 줄 맨 앞에 백틱 없이 씁니다.** 코드 표기 안의 `@`는 불러오지 않습니다.

## Claude Code 전용 도구

- Figma는 사용자 브라우저의 `mcp__claude-in-chrome__*` 도구로만 읽습니다. 절차는 [.claude/rules/figma.md](.claude/rules/figma.md)에 있습니다.
- 워크트리 세션(`claude -w`, `.claude/worktrees/`)의 운영 방식은 [.claude/rules/git.md](.claude/rules/git.md)에 있습니다.
