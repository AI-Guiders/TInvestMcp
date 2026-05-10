using System.Collections.Frozen;
using System.Text.Json;
using Grpc.Net.Client;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TInvestMcp;
using Tinkoff.InvestApi.V1;

const string Endpoint = "https://invest-public-api.tinkoff.ru:443";

var token = Environment.GetEnvironmentVariable("TINVEST_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    var tokenFile = Environment.GetEnvironmentVariable("TINVEST_TOKEN_FILE");
    if (!string.IsNullOrWhiteSpace(tokenFile) && File.Exists(tokenFile))
        token = File.ReadAllText(tokenFile).Trim();
}
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Set TINVEST_TOKEN or TINVEST_TOKEN_FILE (path to a file with the token).");
    return 1;
}

var channel = GrpcChannel.ForAddress(new Uri(Endpoint));
var metadata = new Grpc.Core.Metadata { { "Authorization", "Bearer " + token } };
var ctx = new ApiContext(
    metadata,
    new UsersService.UsersServiceClient(channel),
    new OperationsService.OperationsServiceClient(channel),
    new InstrumentsService.InstrumentsServiceClient(channel));

// Список инструментов собираем один раз при старте — ListToolsHandler возвращает ссылку без аллокаций
var toolsList = ToolCatalog.Build();

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "TInvestMcp", Version = "1.0.0" },
    ServerInstructions = ServerInstructions.Text,
    ProtocolVersion = "2024-11-05",
    // Список инструментов статичен после старта — клиент может кэшировать
    Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } },
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = toolsList }),

        CallToolHandler = async (request, cancellationToken) =>
        {
            var name = request.Params?.Name ?? "";
            var args = request.Params?.Arguments ?? FrozenDictionary<string, JsonElement>.Empty;

            try
            {
                var argsRo = args as IReadOnlyDictionary<string, JsonElement> ?? FrozenDictionary<string, JsonElement>.Empty;
                var text = await ToolHandlers.HandleAsync(name, ctx, argsRo, cancellationToken).ConfigureAwait(false);
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = text }]
                };
            }
            catch (McpProtocolException ex)
            {
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = ex.Message }],
                    IsError = true
                };
            }
            catch (Grpc.Core.RpcException ex)
            {
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"T-Invest API error: {ex.StatusCode} — {ex.Message}" }],
                    IsError = true
                };
            }
            catch (Exception ex)
            {
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"Error: {ex.GetType().Name} — {ex.Message}\n{(ex.InnerException != null ? "Inner: " + ex.InnerException.Message : "")}" }],
                    IsError = true
                };
            }
        }
    }
};

var transport = new StdioServerTransport("TInvestMcp");
await using var server = McpServer.Create(transport, options);
await server.RunAsync();
return 0;
