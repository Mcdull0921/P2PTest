using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace P2P
{
    [Serializable]
    public class User
    {
        public Guid id;
        public string userName;
        public IPEndPoint address;
    }

    public class MessageStatus
    {
        public Guid userId;
        public int ack;
        public byte[] buffer;
    }
}
