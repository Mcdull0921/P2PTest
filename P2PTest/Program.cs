using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using P2P;

namespace P2PTest
{
    class Program
    {
        static void Main(string[] args)
        {
            P2PServer server = new P2PServer(IPAddress.Any, 8090);
            Console.WriteLine("服务已启动");
            server.StartListen();


            Console.ReadKey();
        }
    }

}
