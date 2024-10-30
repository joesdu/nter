// See https://aka.ms/new-console-template for more information
namespace nter;

public static class Program
{
    private static CancellationTokenSource? _cts;

    // ReSharper disable once UnusedMember.Global
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }
        _cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, _) => _cts.Cancel();
        await MainAsync(args, _cts.Token);
    }

    private static async Task MainAsync(string[] args, CancellationToken token)
    {
        var port = 5000; // 默认端口
        // 解析 -p 参数
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "-p" || i + 1 >= args.Length || !int.TryParse(args[i + 1], out var parsedPort)) continue;
            port = parsedPort;
            break;
        }
        switch (args[0])
        {
            case "-s":
                // 作为服务器运行
                var server = new NterServer(port);
                await server.RunServer(token);
                break;
            case "-c" when args.Length > 1:
                {
                    // 作为客户端运行
                    var serverAddress = args[1];
                    var client = new NterClient(serverAddress, port);
                    await client.RunClient(token);
                    break;
                }
            case "-h":
                // 显示帮助信息
                ShowHelp();
                break;
            default:
                Console.WriteLine("参数错误。请使用参数 -s 启动服务器，或使用参数 -c <服务器地址> 启动客户端。");
                break;
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("用法:");
        Console.WriteLine("  -s          启动服务器");
        Console.WriteLine("  -c <地址>   启动客户端并连接到指定的服务器地址");
        Console.WriteLine("  -p <端口>   指定服务器或客户端使用的端口（默认: 5000）");
        Console.WriteLine("  -h          显示帮助信息");
    }
}
