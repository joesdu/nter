using System.Net;
using System.Net.Sockets;

namespace nter;
internal sealed class NterServer(int port)
{
    /// <summary>
    /// 运行服务器
    /// </summary>
    /// <returns></returns>
    public async Task RunServer(CancellationToken cts)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.Listen(10);
        Console.WriteLine($"""
                           --------------------------------------------------------
                           服务器已启动,监听端口 {port}
                           --------------------------------------------------------
                           """);

        while (!cts.IsCancellationRequested)
        {
            var client = await listener.AcceptAsync(cts);
            _ = Task.Run(() => HandleClientAsync(client, port, cts), cts);
        }
    }

    /// <summary>
    /// 处理客户端连接
    /// </summary>
    /// <param name="client"></param>
    /// <param name="port"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    private static async Task HandleClientAsync(Socket client, int port, CancellationToken cts)
    {
        Console.WriteLine($"""
                           已接受来自 {client.RemoteEndPoint} 的连接
                           --------------------------------------------------------
                           """);
        var buffer = new byte[1024 * 1024]; // 1MB 缓冲区
        var endMarker = BitConverter.GetBytes(int.MaxValue);
        var endMarkerLength = endMarker.Length;

        try
        {
            while (!cts.IsCancellationRequested)
            {
                long totalBytesReceived = 0;
                var startTime = DateTime.MinValue;

                while (true)
                {
                    var bytesRead = await client.ReceiveAsync(buffer, SocketFlags.None, cts);
                    if (bytesRead == 0) break; // 客户端已断开

                    if (startTime == DateTime.MinValue)
                    {
                        // 解析客户端发送的时间戳
                        var timestampBytes = buffer.AsSpan(0, 8).ToArray();
                        var timestampTicks = BitConverter.ToInt64(timestampBytes, 0);
                        startTime = new DateTime(timestampTicks);
                    }

                    totalBytesReceived += bytesRead;

                    // 检查是否接收到结束符
                    if (bytesRead < endMarkerLength || !buffer.AsSpan(bytesRead - endMarkerLength, endMarkerLength).SequenceEqual(endMarker)) continue;
                    totalBytesReceived -= endMarkerLength; // 不计入结束符的字节数
                    break;
                }

                if (totalBytesReceived > 0)
                {
                    var totalDuration = DateTime.Now - startTime;
                    var totalBandwidth = totalBytesReceived * 8 / totalDuration.TotalSeconds / 1_000_000; // Mbps
                    Console.WriteLine($"[{Environment.CurrentManagedThreadId}] 接收: {totalBytesReceived / (1024 * 1024):F2} MBytes 带宽: {totalBandwidth:F2} Mbps");
                }
                else
                {
                    break; // 如果没有接收到数据，退出循环
                }
            }
            Console.WriteLine("--------------------------------------------------------");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生异常:{ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine($"""
                               --------------------------------------------------------
                               服务器已启动,监听端口 {port}
                               --------------------------------------------------------
                               """);
        }
    }
}
