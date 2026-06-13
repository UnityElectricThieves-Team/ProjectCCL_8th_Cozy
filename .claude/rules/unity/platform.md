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

### 책임 경계

- 게임 로직(별 클릭, 친밀도, 변신 등)이 **P/Invoke를 직접 호출하면 안 된다.** Platform/ 안에서 wrapper API를 노출하고, 게임 로직은 wrapper만 호출.
- Steam SDK 같은 외부 서비스는 Platform/에 두지 않는다 — OS가 아닌 서비스이므로 별도 모듈.

## 테스트 가능성

- 순수 로직(좌표 계산, NCHITTEST 매핑 등)은 `UnityEngine` 의존을 빼서 EditMode 테스트 가능하게.
- 예: `HitTestCalculator.cs`는 좌표/enum만 다루므로 Unity 없이 테스트 가능.

## 새 OS 통합 추가 체크리스트

1. `#if !UNITY_EDITOR` 가드 확인
2. OS 콜백이면 static 필드 보관
3. 콜백 → 메인 스레드 디스패치 패턴 확인
4. wrapper API를 게임 로직에 노출 (게임 로직이 P/Invoke 직접 호출하지 않도록)
