using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace nter;
internal sealed class NterClient(string serverAddress, int port)
{
    /// <summary>
    /// 运行客户端
    /// </summary>
    /// <param name="cts"></param>
    /// <returns></returns>
    public async Task RunClient(CancellationToken cts)
    {
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await client.ConnectAsync(new IPEndPoint(IPAddress.Parse(serverAddress), port), cts);
            Console.WriteLine($"""
                               本地 [[35m{client.LocalEndPoint}[0m] 连接到 [[35m{client.RemoteEndPoint}[0m]
                               -------------------------------------------------------------
                               """);
            var buffer = new byte[1024 * 1024]; // 1MB 缓冲区
            new Random().NextBytes(buffer); // 填充随机数据
            var endMarker = BitConverter.GetBytes(int.MaxValue);

            const int testDuration = 10; // 每次测试持续时间（秒）
            const int testCount = 6; // 测试次数
            long totalBytesSentOverall = 0;
            var overallStopwatch = Stopwatch.StartNew();

            for (var i = 0; i < testCount; i++)
            {
                long totalBytesSent = 0;
                var testStopwatch = Stopwatch.StartNew();
                var startTime = DateTime.Now;

                // 将时间戳写入缓冲区的前8个字节
                var timestampBytes = BitConverter.GetBytes(startTime.Ticks);
                Array.Copy(timestampBytes, 0, buffer, 0, timestampBytes.Length);

                while (testStopwatch.Elapsed.TotalSeconds < testDuration)
                {
                    await client.SendAsync(buffer, SocketFlags.None, cts);
                    totalBytesSent += buffer.Length;
                    totalBytesSentOverall += buffer.Length;
                }

                // 发送结束符
                await client.SendAsync(endMarker, SocketFlags.None, cts);
                var totalDuration = testStopwatch.Elapsed;
                var totalBandwidthMbps = totalBytesSent * 8 / totalDuration.TotalSeconds / 1_000_000; // Mbps
                Console.WriteLine($"[{i + 1}]|用时:\e[33m{totalDuration.TotalSeconds:F2}\e[0m秒|发送: \e[32m{totalBytesSent / (1024 * 1024):F2}\e[0m MBytes |带宽: \e[34m{totalBandwidthMbps:F2}\e[0m Mbps");
            }

            var overallDuration = overallStopwatch.Elapsed;
            var averageBandwidth = totalBytesSentOverall * 8 / overallDuration.TotalSeconds / 1_000_000; // Mbps
            Console.WriteLine($"""
                                -------------------------------------------------------------
                                [{Environment.CurrentManagedThreadId}]|总用时:[33m{overallDuration.TotalSeconds:F2}[0m秒,总发送: [32m{totalBytesSentOverall / (1024 * 1024 * 1024):F2}[0m GBytes,带宽: [34m{averageBandwidth:F2}[0m Mbps
                                -------------------------------------------------------------
                                """);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"连接失败:{ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }
}
