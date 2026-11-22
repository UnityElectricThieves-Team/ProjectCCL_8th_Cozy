---
paths:
  - "Project_Cozy/Assets/Scripts/Platform/**/*.cs"
---

# Platform/ 코드 작업 규칙

OS 의존 코드(Win32, WinAPI, DwmApi, LL Hook 등) 격리 레이어.
디렉토리 오리엔테이션은 `Project_Cozy/Assets/Scripts/Platform/CLAUDE.md` 참고. 이 파일은 **반드시 지켜야 하는 규칙**만 담는다.

## 절대 지켜야 할 것

### Editor 보호

- **Win32 호출은 Editor에서 절대 실행되지 않게 막는다.** Editor에서 실행되면 Unity Editor 자체의 창/입력이 망가진다. 한 번 망가지면 재시작 필요.
- 막는 방식은 둘 중 하나다.
  - **호출을 `#if !UNITY_EDITOR`로 감싼다.** 인스펙터 값 없이 OS를 부르는 코드(시작 프로그램 등록·모니터 목록 같은 static 클래스, LL 훅 컴포넌트 등)가 이 방식이다.
  - **OS 핸들을 얻는 한 곳만 `#if`로 막고, 나머지 Win32 경로는 핸들이 비었으면 멈춘다.** `WindowManager`가 이 방식이다 — HWND는 `TryAcquireHwnd`에서만 잡고, Editor에서는 HWND가 늘 비어 있어 어떤 Win32 경로도 실행되지 않는다.
- **인스펙터 값을 읽고 그에 따라 Win32를 부르는 컴포넌트는 두 번째 방식을 쓴다.** 판단 코드까지 `#if` 안에 넣으면 Editor 컴파일에서 그 코드가 빠진다. 그러면 인스펙터 값을 "쓰지 않는다"는 경고가 나고, 빌드에서만 나는 컴파일 오류는 빌드할 때까지 드러나지 않는다. 이 경고를 `#pragma`로 끄지 않는다 — 구조를 고친다.

### 델리게이트 GC 방지

- OS에 함수 포인터로 넘긴 콜백 델리게이트(`WndProc`, `LowLevelKeyboardProc` 등)는 필드에 붙잡아 둔다. GC가 거두면 OS가 부르는 순간 액세스 위반으로 게임이 꺼진다.
- **지킬 것은 하나다: OS에 넘긴 델리게이트마다 보관 칸을 하나 두고, OS가 그 델리게이트를 더는 부를 수 없을 때까지 그 칸에 다른 값을 넣지 않는다.** 칸을 덮어쓰면 OS가 아직 부를 수 있는 옛 델리게이트를 붙잡는 곳이 사라진다.
  - "더는 부를 수 없을 때"는 보통 등록을 푼 뒤다. 창 프로시저는 원래 프로시저로 되돌린 뒤에도 이미 보낸 메시지로 불릴 수 있으므로, 한 번 만든 델리게이트를 프로그램이 끝날 때까지 그대로 둔다(다시 설치할 때도 새로 만들지 않는다).
- static 칸은 클래스에 하나뿐이라 같은 클래스의 인스턴스들, 같은 베이스를 상속한 자식들이 함께 쓴다. 그래서 static 칸은 등록이 프로그램에 하나뿐이거나(창 프로시저), 한 번만 만들고 다시 대입하지 않을 때(`static readonly` — 부른 동안만 쓰는 열거 콜백처럼 여러 번 넘겨도 같은 델리게이트) 쓴다.
- 그 밖에 등록이 여럿일 수 있으면 인스턴스 칸에 두고, 그 객체가 등록을 다 풀 때까지 살아 있게 한다. 지금 코드가 이 원칙을 지키는 사용 조건은 [Platform/CLAUDE.md](../../../Project_Cozy/Assets/Scripts/Platform/CLAUDE.md)의 각 파일 설명에 있다.

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

### OutFocus 훅의 static 이벤트는 `BeforeSceneLoad` 이후에 구독한다

- OutFocus 훅(`OutFocusKeyHook`·`OutFocusMouseHook`)은 도메인 리로드를 끈 에디터를 위해 `SubsystemRegistration` 단계에서 자기 static 이벤트를 비운다.
- 그래서 `[RuntimeInitializeOnLoadMethod]`로 구독하는 static 클래스는 `BeforeSceneLoad` 이후 단계에서 구독한다. 같은 `SubsystemRegistration`에서 구독하면 클래스끼리의 실행 순서가 정해져 있지 않아, 훅이 나중에 돌면 구독이 지워진다.
- 이 실수는 빌드에서만 드러난다. 에디터에서는 훅 자체가 꺼져 있어(Editor 보호) 창 밖 입력이 원래 들어오지 않기 때문이다. 자기 상태의 초기화(지난 구독 정리 등)는 `SubsystemRegistration`에서 해도 된다.
- 컴포넌트가 `OnEnable`에서 구독하는 것은 이 문제와 무관하다.

### 책임 경계

- 게임 로직(구름 클릭, 친밀도, 변신 등)이 **P/Invoke를 직접 호출하면 안 된다.** Platform/ 안에서 wrapper API를 노출하고, 게임 로직은 wrapper만 호출.
- Steam SDK 같은 외부 서비스는 Platform/에 두지 않는다 — OS가 아닌 서비스이므로 별도 모듈.

## 테스트 가능성

- 순수 로직(좌표 계산, NCHITTEST 매핑 등)은 `UnityEngine` 의존을 빼서 EditMode 테스트 가능하게.
- 예: `HitTestCalculator.cs`는 좌표/enum만 다루므로 Unity 없이 테스트 가능.

## 새 OS 통합 추가 체크리스트

1. Editor에서 Win32가 실행되지 않는지 확인 (위 "Editor 보호"의 두 방식 중 하나)
2. OS 콜백이면 위 "델리게이트 GC 방지"대로 보관
3. 콜백 → 메인 스레드 디스패치 패턴 확인
4. wrapper API를 게임 로직에 노출 (게임 로직이 P/Invoke 직접 호출하지 않도록)
