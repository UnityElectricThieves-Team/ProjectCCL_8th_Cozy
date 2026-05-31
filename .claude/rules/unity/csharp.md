---
paths:
  - "**/*.cs"
---

# C# / Unity 스크립트 규칙

## 코딩 스타일

- **Namespace 미사용.** 팀 컨벤션에 따라 글로벌 namespace 유지.
- 클래스명·메서드명은 `PascalCase`, 필드·로컬은 `camelCase`.
- 새 파일 추가 시 같은 폴더의 기존 파일 스타일을 그대로 따른다 (인덴트, 중괄호 위치 등).

## Unity API 사용 시

- **`Update()` 안에서 `GetComponent` / `Find` 계열 호출 금지.** Awake/Start에서 캐시.
- `Instantiate` / `Destroy`는 GC 압박이 크다 — 자주 생성/파괴되는 객체는 풀링 검토.
- 코루틴 사용 시 종료 조건 명시. 무한 코루틴은 컴포넌트 비활성화 시 자동 종료되지 않는 케이스 주의.

## 데스크톱 펫 특성 주의

이 프로젝트는 바탕화면에 항상 떠 있는 게임이다. **리소스 점유율이 곧 사용자 경험**이다.

- 매 프레임 할당이 발생하는 패턴(LINQ 체인, 문자열 연결 등) 피하기.
- 불필요한 `Debug.Log`는 빌드에서 제거하거나 `[Conditional]`로 가드.
- 항상 떠 있어야 하므로 메모리 누수에 특히 주의. 이벤트 구독은 반드시 해제.

## 테스트 가능성

- Unity API 의존 없이 풀 수 있는 순수 로직(좌표 계산, 상태 머신 등)은 `UnityEngine` 의존을 빼서 EditMode 테스트가 가능하게.
- 예: `Project_Cozy/Assets/Scripts/Platform/Window/HitTestCalculator.cs`.

## 새 스크립트 추가 시

- 컨트롤러·AI·친밀도 등 게임 로직은 `Project_Cozy/Assets/Scripts/` 아래에.
- OS 의존 코드(Win32 / WinAPI / P/Invoke)는 **반드시** `Project_Cozy/Assets/Scripts/Platform/`에 (자세한 규칙은 platform.md).
- 캐릭터 자산 폴더(`Assets/Characters/`)에 스크립트를 두지 않는다.
