using System.Text;

namespace ClientCore.PlatformShim;

public static class EncodingExt
{
    static EncodingExt()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ANSI = Encoding.GetEncoding(0);
    }

    /// <summary>
    /// 获取传统 ANSI 编码（不是 Windows-1252，也不是任何特定编码）。
    /// ANSI 并不指代特定的代码页，它指的是默认的非 Unicode 代码页，可以从控制面板中更改。
    /// </summary>
    public static Encoding ANSI { get; }
}