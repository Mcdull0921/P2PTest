using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace P2P
{
    [Serializable]
    public class Message
    {
        public MessageType type;
        public Guid userId;
        public byte[] content;
    }

    public enum MessageType
    {
        PING,
        LOGIN,
        MSG,
        ACK,
        P2PREQUEST,
        P2PSOMEONECALLYOU,
        P2PACCEPT,
        LIST,
        EXIT
    }

    public class TextMessage : EventArgs
    {
        public string userName;
        public string content;
        public IPEndPoint address;
    }

    public class UserList : EventArgs
    {
        public Dictionary<Guid, User> dictinary;
    }
}
