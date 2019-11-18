using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using P2P;

namespace P2PClient
{
    class Program
    {
        const string help = "输入命令：\r\n Start开始连接：start 用户名\r\n Stop停止监听：stop\r\n Send发送消息：send 用户名 消息\r\n List用户列表：list\r\n Quit退出";
        static Dictionary<Guid, User> dcUsers;
        static void Main(string[] args)
        {
            Console.WriteLine("输入服务器IP地址：");
            string ip = "115.29.188.54";// Console.ReadLine();
            var server = new IPEndPoint(IPAddress.Parse(ip), 8090);//115.29.188.54;127.0.0.1
            Console.WriteLine("输入本机连接端口：");
            int port = Int32.Parse(Console.ReadLine());
            P2P.P2PClient client = new P2P.P2PClient(server, port);
            client.onReceiveMessage += new EventHandler<TextMessage>(client_onReceiveMessage);
            client.onUpdateUser += new EventHandler<UserList>(client_onUpdateUser);
            client.onLoginOff += new EventHandler(client_onLoginOff);
            Console.WriteLine(help);
            while (true)
            {
                var cmd = Console.ReadLine();
                var arr = cmd.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (arr.Length == 0)
                    continue;
                if (arr[0].ToUpper() == "START" && arr.Length > 1)
                {
                    Console.WriteLine("启动{0}", client.Start(arr[1]) ? "成功" : "失败");
                }
                else if (arr[0].ToUpper() == "LOGIN" && arr.Length > 1)
                {
                    client.Login(arr[1]);
                }
                else if (arr[0].ToUpper() == "STOP")
                {
                    Console.WriteLine("关闭{0}", client.Stop() ? "成功" : "失败");
                }
                else if (arr[0] == "?")
                {
                    Console.WriteLine(help);
                }
                else if (arr[0].ToUpper() == "SEND" && arr.Length > 2)
                {
                    if (dcUsers == null || dcUsers.Count == 0)
                        continue;
                    var u = dcUsers.Values.FirstOrDefault(p => p.userName == arr[1]);
                    if (u == null)
                    {
                        Console.WriteLine("用户{0}不存在", arr[1]);
                        continue;
                    }
                    client.Send(arr[2], u);
                }
                else if (arr[0].ToUpper() == "LIST")
                {
                    client.List();
                }
                else if (arr[0].ToUpper() == "QUIT")
                {
                    client.Stop();
                    return;
                }
                else
                    Console.WriteLine("指令无效");
            }
        }

        static void client_onLoginOff(object sender, EventArgs e)
        {
            Console.WriteLine("已离线");
        }

        static void client_onUpdateUser(object sender, UserList e)
        {
            dcUsers = e.dictinary;
            Console.WriteLine("用户列表：");
            if (dcUsers == null || dcUsers.Count == 0)
                return;
            foreach (var u in dcUsers.Values)
            {
                Console.WriteLine("id:{0},用户名：{1}，地址{2}", u.id, u.userName, u.address);
            }
        }

        static void client_onReceiveMessage(object sender, TextMessage e)
        {
            Console.WriteLine("{0}[{1}]说：{2}", e.userName, e.address, e.content);
        }
    }
}
