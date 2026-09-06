using System.IO;
using UnityEngine;

/// <summary>
/// 게임이 다루는 모든 데이터 파일 경로를 한 곳에서 관리하는 중앙 레지스트리.
/// 경로는 성격에 따라 두 종류로 나뉘며, 새 데이터 파일을 추가할 때 반드시 올바른 쪽에 두어야 한다.
///
/// 1) 배포 데이터 (읽기 전용, 암호화): StreamingAssets 하위. Tool이 만들어 빌드에 포함되는 산출물.
///    설치 폴더에 위치하므로 게임이 여기에 쓰기를 시도하면 안 된다(권한 없음, 업데이트 시 덮어써짐).
/// 2) 유저 데이터 (쓰기): persistentDataPath 하위. 플레이하며 쌓이는 모든 기록(하트, 스폰 기운, 설정 등).
///    설치/업데이트와 무관하게 유지되어야 하므로 반드시 이쪽에 쓴다.
///    전부 UserDataSaveIO 경유 — 에디터 평문(.json), 배포 암호화(.dat).
///    캐주얼 치트(메모장 수정)의 문턱을 높이되, 개발 중 디버깅 편의는 유지하기 위함.
///
/// UnityEngine에 의존하므로 Tool(WPF)과 공유하지 않는다(게임 전용). Tool 쪽 경로는 RepoPaths가 담당.
/// </summary>
public static class GameDataPaths
{
    // ── 배포 데이터 (읽기 전용, 암호화) ──
    public static string CollectionData => StreamingAssetsPaths.GetPath("CollectionData/collection.dat");

    // ── 유저 데이터 (쓰기) ──
    private static string SaveRoot => Path.Combine(Application.persistentDataPath, "SaveData");

    // 저장 방식이 모드에 따라 갈리므로 확장자도 함께 갈린다 (UserDataSaveIO와 짝).
#if UNITY_EDITOR
    private const string UserDataExtension = ".json";
#else
    private const string UserDataExtension = ".dat";
#endif
    public static string SpawnEnergy => Path.Combine(SaveRoot, "spawnEnergy" + UserDataExtension);
    public static string Hearts => Path.Combine(SaveRoot, "hearts" + UserDataExtension);
    public static string ShopInventory => Path.Combine(SaveRoot, "shopInventory" + UserDataExtension);
    public static string Backgrounds => Path.Combine(SaveRoot, "backgrounds" + UserDataExtension);
    public static string Viewport => Path.Combine(SaveRoot, "viewport" + UserDataExtension);
    public static string Settings => Path.Combine(SaveRoot, "settings" + UserDataExtension);
}
