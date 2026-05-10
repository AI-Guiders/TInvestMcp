# T-Invest MCP — каталог тулов

<!-- GENERATED:ToolCatalog START -->

> Автогенерация из `ToolCatalog.Build()`. Не править этот блок вручную.
>
> Обновление: из каталога `TInvestMcp` выполнить `dotnet run --project tools/ExportMcpManifest -- --write`.
>
> Тексты совпадают с полем `description` у инструментов MCP; полная схема — в `inputSchema`.

### `tinvest_get_accounts`

Список счетов пользователя (брокерский, ИИС и т.д.).

### `tinvest_get_portfolio`

Портфель по счёту: стоимость по типам активов, доходность, позиции с ценами.

### `tinvest_get_positions`

Позиции по счёту: бумаги, валюта, фьючерсы, опционы (без доходностей).

### `tinvest_get_withdraw_limits`

Доступный остаток для вывода и заблокированные суммы (в т.ч. ГО по фьючерсам).

### `tinvest_get_margin_attributes`

Маржинальные показатели: ликвидный портфель, начальная/минимальная маржа, недостающие средства.

### `tinvest_find_instrument`

Поиск инструмента по тикеру или названию. Возвращает FIGI, ticker, name, type.

### `tinvest_get_dividends`

История выплат дивидендов по инструменту и последняя выплата. Укажите ticker или figi.

### `tinvest_get_operations`

История операций по счёту с фильтрацией и пагинацией (по умолчанию limit=1000).

### `tinvest_generate_broker_report`

Запуск формирования брокерского отчёта за период. Возвращает task_id для tinvest_get_broker_report.

### `tinvest_get_broker_report`

Получение готового брокерского отчёта по task_id. Пагинация через page (с 1).

<!-- GENERATED:ToolCatalog END -->

