using System.IO;
using UnityEngine;

/// <summary>
/// 게임이 다루는 모든 데이터 파일 경로를 한 곳에서 관리하는 중앙 레지스트리.
/// 경로는 성격에 따라 두 종류로 나뉘며, 새 데이터 파일을 추가할 때 반드시 올바른 쪽에 두어야 한다.
///
/// 1) 배포 데이터 (읽기 전용, 암호화): StreamingAssets 하위. Tool이 만들어 빌드에 포함되는 산출물.
///    설치 폴더에 위치하므로 게임이 여기에 쓰기를 시도하면 안 된다(권한 없음, 업데이트 시 덮어써짐).
/// 2) 런타임 세이브 (쓰기, 평문 JSON): persistentDataPath 하위. 유저 진행 상황.
///    설치/업데이트와 무관하게 유지되어야 하므로 반드시 이쪽에 쓴다.
///    유저 데이터는 암호화하지 않는다 — 싱글 게임이라 치트 방지 실익이 없고, 디버깅/복구 편의가 더 크다.
///
/// UnityEngine에 의존하므로 Tool(WPF)과 공유하지 않는다(게임 전용). Tool 쪽 경로는 RepoPaths가 담당.
/// </summary>
public static class GameDataPaths
{
    // ── 배포 데이터 (읽기 전용, 암호화) ──
    public static string CollectionData => StreamingAssetsPaths.GetPath("CollectionData/collection.dat");

    // ── 런타임 세이브 (쓰기, 평문 JSON) ──
    private static string SaveRoot => Path.Combine(Application.persistentDataPath, "SaveData");
    public static string SpawnEnergy => Path.Combine(SaveRoot, "spawnEnergy.json");
    public static string Hearts => Path.Combine(SaveRoot, "hearts.json");
}
