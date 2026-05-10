namespace TInvestMcp;

using System.Text.Json;

/// <summary>JSON-схемы параметров инструментов MCP (для InputSchema).</summary>
internal static class ToolSchemas
{
    private static JsonElement ToElement(object schema) =>
        JsonSerializer.SerializeToElement(schema);

    public static JsonElement Accounts() =>
        ToElement(new { type = "object", properties = new { } });

    public static JsonElement Portfolio() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                account_id = new { type = "string", description = "ID счёта (если не указан — первый счёт)" },
                currency = new { type = "string", description = "Валюта расчёта: RUB, USD, EUR", @enum = new[] { "RUB", "USD", "EUR" } }
            }
        });

    public static JsonElement Positions() =>
        ToElement(new
        {
            type = "object",
            properties = new { account_id = new { type = "string", description = "ID счёта (опционально)" } }
        });

    public static JsonElement WithdrawLimits() =>
        ToElement(new
        {
            type = "object",
            properties = new { account_id = new { type = "string", description = "ID счёта (опционально)" } }
        });

    public static JsonElement MarginAttributes() =>
        ToElement(new
        {
            type = "object",
            properties = new { account_id = new { type = "string", description = "ID счёта (опционально)" } }
        });

    public static JsonElement FindInstrument() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Тикер или часть названия (например TPAY, SBCB, Сбербанк)." }
            },
            required = new[] { "query" }
        });

    public static JsonElement Dividends() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                ticker = new { type = "string", description = "Тикер инструмента (например TPAY). Если указан вместе с figi — figi имеет приоритет." },
                figi = new { type = "string", description = "FIGI инструмента. Если не указан, по ticker ищется через FindInstrument." },
                from = new { type = "string", description = "Начало периода (YYYY-MM-DD), опционально." },
                to = new { type = "string", description = "Конец периода (YYYY-MM-DD), опционально." }
            }
        });

    public static JsonElement Operations() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                account_id = new { type = "string", description = "ID счёта (если не указан — первый счёт)" },
                instrument_id = new { type = "string", description = "FIGI или UID инструмента (опционально, для фильтрации по конкретному инструменту)" },
                from = new { type = "string", description = "Начало периода (YYYY-MM-DD), по умолчанию — 1 месяц назад." },
                to = new { type = "string", description = "Конец периода (YYYY-MM-DD), по умолчанию — сейчас." },
                operation_types = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Типы операций для фильтрации, например: OPERATION_TYPE_DIVIDEND, OPERATION_TYPE_BUY, OPERATION_TYPE_SELL и т.д."
                },
                state = new
                {
                    type = "string",
                    description = "Статус операций: OPERATION_STATE_EXECUTED (исполненные), OPERATION_STATE_CANCELED, OPERATION_STATE_PROGRESS.",
                    @enum = new[] { "OPERATION_STATE_UNSPECIFIED", "OPERATION_STATE_EXECUTED", "OPERATION_STATE_CANCELED", "OPERATION_STATE_PROGRESS" }
                },
                cursor = new { type = "string", description = "Курсор для пагинации (из предыдущего ответа next_cursor)." },
                limit = new { type = "integer", description = "Количество операций (по умолчанию 1000, макс. 1000)." },
                without_commissions = new { type = "boolean", description = "Исключить комиссии из результатов. По умолчанию false." },
                without_trades = new { type = "boolean", description = "Не включать массив сделок. По умолчанию false." },
                without_overnights = new { type = "boolean", description = "Исключить overnight-операции. По умолчанию false." }
            }
        });

    public static JsonElement GenerateBrokerReport() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                account_id = new { type = "string", description = "ID счёта (если не указан — первый счёт)" },
                from = new { type = "string", description = "Начало периода (YYYY-MM-DD), обязательно." },
                to = new { type = "string", description = "Конец периода (YYYY-MM-DD), обязательно." }
            },
            required = new[] { "from", "to" }
        });

    public static JsonElement GetBrokerReport() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                task_id = new { type = "string", description = "Идентификатор задачи (из tinvest_generate_broker_report)." },
                page = new { type = "integer", description = "Номер страницы отчёта (начинается с 1). По умолчанию 1." }
            },
            required = new[] { "task_id" }
        });
}
