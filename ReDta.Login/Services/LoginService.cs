using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Vampire.Common.Helpers;
using Vampire.ReDta.Login.Configs;

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
    }

    [DataContract]
    public class LoginDto
    {
        [DataMember]
        public string email { get; set; }
        [DataMember]
        public string password { get; set; }
    }
}
