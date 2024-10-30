using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Spectre.Console;

namespace nter;
internal sealed class NterServer(int port)
{
    private readonly byte[] _buffer = new byte[1024 * 64]; // 64KB 缓冲区


    /// <summary>
    /// 运行服务器
    /// </summary>
    /// <returns></returns>
    public async Task RunServer(CancellationToken cts)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.Listen(10);
        AnsiConsole.MarkupLine($"""
                           [green]--------------------------------------------------------[/]
                           服务端已启动,监听端口 [purple]{port}[/]
                           [green]--------------------------------------------------------[/]
                           """);

        while (!cts.IsCancellationRequested)
        {
            var client = await listener.AcceptAsync(cts);
            _ = Task.Run(() => HandleClientAsync(client, cts), cts);
        }
    }

    private async Task HandleClientAsync(Socket socket, CancellationToken cts)
    {
        AnsiConsole.MarkupLine($"""
                           已接受来自 [purple]{socket.RemoteEndPoint}[/] 的连接
                           [green]--------------------------------------------------------[/]
                           """);
        long totalBytesReceived = 0;
        var startTicks = Stopwatch.GetTimestamp();
        try
        {
            var table = new Table
            {
                Border = TableBorder.Rounded,
            };
            table.AddColumn("[bold]ID[/]");
            table.AddColumn("[bold]间隔[/]");
            table.AddColumn("[bold]接收[/]");
            table.AddColumn("[bold]带宽[/]");
            table.Columns[0].Centered();
            table.Columns[1].Centered();
            table.Columns[2].Centered();
            table.Columns[3].Centered();

            // 使用 Live 表格
            await AnsiConsole.Live(table).StartAsync(async ctx =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var intervalStopwatch = Stopwatch.StartNew();
                    long intervalBytesReceived = 0;
                    while (true)
                    {
                        var bytesRead = await socket.ReceiveAsync(_buffer, SocketFlags.None, cts);
                        if (bytesRead == 0) break; // 客户端已断开
                        totalBytesReceived += bytesRead;
                        intervalBytesReceived += bytesRead;
                        // 检查是否接收到结束字符
                        if (bytesRead < 4 || BitConverter.ToInt32(_buffer, bytesRead - 4) != int.MaxValue) continue;
                        totalBytesReceived -= 4; // 不计入结束字符的字节数
                        break;
                    }
                    if (intervalBytesReceived > 0)
                    {
                        var elapsedSeconds = (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;
                        var intervalThroughput = intervalBytesReceived * 8 / intervalStopwatch.Elapsed.TotalSeconds / 1_000_000_000; // Gbps
                        table.AddRow(
                            $"{Environment.CurrentManagedThreadId}",
                            $"{Math.Abs(elapsedSeconds - intervalStopwatch.Elapsed.TotalSeconds):F2}-{elapsedSeconds:F2}秒",
                            $"{intervalBytesReceived / (1024 * 1024):F2} MBytes",
                            $"{intervalThroughput:F2} Gbits/秒"
                        );
                        intervalStopwatch.Restart();
                        // 更新表格
                        ctx.Refresh();
                    }
                    else
                    {
                        break; // 如果没有接收到数据，退出循环
                    }
                }
            });
        }
        catch (SocketException)
        {
            var totalDuration = (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;
            var totalThroughput = totalBytesReceived * 8 / totalDuration / 1_000_000_000; // Gbits
            AnsiConsole.MarkupLine($"接收: [green]{totalBytesReceived / (1024 * 1024):F2}[/] MBytes 带宽: [blue]{totalThroughput:F2}[/] Gbits");
        }
        finally
        {
            socket.Close();
            AnsiConsole.MarkupLine($"""
                               [green]--------------------------------------------------------[/]
                               服务器已启动,监听端口 [purple]{port}[/]
                               [green]--------------------------------------------------------[/]
                               """);
        }
    }
}
