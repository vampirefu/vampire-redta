using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ReDta.Login.Helper;
public class AESHelper
{
    public const string PMIV = "cn.vampire699899";
    public const string PMKey = "12345testvampire";

    /// <summary>
    ///  AES 加密
    /// </summary>
    /// <param name="str">明文（待加密）</param>
    /// <param name="key">密文</param>
    /// <returns></returns>
    public static string AesEncrypt(string str, string key = PMKey, string iv = PMIV)
    {
        if (string.IsNullOrEmpty(str))
            return null;
        Byte[] toEncryptArray = Encoding.UTF8.GetBytes(str);

        ///AES/CBC/PKCS5Padding加密类型
        RijndaelManaged rm = new RijndaelManaged
        {
            Key = Encoding.UTF8.GetBytes(key),
            //Mode = CipherMode.ECB,
            Mode = CipherMode.CBC,
            Padding = PaddingMode.PKCS7
        };
        if (!string.IsNullOrEmpty(iv))
            rm.IV = UTF8Encoding.UTF8.GetBytes(iv);

        ICryptoTransform cTransform = rm.CreateEncryptor();
        Byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }

    /// <summary>
    ///  AES 解密
    /// </summary>
    /// <param name="str">明文（待解密）</param>
    /// <param name="key">密文</param>
    /// <returns></returns>
    public static string AesDecrypt(string str, string key = PMKey, string iv = PMIV)
    {
        if (string.IsNullOrEmpty(str))
            return null;
        Byte[] toEncryptArray = Convert.FromBase64String(str);

        RijndaelManaged rm = new RijndaelManaged
        {
            Key = Encoding.UTF8.GetBytes(key),
            Mode = CipherMode.CBC,
            Padding = PaddingMode.PKCS7
        };
        if (!string.IsNullOrEmpty(iv))
            rm.IV = UTF8Encoding.UTF8.GetBytes(iv);

        ICryptoTransform cTransform = rm.CreateDecryptor();
        Byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        return Encoding.UTF8.GetString(resultArray);
    }
}
