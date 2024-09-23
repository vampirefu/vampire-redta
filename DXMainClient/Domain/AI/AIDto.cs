using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DTAClient.Domain.AI;

[DataContract]
public class AIDto
{
    [DataMember]
    public string AIName { get; set; }
    /// <summary>
    /// 新增文件路径集
    /// </summary>
    [DataMember]
    public List<string> AddList { get; set; }
    /// <summary>
    /// 替换文件路径集
    /// </summary>
    [DataMember]
    public List<string> ReplaceList { get; set; }
}
