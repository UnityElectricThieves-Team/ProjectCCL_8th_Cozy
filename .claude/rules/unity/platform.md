---
paths:
  - "Project_Cozy/Assets/Scripts/Platform/**/*.cs"
---

# Platform/ 코드 작업 규칙

OS 의존 코드(Win32, WinAPI, DwmApi, LL Hook 등) 격리 레이어.
디렉토리 오리엔테이션은 `Project_Cozy/Assets/Scripts/Platform/CLAUDE.md` 참고. 이 파일은 **반드시 지켜야 하는 규칙**만 담는다.

## 절대 지켜야 할 것

### Editor 보호

- **Win32 호출은 반드시 `#if !UNITY_EDITOR` 가드 안에서.**
- Editor에서 실행되면 Unity Editor 자체의 창/입력이 망가진다. 한 번 망가지면 재시작 필요.

### 델리게이트 GC 방지

- OS에 함수 포인터로 넘기는 콜백(`WndProc`, `LowLevelKeyboardProc` 등)은 **static 필드**에 보관한다.
- 인스턴스 필드만 두면 GC 수거 후 OS가 함수 호출 시점에 액세스 위반.

### 콜백 스레드 주의

- `WndProc` / LL Hook 콜백은 메시지 펌프 스레드에서 호출될 수 있다.
- 콜백 내부에서 `UnityEngine` API 직접 호출 금지.
- 패턴: 콜백에서 `ConcurrentQueue`로 enqueue → 메인 스레드 `Update`에서 dequeue.

### 입력 소스는 누른 순간만 알린다

- 키 입력을 방송하는 소스는 "안 눌림→눌림"일 때만 알린다. 키를 꾹 눌러 생기는 OS 자동 반복은 보내지 않는다.
- 이유: 기획(Figma 스폰 기운 표)이 "꾹 눌러도 1회"이고, 창 안 경로(`InputSystem.onAnyButtonPress`)와 마우스 훅이 이미 누른 순간만 알린다. 소스 하나만 반복을 섞으면 같은 "눌림" 이벤트가 경로마다 다른 뜻이 된다.
- 반복은 소비자가 아니라 출처(훅)에서 거른다. 소비자마다 거르면 한 곳이 빠진다.
- LL 훅은 받은 메시지에 반복 표시가 없으므로 눌린 키 상태를 직접 들고, 그 상태는 훅 스레드에서만 고친다. keyup을 놓치면 그 키를 1회 덜 셀 수 있지만 덜 세는 쪽으로만 틀리므로 받아들인다.
- 아직 이 약속을 따르지 않는 소스가 있다 — [Platform/CLAUDE.md](../../../Project_Cozy/Assets/Scripts/Platform/CLAUDE.md)의 Input 절 참조.

### 책임 경계

- 게임 로직(구름 클릭, 친밀도, 변신 등)이 **P/Invoke를 직접 호출하면 안 된다.** Platform/ 안에서 wrapper API를 노출하고, 게임 로직은 wrapper만 호출.
- Steam SDK 같은 외부 서비스는 Platform/에 두지 않는다 — OS가 아닌 서비스이므로 별도 모듈.

## 테스트 가능성

- 순수 로직(좌표 계산, NCHITTEST 매핑 등)은 `UnityEngine` 의존을 빼서 EditMode 테스트 가능하게.
- 예: `HitTestCalculator.cs`는 좌표/enum만 다루므로 Unity 없이 테스트 가능.

## 새 OS 통합 추가 체크리스트

1. `#if !UNITY_EDITOR` 가드 확인
2. OS 콜백이면 static 필드 보관
3. 콜백 → 메인 스레드 디스패치 패턴 확인
4. wrapper API를 게임 로직에 노출 (게임 로직이 P/Invoke 직접 호출하지 않도록)
