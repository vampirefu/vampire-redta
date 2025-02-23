using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Vampire.Common.Helpers;
using Vampire.ReDta.Login.Configs;
using ReDta.Login.Common;

namespace Vampire.ReDta.Login.Services
{
    public class LoginService
    {
        public static bool Login(string email, string password, out string msg)
        {
            string url = $"{CommonConfig.Server}/api/Auth/login";

            LoginDto loginDto = new LoginDto();
            loginDto.email = email;
            loginDto.password = password;

            string dtoStr = JsonSerializeHelper.JsonSerialize(loginDto);

            try
            {
                string ret = HttpHelper.Post(url, dtoStr);
                if (ret.Contains("token"))
                {
                    var backDto = JsonSerializeHelper.JsonDeserialize<LoginBackDto>(ret);
                    CommonInfo.Instance.Token = backDto?.token;

                    msg = "登录成功";
                    return true;
                }
                else
                    msg = ret;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
            }

            return false;
        }


        public static async Task CheckSession(Action errCallBack = null)
        {
            HttpClient client = new HttpClient();
            // 设置 API 的基础地址（请根据实际情况调整）
            client.BaseAddress = new Uri(CommonConfig.Server);

            // 设置 Bearer 认证头
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CommonInfo.Instance.Token);

            // 循环轮询，每隔 10 秒调用一次
            while (true)
            {
                try
                {
                    // 发送 GET 请求到 /api/auth/checksession 接口
                    HttpResponseMessage response = await client.GetAsync("/api/auth/checksession");
                    string json = await response.Content.ReadAsStringAsync();

                    // 注意：此处要求后端返回的 JSON 格式类似于 { "sessionId": "xxx" }
                    if (!json.Contains("sessionId"))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("调用接口时出现异常：" + ex.Message);
                }

                // 轮询间隔，例如 10 秒
                await Task.Delay(20 * 1000);
            }

            //MessageBox.Show("该账号已在其他地方登录");
            errCallBack?.Invoke();
        }
    }

}

[DataContract]
public class LoginDto
{
    [DataMember]
    public string email { get; set; }
    [DataMember]
    public string password { get; set; }
}


[DataContract]
public class LoginBackDto
{
    [DataMember]
    public string token { get; set; }
}

