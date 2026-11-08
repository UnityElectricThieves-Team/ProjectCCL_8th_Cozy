using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

/// <summary>
/// 유저 데이터(하트, 스폰 기운, 설정 등 플레이하며 쌓이는 모든 기록)의 저장/로드 지점.
/// 유저 데이터는 전부 이 클래스를 거친다 — 데이터 종류마다 저장 방식을 고민할 필요가 없다.
/// 에디터에서는 평문 JSON(메모장 디버깅·QA 상태 재현), 배포 빌드에서는 암호화로 저장한다.
/// 암호화는 키 추출·메모리 수정까지 막지는 못하지만, "메모장으로 10초 만에 고치는" 캐주얼 치트의 문턱을 높인다.
/// UNITY_EDITOR 분기를 쓰므로 게임 전용 — Tool(WPF)과 공유하지 않는다.
/// </summary>
public static class UserDataSaveIO
{
    public static void Save<T>(string filePath, T data)
    {
#if UNITY_EDITOR
        GameDataIO.SavePlain(filePath, data);
#else
        GameDataIO.SaveEncrypted(filePath, data);
#endif
    }

    public static T Load<T>(string filePath) where T : new()
    {
#if UNITY_EDITOR
        return GameDataIO.LoadPlain<T>(filePath);
#else
        // 배포 콘텐츠와 달리 유저 세이브는 손상 시 크래시 대신 리셋한다(LoadPlain과 같은 정책).
        // LoadEncrypted는 콘텐츠용이라 시끄럽게 던지므로 여기서 받아서 new T()로 전환한다.
        try
        {
            return GameDataIO.LoadEncrypted<T>(filePath);
        }
        catch (CryptographicException) { return new T(); }
        catch (JsonException) { return new T(); }
        catch (InvalidDataException) { return new T(); }
#endif
    }
}
