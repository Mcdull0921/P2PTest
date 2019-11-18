using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace P2P
{
    static class Common
    {
        static BinaryFormatter formatter = new BinaryFormatter();
        internal static T Deserialize<T>(byte[] bytes) where T : class
        {
            MemoryStream ms = new MemoryStream(bytes);
            T obj = formatter.Deserialize(ms) as T;
            return obj;
        }

        internal static byte[] Serialize(object obj)
        {
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, obj);
            return ms.ToArray();
        }
    }

    static class IPEndPointExtend
    {
        public static bool EqualsValue(this IPEndPoint ip, IPEndPoint other)
        {
            return ip.Address.ToString() == other.Address.ToString() && ip.Port == other.Port;
        }
    }
}
