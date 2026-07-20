---
paths:
  - "**/*.unity"
---

# Unity 씬(.unity) 작업 규칙

## 머지 충돌 주의

`.unity` 파일은 텍스트(YAML)지만 Unity 내부 ID와 순서가 엮여 있어 **두 사람이 동시에 만지면 머지가 거의 항상 깨진다.**

- Claude는 씬 파일을 **직접 텍스트로 편집하지 않는다.** YAML을 직접 수정하면 ID 깨짐, 컴포넌트 참조 끊김이 발생한다.
- 씬에 변경이 필요하면 사용자에게 Unity Editor에서 직접 수정하도록 안내한다.
- 단, 단순 검색(어떤 컴포넌트가 붙어있는지 확인)을 위해 grep으로 읽는 것은 OK.

## 새 씬 추가

- `Project_Cozy/Assets/Scenes/` 아래에 추가.
- Build Settings에 등록 필요 (`ProjectSettings/EditorBuildSettings.asset` 변경됨 — Git 충돌 주의).

## 커밋

- 씬 변경 커밋은 prefix `scene` 권장.
- 씬 변경 시 동반되는 `.meta` 파일도 함께 커밋. `.meta`만 빠지면 다른 사람 환경에서 GUID 재할당이 일어난다.
