/* 判断消息类型来自服务端还是客户端，不满足条件的消息直接抛弃
 * 内部维护一个用户列表，记录用户的外网IP和端口，不在列表中的消息抛弃
 * 消息类型有分用户类型：文字，抖动，消息确认收到，打洞验证；服务端消息：用户列表，用户退出，打洞请求
 * 直接对单个用户发送消息，超出限制次数向服务端发送打洞请求，服务端向对方发送打洞请求，对方回发一个打洞消息确认收到，收到该消息再次P2P发送
 * 主动退出：客户端向服务端发送退出消息，自身不再连接，服务端收到消息后踢出客户端
 * 被动退出：客户端不断向服务端发送包，但累积到一定数量都没有收到服务端回执认为连接断开；服务端定时轮询，如果没有收到客户端发送的包认为该客户端已断开
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace P2P
{
    public class P2PClient
    {
        UdpClient udpClient;
        Thread thread;
        Thread threadPing;
        bool isStart;
        IPEndPoint remote;
        IPEndPoint server;
        Dictionary<Guid, User> dcUser;
        Dictionary<Guid, MessageStatus> dcAck;
        User currentUser;
        int pingNum;
        SynchronizationContext synchroinzationContext;

        public event EventHandler<TextMessage> onReceiveMessage;
        public event EventHandler<UserList> onUpdateUser;
        public event EventHandler onLoginOff;

        public P2PClient(IPEndPoint server, int localport)
        {
            this.server = server;
            this.remote = new IPEndPoint(IPAddress.Any, 0);
            this.udpClient = new UdpClient(localport);
            this.currentUser = new User();
            dcUser = new Dictionary<Guid, User>();
            dcAck = new Dictionary<Guid, MessageStatus>();
            this.isStart = false;
        }

        public P2PClient(IPEndPoint server, int localport, SynchronizationContext synchroinzationContext)
            : this(server, localport)
        {
            this.synchroinzationContext = synchroinzationContext;
        }

        public bool Start(string userName)
        {
            if (!isStart)
            {
                isStart = true;
                thread = new Thread(Receive);
                thread.IsBackground = true;
                thread.Start();
                threadPing = new Thread(Ping);
                threadPing.IsBackground = true;
                threadPing.Start();
                pingNum = 0;
                Thread.Sleep(500);
                Login(userName);
                return true;
            }
            return false;
        }

        public bool Stop()
        {
            if (isStart)
            {
                isStart = false;
                if (thread != null)
                    thread.Abort();
                if (threadPing != null)
                    threadPing.Abort();
                LoginOut();
                //离线事件
                OnLoginOff();
                return true;
            }
            return false;
        }

        public void List()
        {
            if (currentUser.id == default(Guid))
                return;
            var bytes = Common.Serialize(new Message { type = MessageType.LIST, userId = currentUser.id });
            udpClient.Send(bytes, bytes.Length, server);
        }

        public void Send(string msg, User user)
        {
            if (currentUser.id == default(Guid))
                return;
            if (!dcAck.ContainsKey(user.id))
                dcAck.Add(user.id, new MessageStatus { userId = user.id, ack = 0 });
            else
                dcAck[user.id].ack = 0;
            var bytes = Common.Serialize(new Message { type = MessageType.MSG, userId = currentUser.id, content = Encoding.UTF8.GetBytes(msg) });
            for (int i = 0; i < 10; i++)
            {
                if (dcAck[user.id].ack == 0)
                {
                    udpClient.Send(bytes, bytes.Length, user.address);
                    Thread.Sleep(10);
                }
                else
                    return;
            }
            //消息缓存起来
            dcAck[user.id].buffer = bytes;
            bytes = Common.Serialize(new Message { type = MessageType.P2PREQUEST, userId = currentUser.id, content = Encoding.UTF8.GetBytes(user.id.ToString()) });
            udpClient.Send(bytes, bytes.Length, server);
            Console.WriteLine("向服务器发送了P2P请求");
        }

        public void Login(string userName)
        {
            var bytes = Common.Serialize(new Message { type = MessageType.LOGIN, content = Encoding.UTF8.GetBytes(userName) });
            udpClient.Send(bytes, bytes.Length, server);
        }

        private void LoginOut()
        {
            if (currentUser.id == default(Guid))
                return;
            var bytes = Common.Serialize(new Message { type = MessageType.EXIT, userId = currentUser.id });
            udpClient.Send(bytes, bytes.Length, server);
        }

        private void Receive()
        {
            while (isStart)
            {
                try
                {
                    var bytes = udpClient.Receive(ref remote);
                    Message msg = Common.Deserialize<Message>(bytes);
                    if (msg == null)
                    {
                        continue;
                    }
                    switch (msg.type)
                    {
                        case MessageType.PING:
                            {
                                if (server.EqualsValue(remote))
                                {
                                    Interlocked.Exchange(ref pingNum, 0);
                                }
                            }
                            break;
                        case MessageType.LOGIN:
                            {
                                if (server.EqualsValue(remote))
                                {
                                    currentUser.id = new Guid(msg.content);
                                    Console.WriteLine("用户Id已更新：" + currentUser.id);
                                }
                            }
                            break;
                        case MessageType.LIST:
                            {
                                if (server.EqualsValue(remote))
                                {
                                    dcUser = Common.Deserialize<Dictionary<Guid, User>>(msg.content);
                                    OnUpdateUser(new UserList { dictinary = dcUser });
                                }
                            }
                            break;
                        case MessageType.P2PSOMEONECALLYOU:
                            {
                                if (server.EqualsValue(remote))
                                {
                                    if (dcUser.ContainsKey(msg.userId))
                                    {
                                        bytes = Common.Serialize(new Message { type = MessageType.P2PACCEPT, userId = currentUser.id });
                                        udpClient.Send(bytes, bytes.Length, dcUser[msg.userId].address);
                                        Console.WriteLine("P2PSOMEONECALLYOU收到来自服务器P2P请求申请，来自用户{0}[{1}]", dcUser[msg.userId].userName, dcUser[msg.userId].address);
                                    }
                                }
                            }
                            break;
                        case MessageType.MSG:
                            {
                                if (dcUser.ContainsKey(msg.userId) && dcUser[msg.userId].address.EqualsValue(remote))
                                {
                                    bytes = Common.Serialize(new Message { type = MessageType.ACK, userId = currentUser.id });
                                    udpClient.Send(bytes, bytes.Length, dcUser[msg.userId].address);
                                    OnReceiveMessage(new TextMessage { userName = dcUser[msg.userId].userName, content = Encoding.UTF8.GetString(msg.content), address = remote });
                                }
                            }
                            break;
                        case MessageType.ACK:
                            {
                                if (dcUser.ContainsKey(msg.userId) && dcUser[msg.userId].address.EqualsValue(remote))
                                {
                                    if (dcAck.ContainsKey(msg.userId) && dcAck[msg.userId].ack == 0)
                                        dcAck[msg.userId].ack = 1;
                                    Console.WriteLine("ACK收到来自用户：{0}[{1}]的消息已送达回执", dcUser[msg.userId].userName, dcUser[msg.userId].address);
                                }
                            }
                            break;
                        case MessageType.P2PACCEPT:
                            {
                                Console.WriteLine("P2PACCEPT收到来自p2p申请用户的回执，来自用户{0}[{1}]", dcUser[msg.userId].userName, dcUser[msg.userId].address);
                                //收到这个消息代表P2P打洞已建立,如果有缓存消息发送出去
                                if (dcAck[msg.userId].buffer != null)
                                {
                                    udpClient.Send(dcAck[msg.userId].buffer, dcAck[msg.userId].buffer.Length, dcUser[msg.userId].address);
                                    dcAck[msg.userId].buffer = null;
                                    Console.WriteLine("发送缓存消息");
                                }
                            }
                            break;
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine(ex.Message);
                    Thread.Sleep(1000);
                    continue;
                }
            }
        }

        private void Ping()
        {
            while (isStart)
            {
                if (Thread.VolatileRead(ref pingNum) > 10)
                {
                    Console.WriteLine("连接丢失了");
                    isStart = false;
                    if (thread != null)
                        thread.Abort();
                    //离线事件
                    OnLoginOff();
                    return;
                }
                else
                {
                    var bytes = Common.Serialize(new Message { type = MessageType.PING, userId = currentUser.id });
                    udpClient.Send(bytes, bytes.Length, server);
                    Interlocked.Increment(ref pingNum);
                }
                Thread.Sleep(3000);
            }
        }

        private void OnReceiveMessage(TextMessage msg)
        {
            var handle = onReceiveMessage;
            if (handle != null)
            {
                if (synchroinzationContext == null)
                    handle(this, msg);
                else
                    synchroinzationContext.Post(o => handle(this, (TextMessage)o), msg);
            }
        }

        private void OnUpdateUser(UserList users)
        {
            var handle = onUpdateUser;
            if (handle != null)
            {
                if (synchroinzationContext == null)
                    handle(this, users);
                else
                    synchroinzationContext.Post(o => handle(this, (UserList)o), users);
            }
        }

        private void OnLoginOff()
        {
            var handle = onLoginOff;
            if (handle != null)
            {
                if (synchroinzationContext == null)
                    handle(this, null);
                else
                    synchroinzationContext.Post(o => handle(this, null), null);
            }
        }
    }
}
