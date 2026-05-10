namespace TInvestMcp;

using Google.Protobuf.WellKnownTypes;
using Tinkoff.InvestApi.V1;

/// <summary>Преобразование типов protobuf в обычные типы C#.</summary>
internal static class ProtoHelpers
{
    extension(MoneyValue source)
    {
        public decimal ToDecimalValue() => (decimal)source.Units + (decimal)source.Nano / 1_000_000_000m;
    }

    extension(Quotation source)
    {
        public decimal ToDecimalValue() => (decimal)source.Units + (decimal)source.Nano / 1_000_000_000m;
    }

    public static decimal ToDecimal(MoneyValue? m) => m?.ToDecimalValue() ?? 0m;

    public static decimal QuotationToDecimal(Quotation? q) => q?.ToDecimalValue() ?? 0m;

    public static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(dt.ToUniversalTime());
}
