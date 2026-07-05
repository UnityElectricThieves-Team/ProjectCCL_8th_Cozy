using System;
using System.IO;

namespace CollectionTool
{
    /// <summary>
    /// 저장소 루트를 실행 위치 기준으로 찾아서 Tool/게임 쪽 데이터 경로를 계산한다.
    /// Debug/Release, 실행 폴더 위치가 달라져도 "Project_Cozy"와 "Tool" 폴더가 같이 있는 지점을 루트로 인식한다.
    /// </summary>
    internal static class RepoPaths
    {
        public static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "Project_Cozy")) &&
                        Directory.Exists(Path.Combine(dir.FullName, "Tool")))
                    {
                        return dir.FullName;
                    }
                    dir = dir.Parent;
                }
                throw new DirectoryNotFoundException("저장소 루트(Project_Cozy, Tool 폴더를 포함하는 상위 폴더)를 찾을 수 없습니다.");
            }
        }

        // 작업용 원본(평문, 스포일러 포함) - git에 커밋되지 않는 위치.
        public static string MasterDataFilePath =>
            Path.Combine(RepoRoot, "Tool", "CCLCozyGameTool", "GameData", "collection_master.json");

        // 게임에 실제로 들어가는 배포용 산출물(암호문) - StreamingAssets에 커밋 가능.
        public static string ExportedGameDataFilePath =>
            Path.Combine(RepoRoot, "Project_Cozy", "Assets", "StreamingAssets", "CollectionData", "collection.dat");

        // 디스코드 웹훅 URL 설정 파일 - GameData와 같이 git에 커밋되지 않는 위치(웹훅 URL은 사실상 비밀값).
        public static string DiscordWebhookConfigFilePath =>
            Path.Combine(RepoRoot, "Tool", "CCLCozyGameTool", "GameData", "discord_webhook.txt");
    }
}
