using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReDta.Login.Common;
public class CommonInfo
{
    private static CommonInfo _instance;
    public static CommonInfo Instance => _instance ?? (_instance = new CommonInfo());

    private CommonInfo()
    {

    }

    public string Token { get; set; }
}
