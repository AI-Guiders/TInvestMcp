# TInvestMcp — MCP-сервер для T-Bank Invest API (только данные)

Консольный MCP-сервер на C#: счета, портфель, позиции, лимиты вывода, маржа, дивиденды по инструменту и история операций. Только чтение, для финплана и анализа в Cursor/IDE.

## Требования

- .NET 10
- Токен доступа T-Invest API (только чтение подойдёт)

## Сборка

```bash
cd TInvestMcp
dotnet build
```

## Каталог тулов (автогенерация)

Полный список имён и текстов `description` — в [`docs/MCP-TOOLS.md`](docs/MCP-TOOLS.md); машиночитаемый манифест — [`mcp-tools.manifest.json`](mcp-tools.manifest.json). Обновление из корня `TInvestMcp`:

```bash
dotnet run --project tools/ExportMcpManifest -- --write
```

## Публикация exe (для MCP в Cursor)

Чтобы Cursor запускал MCP из **exe**, а не из проекта, сборка не будет блокироваться запущенным процессом.

В **csproj** заданы `RuntimeIdentifier=win-x64` и `SelfContained=true` — самодостаточная сборка (рантайм в папке, не зависит от установленного .NET в системе). Достаточно:

```bash
cd TInvestMcp
dotnet publish -c Release -o publish
```

Exe появится в `TInvestMcp/publish/TInvestMcp.exe`. В конфиге MCP в Cursor укажи этот exe (см. ниже).

Обновить exe после правок: снова выполни ту же команду publish, затем перезапусти MCP или Cursor.

## MCP в Cursor: запуск через exe

1. Один раз опубликовать: `dotnet publish -c Release -o publish` (из папки TInvestMcp; self-contained задан в csproj).
2. В настройках Cursor (Settings → MCP) для сервера **tinvest** заменить конфиг на запуск exe:
   - **command** — полный путь к `TInvestMcp.exe` в папке `publish`, например:  
     `C:\path\to\workspace\Financial\software\open\TInvestMcp\publish\TInvestMcp.exe`
   - **args** — пустой массив `[]`.
   - **env** (по желанию) — например `TINVEST_TOKEN_FILE` с путём к файлу с токеном.

Пример блока для вставки в конфиг MCP см. в файле **mcp-exe.example.json** (скопируй секцию `tinvest` в свой конфиг и поправь пути при необходимости).

## Запуск

Токен задаётся одним из способов (без хранения в конфиге Cursor в открытом виде):

1. **Переменная окружения `TINVEST_TOKEN`** — задать в системе или в профиле терминала. Тогда в конфиге MCP **не указывать** `env` с токеном — процесс унаследует окружение.
2. **Файл с токеном** — переменная **`TINVEST_TOKEN_FILE`** = путь к файлу, в котором лежит токен (одна строка). Файл добавить в .gitignore.

Сервер работает по **stdio**: Cursor (или другой MCP-клиент) запускает процесс и общается через stdin/stdout.

## Инструменты (tools)

| Инструмент | Описание |
|------------|----------|
| `tinvest_get_accounts` | Список счетов (брокерский, ИИС и т.д.) |
| `tinvest_get_portfolio` | Портфель по счёту (опционально `account_id`, `currency`: RUB/USD/EUR) |
| `tinvest_get_positions` | Позиции: бумаги, валюта, фьючерсы, опционы |
| `tinvest_get_withdraw_limits` | Доступный вывод и заблокированные суммы |
| `tinvest_get_margin_attributes` | Маржа: ликвидный портфель, начальная/минимальная маржа |
| `tinvest_find_instrument` | Поиск инструмента по тикеру/названию (`query`) — возвращает FIGI, ticker, name, type для подстановки в другие инструменты |
| `tinvest_get_dividends` | История выплат дивидендов по инструменту (`ticker` или `figi`, опционально `from`/`to`) |
| `tinvest_get_operations` | История операций по счёту с пагинацией: покупки, продажи, дивиденды, комиссии и т.д. Фильтры: `instrument_id`, `operation_types`, `state`, `from`/`to`, `cursor`, `limit` |
| `tinvest_generate_broker_report` | Запуск формирования брокерского отчёта за период (`from`, `to`). Возвращает `task_id` для следующего шага. |
| `tinvest_get_broker_report` | Получение готового брокерского отчёта по `task_id` (пагинация через `page`). |

## Работа с Cursor

Сервер передаёт в MCP инструкцию: при запросе данных (доход по тикеру, портфель, операции и т.п.) вызывать инструменты сразу, без предварительного плана шагов. Типичные сценарии:

- **Портфель или позиции** — вызвать `tinvest_get_portfolio` или `tinvest_get_positions` без аргументов (берётся первый счёт).
- **Доход по тикеру за период** — сначала `tinvest_find_instrument` по тикеру → взять `figi`, затем `tinvest_get_operations` с `instrument_id=figi`, `from`, `to`; для дивидендов — `tinvest_get_dividends` с `ticker` или `figi`.
- **Брокерский отчёт за период** — `tinvest_generate_broker_report(from, to)` → в ответе `task_id`, через несколько секунд `tinvest_get_broker_report(task_id)`.

Токен задаётся через `TINVEST_TOKEN` или `TINVEST_TOKEN_FILE` (в конфиге MCP или в окружении). Запуск через exe из `publish` рекомендуется, чтобы не блокировать сборку.

## Более эффективные запросы для отчётов

Для «финансовый результат за период» или сводки по операциям:

- **Брокерский отчёт** — API **GenerateBrokerReport** + **GetBrokerReport**: один раз формируется отчёт за период (по сделкам), затем забирается по страницам. Подходит для точных данных по сделкам; дивиденды/купоны/ввод-вывод в этот отчёт не входят.
- **Операции** — при использовании `tinvest_get_operations` задавать **limit=1000** (максимум), чтобы уменьшить число запросов при обходе по `cursor`.

Подробнее: [API_EFFICIENT_REQUESTS.md](API_EFFICIENT_REQUESTS.md).

## Эффективность работы MCP

- **Список инструментов** — собирается один раз при старте; при каждом ListTools возвращается тот же список без повторных аллокаций и сериализации схем.
- **Кэш account_id** — если несколько инструментов подряд вызываются без параметра `account_id`, первый счёт запрашивается у API один раз и на 3 секунды кэшируется, чтобы не дергать GetAccounts на каждый вызов (портфель, позиции, операции и т.д.).
- **Канал gRPC** — один долгоживущий канал на процесс, переиспользуется для всех запросов.

Рекомендации по эффективным запросам к API (брокерский отчёт, limit=1000 для операций) — в [API_EFFICIENT_REQUESTS.md](API_EFFICIENT_REQUESTS.md).

## Стек

- **.NET 10**, **C# 14** (extension members, primary constructors)
- **ModelContextProtocol** (C# SDK) — MCP-сервер, stdio
- **Tinkoff.InvestApi** — gRPC-клиент к T-Bank Invest API

Только чтение данных; торговые операции не выполняются.

## Структура проекта (рефакторинг)

- **Program.cs** — точка входа, создание канала и контекста API, регистрация инструментов и диспетчер вызовов.
- **ServerInstructions.cs** — текст инструкций для MCP-клиента (когда вызывать tinvest_*, не искать по репе).
- **ApiContext.cs** — клиенты gRPC (Users, Operations, Instruments) и метаданные авторизации.
- **ProtoHelpers.cs** — преобразование типов protobuf в decimal и Timestamp (C# 14 extension members для MoneyValue/Quotation).
- **ToolSchemas.cs** — JSON-схемы параметров каждого инструмента (InputSchema).
- **ToolHandlers.cs** — обработчики по имени инструмента (один метод на инструмент, диспетчер по `switch`).
