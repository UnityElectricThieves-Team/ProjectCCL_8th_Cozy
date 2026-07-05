using System.IO;
using UnityEngine;

/// <summary>
/// StreamingAssets 하위 경로를 절대 경로로 변환하는 공용 헬퍼.
/// GameDataStore/GameDataCrypto와 달리 UnityEngine에 의존하므로 Tool(WPF)과는 공유하지 않는다(게임 전용).
/// </summary>
public static class StreamingAssetsPaths
{
    public static string GetPath(string relativePath) =>
        Path.Combine(Application.streamingAssetsPath, relativePath);
}
