namespace Assets.Scripts.Contents.CollectionSystem.Model
{
    /// <summary>
    /// CollectionBoolData의 저장/로드 지점. 실제 직렬화·암호화는 공용 GameDataStore(Platform/Data)에 위임한다.
    /// Tool과 게임이 같은 포맷을 써야 하므로 이 파일도 linked file로 공유한다.
    /// </summary>
    public static class CollectionDataStorage
    {
        public static void SavePlain(string filePath, CollectionBoolData data) =>
            GameDataStore.SavePlain(filePath, data);

        public static CollectionBoolData LoadPlain(string filePath) =>
            GameDataStore.LoadPlain<CollectionBoolData>(filePath);

        public static void SaveEncrypted(string filePath, CollectionBoolData data) =>
            GameDataStore.SaveEncrypted(filePath, data);

        public static CollectionBoolData LoadEncrypted(string filePath) =>
            GameDataStore.LoadEncrypted<CollectionBoolData>(filePath);
    }
}
