using ModelContextProtocol.Protocol;
using Tool = ModelContextProtocol.Protocol.Tool;

namespace TInvestMcp;

/// <summary>Каталог MCP-тулов. Согласован с <c>mcp-tools.manifest.json</c> и <c>docs/MCP-TOOLS.md</c> (генерация: <c>tools/ExportMcpManifest</c>).</summary>
internal static class ToolCatalog
{
    internal static List<Tool> Build() =>
    [
        new() { Name = "tinvest_get_accounts", Description = "Список счетов пользователя (брокерский, ИИС и т.д.).", InputSchema = ToolSchemas.Accounts() },
        new() { Name = "tinvest_get_portfolio", Description = "Портфель по счёту: стоимость по типам активов, доходность, позиции с ценами.", InputSchema = ToolSchemas.Portfolio() },
        new() { Name = "tinvest_get_positions", Description = "Позиции по счёту: бумаги, валюта, фьючерсы, опционы (без доходностей).", InputSchema = ToolSchemas.Positions() },
        new() { Name = "tinvest_get_withdraw_limits", Description = "Доступный остаток для вывода и заблокированные суммы (в т.ч. ГО по фьючерсам).", InputSchema = ToolSchemas.WithdrawLimits() },
        new() { Name = "tinvest_get_margin_attributes", Description = "Маржинальные показатели: ликвидный портфель, начальная/минимальная маржа, недостающие средства.", InputSchema = ToolSchemas.MarginAttributes() },
        new() { Name = "tinvest_find_instrument", Description = "Поиск инструмента по тикеру или названию. Возвращает FIGI, ticker, name, type.", InputSchema = ToolSchemas.FindInstrument() },
        new() { Name = "tinvest_get_dividends", Description = "История выплат дивидендов по инструменту и последняя выплата. Укажите ticker или figi.", InputSchema = ToolSchemas.Dividends() },
        new() { Name = "tinvest_get_operations", Description = "История операций по счёту с фильтрацией и пагинацией (по умолчанию limit=1000).", InputSchema = ToolSchemas.Operations() },
        new() { Name = "tinvest_generate_broker_report", Description = "Запуск формирования брокерского отчёта за период. Возвращает task_id для tinvest_get_broker_report.", InputSchema = ToolSchemas.GenerateBrokerReport() },
        new() { Name = "tinvest_get_broker_report", Description = "Получение готового брокерского отчёта по task_id. Пагинация через page (с 1).", InputSchema = ToolSchemas.GetBrokerReport() },
    ];
}
