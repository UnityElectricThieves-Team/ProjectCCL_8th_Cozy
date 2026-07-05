using Assets.Scripts.Contents.CollectionSystem.Model;

/// <summary>
/// 게임 런타임에서 CollectionTool이 내보낸 collection.dat를 불러오는 진입점.
/// StreamingAssets/CollectionData/collection.dat 경로는 CollectionTool의 RepoPaths.ExportedGameDataFilePath와 짝을 이룬다.
/// </summary>
public static class CollectionDataRuntime
{
    private const string RelativePath = "CollectionData/collection.dat";

    public static CollectionBoolData Load() =>
        CollectionDataStorage.LoadEncrypted(StreamingAssetsPaths.GetPath(RelativePath));
}
