using Assets.Scripts.Contents.CollectionSystem.Model;

/// <summary>
/// 게임 런타임에서 CollectionTool이 내보낸 collection.dat를 불러오는 진입점.
/// 경로는 GameDataPaths가 관리하며, CollectionTool의 RepoPaths.ExportedGameDataFilePath와 짝을 이룬다.
/// </summary>
public static class CollectionDataRuntime
{
    public static CollectionBoolData Load() =>
        CollectionDataStorage.LoadEncrypted(GameDataPaths.CollectionData);
}
