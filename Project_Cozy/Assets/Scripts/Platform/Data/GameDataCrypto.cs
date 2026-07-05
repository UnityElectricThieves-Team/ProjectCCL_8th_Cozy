using System.IO;
using System.Security.Cryptography;

/// <summary>
/// 게임 데이터 저장 파일을 평문으로 노출하지 않기 위한 AES 암복호화.
/// 특정 데이터 종류에 종속되지 않는 공용 유틸리티 — GameDataStore가 이 클래스 하나로 모든 데이터를 암복호화한다.
/// Tool(WPF)과 게임(Unity)이 동일한 로직을 공유해야 하므로 Tool 프로젝트에 linked file로도 참조된다.
/// 리버스 엔지니어링을 완전히 막지는 못하지만(키가 코드에 내장됨), 평문 텍스트로 열람/데이터마이닝 되는 것은 막는다.
/// </summary>
public static class GameDataCrypto
{
    private static readonly byte[] Key =
    {
        2,233,185,111,11,91,58,39,26,96,79,124,208,19,102,204,
        185,154,172,129,37,92,188,120,26,39,159,50,156,92,168,108
    };

    private static readonly byte[] IV =
    {
        116,255,248,60,164,44,88,64,67,4,118,212,131,45,191,166
    };

    public static byte[] Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;

        using var ms = new MemoryStream();
        using (var cryptoStream = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cryptoStream))
        {
            writer.Write(plainText);
        }
        return ms.ToArray();
    }

    public static string Decrypt(byte[] cipherBytes)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;

        using var ms = new MemoryStream(cipherBytes);
        using var cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var reader = new StreamReader(cryptoStream);
        return reader.ReadToEnd();
    }
}
