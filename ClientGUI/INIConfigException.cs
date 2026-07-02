using System;

namespace ClientGUI
{
    /// <summary>
    /// 当INI数据无效时抛出的异常。
    /// </summary>
    public class INIConfigException : Exception
    {
        public INIConfigException(string message) : base(message)
        {
        }
    }
}
