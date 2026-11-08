---
paths:
  - "Project_Cozy/ProjectSettings/**"
---

# ProjectSettings 작업 규칙

## 절대 금지

- **Claude는 `ProjectSettings/*.asset` 파일을 직접 텍스트 편집하지 않는다.** Unity 내부 직렬화 포맷이며 잘못 손대면 프로젝트가 열리지 않을 수 있다.
- 변경이 필요하면 Unity Editor에서 사용자가 직접 수정하도록 안내한다.

## 동시 작업 위험

`Project_Cozy/ProjectSettings/*` 전체는 머지 충돌 다발 영역이다.

- 다른 작업과 함께 묶이지 않도록 별도 커밋으로 분리.

## 의도치 않은 변경 점검

다른 작업을 한 뒤에도 `ProjectSettings/` 파일들이 변경되어 있는 경우가 있다 (Editor가 자동으로 건드림).

- 커밋 전 `git status`로 의도치 않은 ProjectSettings 변경 확인.
- 의도하지 않은 변경이면 `git checkout -- Project_Cozy/ProjectSettings/<file>`로 되돌릴 것.

## 자주 변하는 파일

- `EditorBuildSettings.asset` — 씬 추가/제거 시 변경됨
- `ProjectSettings.asset` — Player Settings, Quality 등 변경 시
- `URP*GraphicsSettings.asset` — 렌더 파이프라인 설정 변경 시
- `InputManager.asset` — Input System과 무관한 레거시 입력 설정. New Input System 사용 중이므로 변경되면 의도된 건지 의심할 것.
