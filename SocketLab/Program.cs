using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketLab
{
    class Program
    {
        public static Dictionary<Guid, SocketClient> dict = new Dictionary<Guid, SocketClient>();
        static void Main(string[] args)
        {
            
            var listener = new TcpListener(2055);
            listener.Start();
            Console.WriteLine("Server mounted, listening to port 2055");
            while (!(Console.KeyAvailable))
            {
                Socket soc = listener.AcceptSocket();
                Console.WriteLine("Connected: {0}",
                                             soc.RemoteEndPoint);
                try
                {
                    Stream s = new NetworkStream(soc);
                    StreamReader sr = new StreamReader(s);
                    StreamWriter sw = new StreamWriter(s);
                    sw.AutoFlush = true; // enable automatic flushing

                    var client = new SocketClient()
                    {
                        Id = Guid.NewGuid(),
                        Reader = sr,
                        Writer = sw,
                        Socket = soc,
                        Stream = s
                    };
                    client.Nick = client.Id.ToString();
                    foreach (var cli in dict.Values)
                    {
                        cli.Writer.WriteLineAsync("Connected " + client.Id);
                    }
                    dict.Add(client.Id, client);
                    client.StartThread();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    try
                    {
                        soc.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
