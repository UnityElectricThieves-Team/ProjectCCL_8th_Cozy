using UnityEngine.InputSystem;

/// <summary>
/// Win32 가상 키코드(Virtual-Key Code) → <see cref="Key"/> 매핑.
/// 저수준 키보드 훅(WH_KEYBOARD_LL)이 넘겨주는 vkCode를 게임 코드가 다루는 <see cref="Key"/>로 정규화한다.
///
/// 자주 쓰는 키(문자/숫자/F키/넘패드/방향키/모디파이어/편집키)만 다룬다. 매핑되지 않은 코드는 <see cref="Key.None"/>.
/// <see cref="UnityEngine"/> 의존이 없어 EditMode 테스트가 가능하다 (Platform/CLAUDE.md 컨벤션).
/// </summary>
public static class Win32KeyMap
{
    public static Key ToKey(int vkCode)
    {
        // 연속 구간은 enum 산술로 처리 (해당 Key 값들이 연속이라는 전제 — Unity InputSystem 기준).
        if (vkCode >= 0x41 && vkCode <= 0x5A) return Key.A + (vkCode - 0x41);          // 'A'..'Z'
        if (vkCode >= 0x31 && vkCode <= 0x39) return Key.Digit1 + (vkCode - 0x31);      // '1'..'9'  (Digit0은 Digit9 뒤라 별도)
        if (vkCode == 0x30)                   return Key.Digit0;                          // '0'
        if (vkCode >= 0x60 && vkCode <= 0x69) return Key.Numpad0 + (vkCode - 0x60);      // VK_NUMPAD0..9
        if (vkCode >= 0x70 && vkCode <= 0x7B) return Key.F1 + (vkCode - 0x70);           // VK_F1..F12

        switch (vkCode)
        {
            case 0x08:            return Key.Backspace;   // VK_BACK
            case 0x09:            return Key.Tab;         // VK_TAB
            case 0x0D:            return Key.Enter;       // VK_RETURN
            case 0x1B:            return Key.Escape;      // VK_ESCAPE
            case 0x20:            return Key.Space;       // VK_SPACE
            case 0x14:            return Key.CapsLock;    // VK_CAPITAL
            case 0x90:            return Key.NumLock;     // VK_NUMLOCK
            case 0x91:            return Key.ScrollLock;  // VK_SCROLL

            case 0x10: case 0xA0: return Key.LeftShift;   // VK_SHIFT / VK_LSHIFT
            case 0xA1:            return Key.RightShift;  // VK_RSHIFT
            case 0x11: case 0xA2: return Key.LeftCtrl;    // VK_CONTROL / VK_LCONTROL
            case 0xA3:            return Key.RightCtrl;   // VK_RCONTROL
            case 0x12: case 0xA4: return Key.LeftAlt;     // VK_MENU / VK_LMENU
            case 0xA5:            return Key.RightAlt;    // VK_RMENU
            case 0x5B:            return Key.LeftMeta;    // VK_LWIN
            case 0x5C:            return Key.RightMeta;   // VK_RWIN
            case 0x5D:            return Key.ContextMenu; // VK_APPS

            case 0x25:            return Key.LeftArrow;   // VK_LEFT
            case 0x26:            return Key.UpArrow;     // VK_UP
            case 0x27:            return Key.RightArrow;  // VK_RIGHT
            case 0x28:            return Key.DownArrow;   // VK_DOWN

            case 0x21:            return Key.PageUp;      // VK_PRIOR
            case 0x22:            return Key.PageDown;    // VK_NEXT
            case 0x23:            return Key.End;         // VK_END
            case 0x24:            return Key.Home;        // VK_HOME
            case 0x2D:            return Key.Insert;      // VK_INSERT
            case 0x2E:            return Key.Delete;      // VK_DELETE

            default:              return Key.None;
        }
    }
}