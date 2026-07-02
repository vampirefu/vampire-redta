using Rampastring.Tools;

namespace ClientCore.Settings
{
    /// <summary>
    /// INI 设置的基类。
    /// </summary>
    public abstract class INISetting<T> : IIniSetting
    {
        public INISetting(IniFile iniFile, string iniSection, string iniKey,
            T defaultValue)
        {
            IniFile = iniFile;
            IniSection = iniSection;
            IniKey = iniKey;
            DefaultValue = defaultValue;
        }

        public static implicit operator T(INISetting<T> iniSetting)
        {
            return iniSetting.Get();
        }

        public void SetIniFile(IniFile iniFile)
        {
            IniFile = iniFile;
        }

        protected IniFile IniFile { get; private set; }
        protected string IniSection { get; private set; }
        protected string IniKey { get; private set; }
        protected T DefaultValue { get; private set; }

        public T Value
        {
            get { return Get(); }
            set { Set(value); }
        }

        /// <summary>
        /// 如果 INI 文件中当前未指定此设置的值，则将默认值写入 INI 文件。
        /// </summary>
        public void SetDefaultIfNonexistent()
        {
            if (!IniFile.KeyExists(IniSection, IniKey))
                Set(DefaultValue);
        }

        protected abstract T Get();

        protected abstract void Set(T value);

        public abstract void Write();
    }
}
