using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Collections;

namespace P2P
{
    public class P2PServer
    {
        UdpClient udpClient;
        Thread thread;
        IPEndPoint remote;
        Dictionary<Guid, User> dcUser;
        Dictionary<Guid, int> dcPingNum;
        List<Guid> offline;
        bool isStart;
        Thread threadCheck;
        readonly object lockobject = new object();

        public P2PServer(IPAddress ip, int port)
        {
            udpClient = new UdpClient(new IPEndPoint(ip, port));
            remote = new IPEndPoint(IPAddress.Any, 0);
            dcUser = new Dictionary<Guid, User>();
            dcPingNum = new Dictionary<Guid, int>();
            thread = new Thread(Receive);
            thread.IsBackground = true;
            threadCheck = new Thread(Check);
            threadCheck.IsBackground = true;
            offline = new List<Guid>();
            isStart = false;
        }

        public void StartListen()
        {
            if (!isStart)
            {
                isStart = true;
                thread.Start();
                threadCheck.Start();
            }
        }

        public void StopListen()
        {
            if (isStart)
            {
                isStart = false;
                thread.Abort();
                threadCheck.Abort();
            }
        }

        private void Receive()
        {
            while (isStart)
            {
                var bytes = udpClient.Receive(ref remote);
                Message msg = Common.Deserialize<Message>(bytes);
                if (msg == null)
                {
                    continue;
                }
                lock (lockobject)
                {
                    switch (msg.type)
                    {
                        case MessageType.PING:
                            {
                                if (dcUser.ContainsKey(msg.userId))
                                {
                                    bytes = Common.Serialize(new Message { type = MessageType.PING });
                                    udpClient.Send(bytes, bytes.Length, dcUser[msg.userId].address);
                                    dcPingNum[msg.userId] = 1;
                                    if (!remote.EqualsValue(dcUser[msg.userId].address))
                                    {
                                        //外网端口可能发生变化如果长时间没有连接的话，但是一直发送ping的时候发现端口没有变化
                                        Console.WriteLine("地址发生变化，用户{0}[{1}],真实地址：{2}", dcUser[msg.userId].userName, dcUser[msg.userId].address, remote);
                                        dcUser[msg.userId].address = remote;
                                    }
                                }
                            }
                            break;
                        case MessageType.LOGIN:
                            {
                                var user = new User { id = Guid.NewGuid(), userName = Encoding.UTF8.GetString(msg.content), address = remote };
                                dcUser.Add(user.id, user);
                                dcPingNum.Add(user.id, 1);
                                bytes = Common.Serialize(new Message { type = MessageType.LOGIN, content = user.id.ToByteArray() });
                                udpClient.Send(bytes, bytes.Length, user.address);
                                Console.WriteLine("已发送login到{0}[{1}],guid:{2}", user.userName, user.address, user.id);
                                SendUserList();
                            }
                            break;
                        case MessageType.LIST:
                            {
                                if (dcUser.ContainsKey(msg.userId))
                                {
                                    var content = Common.Serialize(dcUser);
                                    bytes = Common.Serialize(new Message { type = MessageType.LIST, content = content });
                                    udpClient.Send(bytes, bytes.Length, remote);
                                    Console.WriteLine("已发送list到{0}[{1}],真实发送地址：{2}", dcUser[msg.userId].userName, dcUser[msg.userId].address, remote);
                                }
                            }
                            break;
                        case MessageType.P2PREQUEST:
                            {
                                if (dcUser.ContainsKey(msg.userId))
                                {
                                    var guid = Guid.Parse(Encoding.UTF8.GetString(msg.content));
                                    if (dcUser.ContainsKey(guid))
                                    {
                                        bytes = Common.Serialize(new Message { type = MessageType.P2PSOMEONECALLYOU, userId = msg.userId });
                                        udpClient.Send(bytes, bytes.Length, dcUser[guid].address);
                                        Console.WriteLine("已发送P2P请求到{0}[{1}],发送人：{2}[{3}],发送人真实地址：{4}", dcUser[guid].userName, dcUser[guid].address, dcUser[msg.userId].userName, dcUser[msg.userId].address, remote);
                                    }
                                }
                            }
                            break;
                        case MessageType.EXIT:
                            {
                                if (dcUser.ContainsKey(msg.userId))
                                {
                                    Console.WriteLine("收到用户{0}[{1}]的离线通知，真实地址{2}", dcUser[msg.userId].userName, dcUser[msg.userId].address, remote);
                                    dcUser.Remove(msg.userId);
                                    dcPingNum.Remove(msg.userId);
                                    SendUserList();
                                }
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 群发在线用户信息
        /// </summary>
        private void SendUserList()
        {
            var content = Common.Serialize(dcUser);
            var bytes = Common.Serialize(new Message { type = MessageType.LIST, content = content });
            foreach (var u in dcUser.Values)
            {
                udpClient.Send(bytes, bytes.Length, u.address);
                Console.WriteLine("已发送list到{0}[{1}]", u.userName, u.address);
            }
        }

        private void Check()
        {
            while (isStart)
            {
                //每30秒钟检查一次，如果没有接受到某用户的PING消息，认为该用户离线
                lock (lockobject)
                {
                    Console.WriteLine("check:" + Thread.CurrentThread.ManagedThreadId.ToString() + "   " + DateTime.Now.ToString());
                    offline.Clear();
                    Guid[] arr = new Guid[dcPingNum.Keys.Count];
                    dcPingNum.Keys.CopyTo(arr, 0);
                    Console.WriteLine("dcPingNum.Keys.Count:" + dcPingNum.Keys.Count);
                    foreach (Guid k in arr)
                    {
                        if (Convert.ToInt32(dcPingNum[k]) == 0)
                        {
                            offline.Add(k);
                        }
                        else
                        {
                            dcPingNum[k] = 0;
                        }
                    }
                    foreach (var k in offline)
                    {
                        dcPingNum.Remove(k);
                        dcUser.Remove(k);
                    }
                    if (offline.Count > 0)
                    {
                        SendUserList();
                    }
                    Console.WriteLine("dcUser.Count:" + dcUser.Count);
                }
                Thread.Sleep(30000);
            }
        }
    }
}
