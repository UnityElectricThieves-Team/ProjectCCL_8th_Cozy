using System.IO;
using Newtonsoft.Json;

/// <summary>
/// 게임 데이터를 파일로 저장/로드하는 공용 중앙 유틸리티.
/// CollectionBoolData뿐 아니라 앞으로 추가될 다른 게임 데이터(설정, 인벤토리 등)도
/// 타입 파라미터만 바꿔서 그대로 재사용한다. Tool(WPF)과 게임(Unity)이 linked file로 공유한다.
///
/// Plain: 평문 JSON (사람이 읽고 수정 가능한 작업용 원본).
/// Encrypted: GameDataCrypto로 AES 암호화한 배포용 산출물.
/// </summary>
public static class GameDataStore
{
    public static void SavePlain<T>(string filePath, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        EnsureDirectoryExists(filePath);
        File.WriteAllText(filePath, json);
    }

    public static T LoadPlain<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath))
        {
            return new T();
        }
        var json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<T>(json) ?? new T();
    }

    public static void SaveEncrypted<T>(string filePath, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.None);
        var cipherBytes = GameDataCrypto.Encrypt(json);
        EnsureDirectoryExists(filePath);
        File.WriteAllBytes(filePath, cipherBytes);
    }

    public static T LoadEncrypted<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath))
        {
            return new T();
        }
        var cipherBytes = File.ReadAllBytes(filePath);
        var json = GameDataCrypto.Decrypt(cipherBytes);
        return JsonConvert.DeserializeObject<T>(json) ?? new T();
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
