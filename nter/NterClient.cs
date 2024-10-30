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
            Console.WriteLine($"本地[{client.LocalEndPoint}]连接到[{client.RemoteEndPoint}]");
            Console.WriteLine("-------------------------------------------------------------");
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
                Console.WriteLine($"[{i + 1}]|用时:{totalDuration.TotalSeconds:F2}秒|发送:\e[32m{totalBytesSent / (1024 * 1024):F2}\e[0m MBytes |带宽:\e[34m{totalBandwidthMbps:F2}\e[0m Mbps");
            }

            var overallDuration = overallStopwatch.Elapsed;
            var averageBandwidth = totalBytesSentOverall * 8 / overallDuration.TotalSeconds / 1_000_000; // Mbps
            Console.WriteLine($"""
                                -------------------------------------------------------------
                                [{Environment.CurrentManagedThreadId}]|总用时:{overallDuration.TotalSeconds:F2}秒,总发送:\e[32m{totalBytesSentOverall / (1024 * 1024 * 1024):F2}\e[0m GBytes,带宽:\e[34m{averageBandwidth:F2}\e[0m Mbps/秒
                                -------------------------------------------------------------
                                """);
            // 延迟测试
            var latencyBuffer = new byte[1];
            var latencyStopwatch = new Stopwatch();
            const int latencyTestCount = 100;
            double totalLatency = 0;

            for (var i = 0; i < latencyTestCount; i++)
            {
                latencyStopwatch.Restart();
                await client.SendAsync(latencyBuffer, SocketFlags.None, cts);
                await client.ReceiveAsync(latencyBuffer, SocketFlags.None, cts);
                latencyStopwatch.Stop();
                totalLatency += latencyStopwatch.Elapsed.TotalMilliseconds;
            }

            var averageLatency = totalLatency / latencyTestCount;
            Console.WriteLine($"平均延迟: {averageLatency:F2} 毫秒");
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
