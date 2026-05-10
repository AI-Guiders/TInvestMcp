namespace TInvestMcp;

/// <summary>Текст инструкций для MCP-клиента (Cursor и др.).</summary>
internal static class ServerInstructions
{
    public const string Text = """
        T-Bank Invest API (только чтение). Данные по счёту, доходу по тикеру, портфелю, операциям приходят только через инструменты tinvest_* — в репозитории их нет. При таких запросах сразу вызывай MCP, не ищи по репе и не читай файлы проекта.
        Как вызывать: account_id можно не передавать. Доход по тикеру (напр. TPAY за 2024): tinvest_find_instrument(query) → figi, затем tinvest_get_operations с instrument_id, from, to и при необходимости tinvest_get_dividends. Портфель/позиции — без аргументов. Брокерский отчёт: tinvest_generate_broker_report(from, to) → task_id, затем tinvest_get_broker_report(task_id).
        """;
}
