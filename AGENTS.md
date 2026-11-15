# AGENTS.md

Codex의 진입 파일입니다. 이 저장소의 공통 원칙은 루트의 `AI_INSTRUCTIONS.md`에 있고, 이 파일에는 Codex에만 해당하는 것만 둡니다. 왜 이렇게 나눴는지는 `AI_INSTRUCTIONS.md` §4에 있습니다.

## 작업 전에 읽을 것

1. **루트 `AI_INSTRUCTIONS.md`를 먼저 끝까지 읽고 따릅니다.** 프로젝트 개요, 새로 만들 때의 규칙, 코드 스타일, 건드리면 안 되는 것이 모두 여기 있습니다.
2. **세부 규칙은 `.claude/rules/`에 있습니다.** Codex는 이 규칙들을 자동으로 불러오지 않습니다. `AI_INSTRUCTIONS.md` §4의 rules 인덱스를 보고, `paths`가 없다고 적힌 규칙은 항상 읽습니다. 나머지는 다루는 파일이 그 규칙 파일 머리의 `paths`에 걸릴 때 직접 열어 읽습니다. 새 파일을 만들 때도, 만들 파일이 걸릴 규칙을 먼저 읽습니다.
3. **폴더별 안내는 그 폴더의 `CLAUDE.md`에 있습니다.** 이름은 Claude Code 관례지만 내용은 공통입니다. 그 폴더의 파일을 다루기 전에 직접 열어 읽습니다. 목록은 `AI_INSTRUCTIONS.md` §4에 있습니다.

## 문서 속 "Claude"

문서와 규칙의 "Claude"는 이 저장소에서 일하는 AI 작업자를 뜻하고, Codex도 여기에 듭니다. 파일 등급(`asset-editing.md`)이나 워크트리·씬 작업 규칙(`git.md`)도 그대로 지킵니다. Codex에 해당하지 않는 것은 도구 이름이 붙은 지시뿐입니다. `mcp__*` 도구나 `claude -w` 명령이 그런 예입니다. Figma를 읽는 방법은 `.claude/rules/figma.md`를 보고, 그 도구가 없으면 사용자에게 어떻게 할지 묻습니다.
