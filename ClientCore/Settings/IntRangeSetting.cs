using Rampastring.Tools;

namespace ClientCore.Settings
{
    /// <summary>
    /// 类似于 IntSetting，此设置在获取和设置时强制执行最小值和最大值限制。
    /// </summary>
    public class IntRangeSetting : IntSetting
    {
        private readonly int MinValue;
        private readonly int MaxValue;

        public IntRangeSetting(IniFile iniFile, string iniSection, string iniKey, int defaultValue, int minValue, int maxValue) : base(iniFile, iniSection, iniKey, defaultValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        /// <summary>
        /// 检查值的有效性。如果值无效，则返回此设置的默认值；
        /// 否则返回设置的值。
        /// </summary>
        /// <param name="value">要检查的值。</param>
        /// <returns>规范化后的值。</returns>
        private int NormalizeValue(int value)
        {
            return InvalidValue(value) ? DefaultValue : value;
        }

        private bool InvalidValue(int value)
        {
            return value < MinValue || value > MaxValue;
        }

        protected override int Get()
        {
            return NormalizeValue(IniFile.GetIntValue(IniSection, IniKey, DefaultValue));
        }

        protected override void Set(int value)
        {
            IniFile.SetIntValue(IniSection, IniKey, NormalizeValue(value));
        }
    }
}
