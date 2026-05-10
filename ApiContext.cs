namespace TInvestMcp;

using Grpc.Core;
using Tinkoff.InvestApi.V1;

/// <summary>Клиенты T-Invest API и метаданные для вызовов.</summary>
internal sealed class ApiContext(
    Metadata metadata,
    UsersService.UsersServiceClient users,
    OperationsService.OperationsServiceClient operations,
    InstrumentsService.InstrumentsServiceClient instruments)
{
    public Metadata Metadata { get; } = metadata;
    public UsersService.UsersServiceClient Users { get; } = users;
    public OperationsService.OperationsServiceClient Operations { get; } = operations;
    public InstrumentsService.InstrumentsServiceClient Instruments { get; } = instruments;
}
