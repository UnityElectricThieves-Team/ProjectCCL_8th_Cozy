using System.IO;
using Newtonsoft.Json;

/// <summary>
/// 게임 데이터를 파일로 저장/로드하는 공용 입출력 유틸리티.
/// CollectionBoolData뿐 아니라 앞으로 추가될 다른 게임 데이터(설정, 인벤토리 등)도
/// 타입 파라미터만 바꿔서 그대로 재사용한다. Tool(WPF)과 게임(Unity)이 linked file로 공유한다.
///
/// Plain: 평문 JSON (사람이 읽고 수정 가능한 작업용 원본).
/// Encrypted: GameDataCrypto로 AES 암호화한 배포용 산출물.
///   포맷: [매직 4바이트 "CZDF"][포맷 버전 1바이트][암호문].
///   버전 바이트가 있어야 나중에 압축 추가·키 교체 같은 포맷 변경 시 기존 파일을 구분해 읽을 수 있다.
///   매직이 없는 파일은 헤더 도입 전 레거시(collection.dat 등)로 간주해 전체를 그대로 복호화한다.
/// </summary>
public static class GameDataIO
{
    private static readonly byte[] Magic = { (byte)'C', (byte)'Z', (byte)'D', (byte)'F' };
    private const byte FormatVersion = 1;

    public static void SavePlain<T>(string filePath, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        EnsureDirectoryExists(filePath);

        // 대상 파일을 직접 덮어쓰면 쓰기 도중 크래시 시 기존 저장본까지 파괴된다(torn write).
        // 임시 파일에 완성한 뒤 원자적으로 교체해 "직전 저장본" 아니면 "새 저장본"만 존재하게 한다.
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        ReplaceFile(tempPath, filePath);
    }

    public static T LoadPlain<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath))
        {
            return new T();
        }
        var json = File.ReadAllText(filePath);
        try
        {
            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }
        catch (JsonException)
        {
            // 손상된 유저 세이브는 리셋해서 게임이 계속 뜨게 한다(파일 없음과 동일 취급).
            // JsonException만 잡는 이유: IOException 같은 일시적 오류까지 리셋으로 처리하면
            // 멀쩡한 세이브를 새 데이터로 오인하고 다음 저장 때 덮어쓰는 사고가 난다.
            return new T();
        }
    }

    public static void SaveEncrypted<T>(string filePath, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.None);
        var cipherBytes = GameDataCrypto.Encrypt(json);
        EnsureDirectoryExists(filePath);

        var tempPath = filePath + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        {
            stream.Write(Magic, 0, Magic.Length);
            stream.WriteByte(FormatVersion);
            stream.Write(cipherBytes, 0, cipherBytes.Length);
        }
        ReplaceFile(tempPath, filePath);
    }

    public static T LoadEncrypted<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath))
        {
            return new T();
        }
        // 손상 시 LoadPlain처럼 리셋하지 않고 예외를 그대로 던진다(의도적).
        // 암호화 파일은 배포 콘텐츠(collection.dat)라서, 손상을 리셋으로 삼키면
        // 빈 도감으로 조용히 게임이 돌아가버린다 — 시끄럽게 실패하는 쪽이 맞다.
        var fileBytes = File.ReadAllBytes(filePath);
        var json = GameDataCrypto.Decrypt(ExtractCipherBytes(filePath, fileBytes));
        return JsonConvert.DeserializeObject<T>(json) ?? new T();
    }

    private static byte[] ExtractCipherBytes(string filePath, byte[] fileBytes)
    {
        if (!HasMagic(fileBytes))
        {
            return fileBytes; // 헤더 도입 전 레거시 파일: 전체가 암호문이다.
        }

        var version = fileBytes[Magic.Length];
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                $"지원하지 않는 게임 데이터 포맷 버전입니다. version={version}, file={filePath}");
        }

        var headerLength = Magic.Length + 1;
        var cipherBytes = new byte[fileBytes.Length - headerLength];
        System.Array.Copy(fileBytes, headerLength, cipherBytes, 0, cipherBytes.Length);
        return cipherBytes;
    }

    private static bool HasMagic(byte[] fileBytes)
    {
        if (fileBytes.Length < Magic.Length + 1)
        {
            return false;
        }
        for (var i = 0; i < Magic.Length; i++)
        {
            if (fileBytes[i] != Magic[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>임시 파일을 대상 경로로 원자적으로 교체한다. File.Replace는 대상이 없으면 실패하므로 첫 저장은 Move로 처리.</summary>
    private static void ReplaceFile(string tempPath, string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, null);
        }
        else
        {
            File.Move(tempPath, filePath);
        }
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
