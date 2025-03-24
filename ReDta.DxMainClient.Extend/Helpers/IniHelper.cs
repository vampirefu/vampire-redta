using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ClientCore;

namespace ReDta.DxMainClient.Extend.Helpers
{
    class IniHelper
    {
        private static string _iniPath;
        public static string IniPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_iniPath) && File.Exists(_iniPath))
                    return _iniPath;
                if (!File.Exists(_iniPath))
                    _iniPath = Path.Combine(ProgramConstants.GamePath, "ReDtaVersion.ini");
                return _iniPath;
            }
        }

        [DllImport("Kernel32.dll")]
        private static extern int GetPrivateProfileString(string strAppName, string strKeyName, string strDefault, StringBuilder sbReturnString, int nSize, string strFileName);

        public static IniValue ReadItem(string section, string key)
        {
            try
            {
                StringBuilder buffer = new StringBuilder(256);
                GetPrivateProfileString(section, key, string.Empty, buffer, buffer.Capacity, IniPath);
                string str = buffer.ToString();
                return new IniValue(str);
            }
            catch
            {
                return new IniValue(string.Empty);
            }
        }

        [DllImport("kernel32.dll")]
        private extern static int WritePrivateProfileStringA(string segName, string keyName, string sValue, string fileName);
        /// <summary>
        /// 用于写任何类型的键值到ini文件中
        /// </summary>
        /// <param name="Section">该键所在的节名称</param>
        /// <param name="Key">该键的名称</param>
        /// <param name="Value">该键的值</param>
        public static void Write(string Section, string Key, object Value)
        {
            if (!File.Exists(IniPath))
                File.Create(IniPath).Close();

            if (Value != null)
                WritePrivateProfileStringA(Section, Key, Value.ToString(), IniPath);
            else
                WritePrivateProfileStringA(Section, Key, null, IniPath);
        }
    }

    public struct IniValue
    {
        private readonly string _value;

        public IniValue(string value)
        {
            _value = value;
        }

        public int AsInt(int defaultValue = -1)
        {
            int result;
            if (int.TryParse(_value, out result))
                return result;
            return defaultValue;
        }

        public long AsLong(long defaultValue = -1)
        {
            long result;
            if (long.TryParse(_value, out result))
                return result;
            return defaultValue;
        }

        public byte AsByte(byte defaultValue = 0)
        {
            byte result;
            if (byte.TryParse(_value, out result))
                return result;
            return defaultValue;
        }

        public bool AsBool(bool defaultValue = false)
        {
            bool result;
            if (bool.TryParse(_value, out result))
                return result;
            return defaultValue;
        }

        public float AsFloat(float defaultValue = -1)
        {
            float result;
            if (float.TryParse(_value, out result))
                return result;
            return defaultValue;
        }

        public double AsDouble(double defaultValue = -1)
        {
            double result;
            if (double.TryParse(_value, out result))
                return result;
            return defaultValue;
        }

        public string AsString(string defaultValue = "")
        {
            if (string.IsNullOrEmpty(_value))
                return defaultValue;
            return _value;
        }

        public DateTime AsDateTime(DateTime defaultValue)
        {
            DateTime result;
            if (DateTime.TryParse(_value, out result))
                return result;
            return defaultValue;
        }

        public bool? AsBoolMark(bool? defaultVal = null)
        {
            if (_value == "True")
                return true;
            else if (_value == "False")
                return false;
            return defaultVal;
        }

        public override string ToString()
        {
            return this._value;
        }
    }
}
