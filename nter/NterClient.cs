using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Spectre.Console;

namespace nter;
internal sealed class NterClient(string serverAddress, int port)
{
    /// <summary>
    /// 创建连接
    /// </summary>
    /// <param name="cts"></param>
    /// <returns></returns>
    public async Task<Socket> ConnectAsync(CancellationToken cts)
    {
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(new IPEndPoint(IPAddress.Parse(serverAddress), port), cts);
        AnsiConsole.MarkupLine($"""
                                本地 [purple]{client.LocalEndPoint}[/] 连接到 [purple]{client.RemoteEndPoint}[/]
                                [green]-------------------------------------------------------------[/]
                                """);
        return client;
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="socket"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public static async Task SendDataAsync(Socket socket, CancellationToken cts)
    {
        var buffer = new byte[1024 * 1024]; // 1MB 缓冲区
        RandomNumberGenerator.Fill(buffer); // 填充随机数据

        long totalBytesSent = 0;
        var stopwatch = Stopwatch.StartNew();
        const int testDuration = 1; // 每次测试持续时间（秒）
        var intervalStopwatch = Stopwatch.StartNew();

        // 创建表格
        var table = new Table
        {
            Border = TableBorder.Rounded,
        };
        table.AddColumn("[bold]ID[/]");
        table.AddColumn("[bold]间隔[/]");
        table.AddColumn("[bold]发送[/]");
        table.AddColumn("[bold]带宽[/]");
        table.Columns[0].Centered();
        table.Columns[1].Centered();
        table.Columns[2].Centered();
        table.Columns[3].Centered();

        // 使用 Live 表格
        await AnsiConsole.Live(table).StartAsync(async ctx =>
        {
            for (var i = 1; i <= 10; i++)
            {
                try
                {
                    long intervalBytesSent = 0;
                    var testStopwatch = Stopwatch.StartNew();
                    while (testStopwatch.Elapsed.TotalSeconds < testDuration)
                    {
                        await socket.SendAsync(buffer, SocketFlags.None, cts);
                        intervalBytesSent += buffer.Length;
                        totalBytesSent += buffer.Length;
                    }
                    // 发送结束字符
                    var endMarker = BitConverter.GetBytes(int.MaxValue);
                    await socket.SendAsync(endMarker, SocketFlags.None, cts);
                    var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    var intervalThroughput = intervalBytesSent * 8 / intervalStopwatch.Elapsed.TotalSeconds / 1_000_000_000; // Gbps
                    table.AddRow(
                        $"{i}",
                        $"{Math.Abs(elapsedSeconds - intervalStopwatch.Elapsed.TotalSeconds):F2}-{elapsedSeconds:F2}秒",
                        $"{intervalBytesSent / (1024 * 1024):F2} MBytes",
                        $"{intervalThroughput:F2} Gbits/秒"
                    );
                    intervalStopwatch.Restart();
                    // 更新表格
                    ctx.Refresh();
                }
                catch (SocketException ex)
                {
                    AnsiConsole.MarkupLine($"[red]发送数据时发生异常:[/] {ex.Message}");
                    throw;
                }
            }
        });
        var totalDuration = stopwatch.Elapsed.TotalSeconds;
        var totalThroughputMbps = totalBytesSent * 8 / totalDuration / 1_000_000_000; // Gbits
        AnsiConsole.MarkupLine($"发送: [green]{totalBytesSent / (1024 * 1024):F2}[/] MBytes 带宽: [blue]{totalThroughputMbps:F2}[/] Gbits");
    }
}
