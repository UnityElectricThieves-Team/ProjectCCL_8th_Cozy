using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CollectionTool
{
    /// <summary>
    /// 도감 항목 추가를 디스코드 웹훅으로 알린다. 웹훅 URL은 비밀값이므로 소스가 아니라
    /// gitignore된 GameData/discord_webhook.txt에서 읽는다(파일이 없으면 조용히 건너뛴다).
    /// </summary>
    internal static class DiscordNotifier
    {
        private static readonly HttpClient Client = new();

        public static async Task<bool> TrySendAsync(string message)
        {
            var webhookUrl = ReadWebhookUrl();
            if (string.IsNullOrWhiteSpace(webhookUrl)) return false;

            try
            {
                var payload = JsonConvert.SerializeObject(new { content = message });
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await Client.PostAsync(webhookUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string? ReadWebhookUrl()
        {
            var path = RepoPaths.DiscordWebhookConfigFilePath;
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
    }
}
