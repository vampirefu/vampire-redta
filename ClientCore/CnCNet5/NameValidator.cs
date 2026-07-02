using System;
using System.Linq;
namespace ClientCore.CnCNet5
{
    public static class NameValidator
    {
        /// <summary>
        /// 检查玩家昵称在 CnCNet 上是否有效。
        /// </summary>
        /// <returns>如果昵称有效则返回 null，否则返回说明名称问题的字符串。</returns>
        public static string IsNameValid(string name)
        {
            var profanityFilter = new ProfanityFilter();

            if (string.IsNullOrEmpty(name))
                return "请输入玩家名称.";

            if (profanityFilter.IsOffensive(name))
                return "请输入一个友好的名称.";

            if (int.TryParse(name.Substring(0, 1), out _))
                return "玩家名称首位不能是数字.";

            if (name[0] == '-')
                return "玩家名称首位不能是短线(-).";

            // 检查是否含有无效字符
            char[] allowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_[]|\\{}^`".ToCharArray();
            char[] nicknameChars = name.ToCharArray();

            foreach (char nickChar in nicknameChars)
            {
                if (!allowedCharacters.Contains(nickChar))
                {
                    return "玩家名称中含有无效字符." + Environment.NewLine +
                    "允许使用的字符为英文字母A-Z与数字.";
                }
            }

            if (name.Length > ClientConfiguration.Instance.MaxNameLength)
                return "您的名称过长.";

            return null;
        }

        /// <summary>
        /// 返回受最大允许长度约束且移除了离线昵称无效字符的玩家昵称。
        /// 不检查脏话或 CnCNet 的无效字符。
        /// </summary>
        /// <param name="name">玩家昵称。</param>
        /// <returns>移除了离线昵称无效字符并受最大名称长度约束的玩家昵称。</returns>
        public static string GetValidOfflineName(string name)
        {
            char[] disallowedCharacters = ",;".ToCharArray();

            string validName = new string(name.Trim().Where(c => !disallowedCharacters.Contains(c)).ToArray());

            if (validName.Length > ClientConfiguration.Instance.MaxNameLength)
                return validName.Substring(0, ClientConfiguration.Instance.MaxNameLength);
            
            return validName;
        }
    }
}
