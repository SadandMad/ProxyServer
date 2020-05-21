using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ProxyServer
{
    class Program
    {
        static void Main()
        {
            TcpListener Awaiting = new TcpListener(IPAddress.Parse("127.0.0.1"), 205);
            Awaiting.Start();

            while (true)
            {
                if (Awaiting.Pending())
                {
                    TcpClient Receiving = Awaiting.AcceptTcpClient();
                    Task Processing = new Task(() => ReceiveMessage(Receiving));
                    Processing.Start();
                }
            }
        }

        public static byte[] CutPath(byte[] data)
        {
            string buffer = Encoding.UTF8.GetString(data);
            Regex header = new Regex(@"http:\/\/[a-z0-9а-яё\:\.]*");
            MatchCollection headers = header.Matches(buffer);
            buffer = buffer.Replace(headers[0].Value, "");
            data = Encoding.UTF8.GetBytes(buffer);
            return data;
        }

        public static void ProcessMessage(byte[] buf, int bytesRead, NetworkStream browserStream)
        {
            try
            {
                string[] buffer = Encoding.UTF8.GetString(buf).Trim().Split(new char[] { '\r', '\n' });
                // if (buffer.Length > 1)
                {
                    string host = buffer.FirstOrDefault(x => x.Contains("Host"));
                    host = host.Substring(host.IndexOf(":") + 2);
                    string[] port = host.Trim().Split(new char[] { ':' });

                    TcpClient sender;
                    if (port.Length == 2)
                    {
                        sender = new TcpClient(port[0], int.Parse(port[1]));
                    }
                    else
                    {
                        sender = new TcpClient(port[0], 80);
                    }

                    NetworkStream serverStream = sender.GetStream();
                    serverStream.Write(CutPath(buf), 0, bytesRead);

                    byte[] answer = new byte[65536];
                    int length = serverStream.Read(answer, 0, answer.Length);

                    string[] head = Encoding.UTF8.GetString(answer).Split(new char[] { '\r', '\n' });
                    string ResponseCode = head[0].Substring(head[0].IndexOf(" ") + 1);
                    Console.WriteLine(host + "  " + ResponseCode);

                    browserStream.Write(answer, 0, length);
                    serverStream.CopyTo(browserStream);

                    serverStream.Close();
                }
            }
            catch
            {
                return;
            }
            finally
            {
                browserStream.Close();
            }
        }

        public static void ReceiveMessage(TcpClient client)
        {
            NetworkStream browserStream = client.GetStream();
            byte[] buf = new byte[65536];
            while (browserStream.CanRead)
            {
                if (browserStream.DataAvailable)
                {
                    try
                    {
                        int length = browserStream.Read(buf, 0, buf.Length);
                        ProcessMessage(buf, length, browserStream);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        return;
                    }
                }
            }
            client.Close();
        }
    }
}