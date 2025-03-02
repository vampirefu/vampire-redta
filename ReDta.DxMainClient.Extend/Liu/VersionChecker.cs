using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DTAConfig.OptionPanels;

namespace ReDta.DxMainClient.Extend.Liu
{
    public class VersionChecker
    {
        public const string VersionUrl = "https://liuwentianhj.cn:8900/update.html";

        public const string ModVersion = "1.8.2";
        public string CurVersion { get { return ModVersion; } }

        public string GetLastVersion()
        {
            string version = CurVersion;
            var task = Task.Run(async () =>
            {
                // 创建一个HttpClient实例
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        // 指定要爬取的URL
                        string url = "https://www.example.com";

                        // 发送GET请求并获取响应
                        HttpResponseMessage response = await client.GetAsync(VersionUrl);

                        // 确保响应是成功的
                        response.EnsureSuccessStatusCode();

                        // 读取响应内容
                        string responseBody = await response.Content.ReadAsStringAsync();
                        string pattern = @"<p>\d+\.\d+\.\d+</p>";
                        var match = Regex.Match(responseBody, pattern);
                        if (match.Success)
                            version = match.Value.Replace(@"<p>", "").Replace(@"</p>", "");
                    }
                    catch (HttpRequestException e)
                    {
                    }
                }
            });
            task.GetAwaiter().GetResult();
            return version;
        }

        public bool HasUpdate(out string newVersion)
        {
            string oldVersion = CurVersion;
            newVersion = GetLastVersion();
            var oldSplits = oldVersion.Split('.');
            var newSplits = newVersion.Split('.');
            for (int i = 0; i < 3; i++)
            {
                if (int.Parse(oldSplits[i]) < int.Parse(newSplits[i]))
                    return true;
            }
            return false;
        }
    }
}
