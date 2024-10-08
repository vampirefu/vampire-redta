﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace DTAClient.DXGUI.Helpers;
public class JsonSerializeHelper
{
    public static string JsonSerialize<T>(T obj)
    {
        //实例化DataContractJsonSerializer对象，需要待序列化的对象类型
        DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
        //实例化一个内存流，用于存放序列化后的数据
        MemoryStream stream = new MemoryStream();
        //使用WriteObject序列化对象
        serializer.WriteObject(stream, obj);
        //写入内存流中
        byte[] dataBytes = new byte[stream.Length];
        stream.Position = 0;
        stream.Read(dataBytes, 0, (int)stream.Length);
        //通过UTF8格式转换为字符串
        return Encoding.UTF8.GetString(dataBytes);
    }

    public static T JsonDeserialize<T>(string jsonString)
    {
        //实例化DataContractJsonSerializer对象，需要待序列化的对象类型
        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
        //把Json传入内存流中保存
        MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
        // 使用ReadObject方法反序列化成对象
        return (T)serializer.ReadObject(stream);
    }
}
