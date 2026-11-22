using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows 로그인 시 이 게임을 자동으로 실행하게 등록·해제한다.
/// 현재 사용자의 시작 프로그램 레지스트리(HKCU\Software\Microsoft\Windows\CurrentVersion\Run)에
/// 실행 파일 경로를 값 하나로 적는 방식이다. 관리자 권한이 필요 없다.
///
/// 언제 부를지는 모른다 — 그 판단(설정이 켜졌는지)은 게임 쪽 적용자가 한다(Platform은 게임 로직을 모른다).
/// 둘 다 여러 번 불러도 결과가 같다: 등록은 덮어쓰고, 해제는 값이 없어도 조용히 지나간다.
///
/// <c>Microsoft.Win32.Registry</c> 대신 advapi32를 직접 부른다. 프로젝트의 API 호환 수준(.NET Standard 2.1)에는
/// 레지스트리 클래스가 보장되지 않고, 스크립팅 백엔드를 IL2CPP로 바꿔도 그대로 동작하게 하려는 것이다.
///
/// 빌드 전용 — Editor에서는 아무것도 하지 않는다. Editor 실행 파일(Unity.exe)을 시작 프로그램에 올리면 안 된다.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const uint REG_SZ = 1;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_FILE_NOT_FOUND = 2;
    private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegSetKeyValue(IntPtr hKey, string subKey, string valueName, uint type, string data, uint dataBytes);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegDeleteKeyValue(IntPtr hKey, string subKey, string valueName);

    // 시작 프로그램 목록(작업 관리자 등)에 보이는 이름이기도 하다.
    private static string ValueName => Application.productName;

    /// <summary>지금 실행 중인 실행 파일을 시작 프로그램으로 등록한다. 게임 폴더를 옮겼으면 새 경로로 덮어쓴다.</summary>
    public static void Register()
    {
#if !UNITY_EDITOR
        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        // 경로에 공백이 있으면 Windows가 첫 공백까지만 실행 파일로 읽으므로 따옴표로 감싼다.
        string command = "\"" + exePath + "\"";
        uint bytes = (uint)((command.Length + 1) * sizeof(char)); // 끝의 널 문자 포함
        int result = RegSetKeyValue(HKEY_CURRENT_USER, RunKey, ValueName, REG_SZ, command, bytes);
        if (result != ERROR_SUCCESS)
            Debug.LogWarning($"[{nameof(StartupRegistration)}] 자동 실행 등록 실패 (오류 {result})");
#endif
    }

    /// <summary>시작 프로그램 등록을 지운다. 등록돼 있지 않아도 괜찮다.</summary>
    public static void Unregister()
    {
#if !UNITY_EDITOR
        int result = RegDeleteKeyValue(HKEY_CURRENT_USER, RunKey, ValueName);
        if (result != ERROR_SUCCESS && result != ERROR_FILE_NOT_FOUND)
            Debug.LogWarning($"[{nameof(StartupRegistration)}] 자동 실행 해제 실패 (오류 {result})");
#endif
    }
}
