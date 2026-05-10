namespace TInvestMcp;

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Tinkoff.InvestApi.V1;

/// <summary>Обработчики вызовов инструментов MCP. Диспетчер по имени инструмента.</summary>
internal static class ToolHandlers
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Краткий кэш первого счёта, чтобы при серии вызовов без account_id не дергать GetAccounts на каждый инструмент.</summary>
    private static readonly TimeSpan AccountIdCacheTtl = TimeSpan.FromSeconds(3);
    private static (string? Id, DateTime ValidUntil) _accountIdCache;
    private static readonly object AccountIdCacheLock = new();

    public static async Task<string> HandleAsync(
        string name,
        ApiContext ctx,
        IReadOnlyDictionary<string, JsonElement> args,
        CancellationToken ct)
    {
        var argsRo = args ?? FrozenDictionary<string, JsonElement>.Empty;
        string GetArg(string key) => argsRo.TryGetValue(key, out var e) ? e.GetString() ?? "" : "";

        return name switch
        {
            "tinvest_get_accounts" => await HandleGetAccountsAsync(ctx, ct),
            "tinvest_get_portfolio" => await HandleGetPortfolioAsync(ctx, GetArg, ct),
            "tinvest_get_positions" => await HandleGetPositionsAsync(ctx, GetArg, ct),
            "tinvest_get_withdraw_limits" => await HandleGetWithdrawLimitsAsync(ctx, GetArg, ct),
            "tinvest_get_margin_attributes" => await HandleGetMarginAttributesAsync(ctx, GetArg, ct),
            "tinvest_find_instrument" => await HandleFindInstrumentAsync(ctx, GetArg, ct),
            "tinvest_get_dividends" => await HandleGetDividendsAsync(ctx, GetArg, ct),
            "tinvest_get_operations" => await HandleGetOperationsAsync(ctx, GetArg, argsRo, ct),
            "tinvest_generate_broker_report" => await HandleGenerateBrokerReportAsync(ctx, GetArg, ct),
            "tinvest_get_broker_report" => await HandleGetBrokerReportAsync(ctx, GetArg, ct),
            _ => throw new McpProtocolException($"Unknown tool: {name}", McpErrorCode.InvalidRequest)
        };
    }

    private static async Task<string> ResolveAccountIdAsync(ApiContext ctx, string accountId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(accountId)) return accountId;
        var now = DateTime.UtcNow;
        lock (AccountIdCacheLock)
        {
            if (_accountIdCache.ValidUntil > now && !string.IsNullOrEmpty(_accountIdCache.Id))
                return _accountIdCache.Id;
        }
        var list = await ctx.Users.GetAccountsAsync(new GetAccountsRequest(), ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var id = list.Accounts.FirstOrDefault()?.Id ?? "";
        lock (AccountIdCacheLock)
        {
            _accountIdCache = (id, now.Add(AccountIdCacheTtl));
        }
        return id;
    }

    private static async Task<string> HandleGetAccountsAsync(ApiContext ctx, CancellationToken ct)
    {
        var accResp = await ctx.Users.GetAccountsAsync(new GetAccountsRequest(), ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var accounts = accResp.Accounts.Select(a => new
        {
            id = a.Id,
            name = a.Name,
            type = a.Type.ToString(),
            status = a.Status.ToString(),
            opened_date = a.OpenedDate?.ToDateTime().ToString("O"),
            access_level = a.AccessLevel.ToString()
        }).ToList();
        return JsonSerializer.Serialize(new { accounts }, JsonOptions);
    }

    private static async Task<string> HandleGetPortfolioAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(ctx, getArg("account_id"), ct);
        var currencyArg = getArg("currency");
        var currencyReq = currencyArg?.ToUpperInvariant() switch
        {
            "USD" => PortfolioRequest.Types.CurrencyRequest.Usd,
            "EUR" => PortfolioRequest.Types.CurrencyRequest.Eur,
            _ => PortfolioRequest.Types.CurrencyRequest.Rub
        };
        var portResp = await ctx.Operations.GetPortfolioAsync(
            new PortfolioRequest { AccountId = accountId, Currency = currencyReq },
            ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var portfolio = new
        {
            account_id = portResp.AccountId,
            total_amount_portfolio = ProtoHelpers.ToDecimal(portResp.TotalAmountPortfolio),
            total_amount_shares = ProtoHelpers.ToDecimal(portResp.TotalAmountShares),
            total_amount_bonds = ProtoHelpers.ToDecimal(portResp.TotalAmountBonds),
            total_amount_etf = ProtoHelpers.ToDecimal(portResp.TotalAmountEtf),
            total_amount_currencies = ProtoHelpers.ToDecimal(portResp.TotalAmountCurrencies),
            total_amount_futures = ProtoHelpers.ToDecimal(portResp.TotalAmountFutures),
            expected_yield_pct = ProtoHelpers.QuotationToDecimal(portResp.ExpectedYield),
            positions_count = portResp.Positions.Count,
            positions = portResp.Positions.Select(p => new
            {
                figi = p.Figi,
                instrument_type = p.InstrumentType,
                quantity = ProtoHelpers.QuotationToDecimal(p.Quantity),
                current_price = ProtoHelpers.ToDecimal(p.CurrentPrice),
                average_position_price = ProtoHelpers.ToDecimal(p.AveragePositionPrice),
                expected_yield_pct = ProtoHelpers.QuotationToDecimal(p.ExpectedYield)
            }).ToList()
        };
        return JsonSerializer.Serialize(portfolio, JsonOptions);
    }

    private static async Task<string> HandleGetPositionsAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(ctx, getArg("account_id"), ct);
        var posResp = await ctx.Operations.GetPositionsAsync(
            new PositionsRequest { AccountId = accountId },
            ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var positions = new
        {
            account_id = accountId,
            money = posResp.Money.Select(m => new { currency = m.Currency, value = ProtoHelpers.ToDecimal(m) }).ToList(),
            blocked = posResp.Blocked.Select(m => new { currency = m.Currency, value = ProtoHelpers.ToDecimal(m) }).ToList(),
            securities = posResp.Securities.Select(s => new { figi = s.Figi, balance = s.Balance, blocked = s.Blocked, instrument_type = s.InstrumentType }).ToList(),
            futures = posResp.Futures.Select(f => new { figi = f.Figi, balance = f.Balance, blocked = f.Blocked }).ToList()
        };
        return JsonSerializer.Serialize(positions, JsonOptions);
    }

    private static async Task<string> HandleGetWithdrawLimitsAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(ctx, getArg("account_id"), ct);
        var wResp = await ctx.Operations.GetWithdrawLimitsAsync(
            new WithdrawLimitsRequest { AccountId = accountId },
            ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var limits = new
        {
            account_id = accountId,
            money = wResp.Money.Select(m => new { currency = m.Currency, value = ProtoHelpers.ToDecimal(m) }).ToList(),
            blocked = wResp.Blocked.Select(m => new { currency = m.Currency, value = ProtoHelpers.ToDecimal(m) }).ToList(),
            blocked_guarantee = wResp.BlockedGuarantee.Select(m => new { currency = m.Currency, value = ProtoHelpers.ToDecimal(m) }).ToList()
        };
        return JsonSerializer.Serialize(limits, JsonOptions);
    }

    private static async Task<string> HandleGetMarginAttributesAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(ctx, getArg("account_id"), ct);
        var marginReq = new GetMarginAttributesRequest { AccountId = accountId };
        var marginResp = await ctx.Users.GetMarginAttributesAsync(marginReq, ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var margin = new
        {
            account_id = accountId,
            liquid_portfolio = ProtoHelpers.ToDecimal(marginResp.LiquidPortfolio),
            starting_margin = ProtoHelpers.ToDecimal(marginResp.StartingMargin),
            minimal_margin = ProtoHelpers.ToDecimal(marginResp.MinimalMargin),
            funds_sufficiency_level = ProtoHelpers.QuotationToDecimal(marginResp.FundsSufficiencyLevel),
            amount_of_missing_funds = ProtoHelpers.ToDecimal(marginResp.AmountOfMissingFunds),
            corrected_margin = ProtoHelpers.ToDecimal(marginResp.CorrectedMargin)
        };
        return JsonSerializer.Serialize(margin, JsonOptions);
    }

    private static async Task<string> HandleFindInstrumentAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var query = getArg("query");
        if (string.IsNullOrWhiteSpace(query))
            throw new McpProtocolException("Укажите query (тикер или название).", McpErrorCode.InvalidRequest);
        var findResp = await ctx.Instruments.FindInstrumentAsync(
            new FindInstrumentRequest { Query = query.Trim() },
            ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var findList = findResp.Instruments
            .Where(i => !string.IsNullOrWhiteSpace(i.Figi))
            .Select(i => new { figi = i.Figi, ticker = i.Ticker, name = i.Name, instrument_type = i.InstrumentType })
            .ToList();
        return JsonSerializer.Serialize(new { query = query.Trim(), count = findList.Count, instruments = findList }, JsonOptions);
    }

    private static async Task<string> HandleGetDividendsAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var figi = getArg("figi");
        var ticker = getArg("ticker");
        if (string.IsNullOrWhiteSpace(figi) && string.IsNullOrWhiteSpace(ticker))
            throw new McpProtocolException("Укажите ticker или figi.", McpErrorCode.InvalidRequest);
        if (string.IsNullOrWhiteSpace(figi) && !string.IsNullOrWhiteSpace(ticker))
        {
            var findResp = await ctx.Instruments.FindInstrumentAsync(
                new FindInstrumentRequest { Query = ticker.Trim() },
                ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
            figi = findResp.Instruments.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Figi))?.Figi ?? "";
            if (string.IsNullOrWhiteSpace(figi))
                throw new McpProtocolException($"Инструмент по тикеру «{ticker}» не найден.", McpErrorCode.InvalidRequest);
        }
        else if (!string.IsNullOrWhiteSpace(figi))
            figi = figi.Trim();

        var fromStr = getArg("from");
        var toStr = getArg("to");
        var fromDt = !string.IsNullOrWhiteSpace(fromStr) && DateTime.TryParse(fromStr, out var fd) ? fd : DateTime.UtcNow.AddYears(-5);
        var toDt = !string.IsNullOrWhiteSpace(toStr) && DateTime.TryParse(toStr, out var td) ? td : DateTime.UtcNow.AddYears(1);

#pragma warning disable CS0612
        var divReq = new GetDividendsRequest
        {
            Figi = figi,
            From = ProtoHelpers.ToTimestamp(fromDt),
            To = ProtoHelpers.ToTimestamp(toDt)
        };
#pragma warning restore CS0612
        var divResp = await ctx.Instruments.GetDividendsAsync(divReq, ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var divList = divResp.Dividends.Select(d => new
        {
            payment_date = d.PaymentDate?.ToDateTime().ToString("O"),
            declared_date = d.DeclaredDate?.ToDateTime().ToString("O"),
            record_date = d.RecordDate?.ToDateTime().ToString("O"),
            last_buy_date = d.LastBuyDate?.ToDateTime().ToString("O"),
            dividend_net = ProtoHelpers.ToDecimal(d.DividendNet),
            dividend_type = d.DividendType.ToString(),
            regularity = d.Regularity.ToString(),
            close_price = ProtoHelpers.ToDecimal(d.ClosePrice),
            yield_value = ProtoHelpers.QuotationToDecimal(d.YieldValue)
        }).ToList();
        var lastPayout = divList.Where(x => !string.IsNullOrEmpty(x.payment_date)).OrderByDescending(x => x.payment_date).FirstOrDefault();
        var result = new
        {
            figi,
            ticker,
            from = fromDt.ToString("yyyy-MM-dd"),
            to = toDt.ToString("yyyy-MM-dd"),
            total_count = divList.Count,
            dividends = divList,
            last_payout = lastPayout
        };
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private static async Task<string> HandleGetOperationsAsync(
        ApiContext ctx,
        Func<string, string> getArg,
        IReadOnlyDictionary<string, JsonElement> args,
        CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(ctx, getArg("account_id"), ct);
        var fromStr = getArg("from");
        var toStr = getArg("to");
        var opsFrom = !string.IsNullOrWhiteSpace(fromStr) && DateTime.TryParse(fromStr, out var ofd) ? ofd : DateTime.UtcNow.AddMonths(-1);
        var opsTo = !string.IsNullOrWhiteSpace(toStr) && DateTime.TryParse(toStr, out var otd) ? otd : DateTime.UtcNow;

        var opTypes = new List<OperationType>();
        if (args.TryGetValue("operation_types", out var opTypesEl) && opTypesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in opTypesEl.EnumerateArray())
            {
                var str = el.GetString() ?? "";
                if (System.Enum.TryParse<OperationType>(str, ignoreCase: true, out var parsed))
                    opTypes.Add(parsed);
            }
        }

        var stateStr = getArg("state");
        var opsState = OperationState.Executed;
        if (!string.IsNullOrWhiteSpace(stateStr))
            System.Enum.TryParse(stateStr, ignoreCase: true, out opsState);

        static bool GetBool(IReadOnlyDictionary<string, JsonElement> a, string key) =>
            a.TryGetValue(key, out var e) && e.ValueKind == JsonValueKind.True;

        var opsReq = new GetOperationsByCursorRequest
        {
            AccountId = accountId,
            From = ProtoHelpers.ToTimestamp(opsFrom),
            To = ProtoHelpers.ToTimestamp(opsTo),
            State = opsState,
            WithoutCommissions = GetBool(args, "without_commissions"),
            WithoutTrades = GetBool(args, "without_trades"),
            WithoutOvernights = GetBool(args, "without_overnights"),
            Limit = 1000
        };
        var instrId = getArg("instrument_id");
        if (!string.IsNullOrWhiteSpace(instrId)) opsReq.InstrumentId = instrId.Trim();
        var cursor = getArg("cursor");
        if (!string.IsNullOrWhiteSpace(cursor)) opsReq.Cursor = cursor;
        var limitStr = getArg("limit");
        if (!string.IsNullOrWhiteSpace(limitStr) && int.TryParse(limitStr, out var lim))
            opsReq.Limit = Math.Clamp(lim, 1, 1000);
        foreach (var ot in opTypes)
            opsReq.OperationTypes.Add(ot);

        var opsResp = await ctx.Operations.GetOperationsByCursorAsync(opsReq, ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var opsList = opsResp.Items.Select(op => new
        {
            id = op.Id,
            date = op.Date?.ToDateTime().ToString("O"),
            type = op.Type.ToString(),
            state = op.State.ToString(),
            name = op.Name,
            description = op.Description,
            figi = op.Figi,
            instrument_type = op.InstrumentType,
            instrument_kind = op.InstrumentKind.ToString(),
            payment = ProtoHelpers.ToDecimal(op.Payment),
            payment_currency = op.Payment?.Currency,
            price = ProtoHelpers.ToDecimal(op.Price),
            commission = ProtoHelpers.ToDecimal(op.Commission),
            quantity = op.Quantity,
            quantity_done = op.QuantityDone,
            accrued_int = ProtoHelpers.ToDecimal(op.AccruedInt),
            instrument_uid = op.InstrumentUid,
            asset_uid = op.AssetUid
        }).ToList();
        var opsResult = new
        {
            account_id = accountId,
            from = opsFrom.ToString("yyyy-MM-dd"),
            to = opsTo.ToString("yyyy-MM-dd"),
            total_count = opsList.Count,
            has_next = opsResp.HasNext,
            next_cursor = opsResp.NextCursor,
            operations = opsList
        };
        return JsonSerializer.Serialize(opsResult, JsonOptions);
    }

    private static async Task<string> HandleGenerateBrokerReportAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(ctx, getArg("account_id"), ct);
        var fromStr = getArg("from");
        var toStr = getArg("to");
        if (string.IsNullOrWhiteSpace(fromStr) || string.IsNullOrWhiteSpace(toStr))
            throw new McpProtocolException("Укажите from и to (YYYY-MM-DD).", McpErrorCode.InvalidRequest);
        if (!DateTime.TryParse(fromStr, out var fromDt) || !DateTime.TryParse(toStr, out var toDt))
            throw new McpProtocolException("Некорректный формат дат from/to.", McpErrorCode.InvalidRequest);

        var genReq = new BrokerReportRequest
        {
            GenerateBrokerReportRequest = new GenerateBrokerReportRequest
            {
                AccountId = accountId,
                From = ProtoHelpers.ToTimestamp(fromDt),
                To = ProtoHelpers.ToTimestamp(toDt)
            }
        };
        var genResp = await ctx.Operations.GetBrokerReportAsync(genReq, ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var taskId = genResp.GenerateBrokerReportResponse?.TaskId ?? "";
        if (string.IsNullOrEmpty(taskId))
            throw new McpProtocolException("Не получен task_id от API.", McpErrorCode.InvalidRequest);
        return JsonSerializer.Serialize(new
        {
            task_id = taskId,
            account_id = accountId,
            from = fromDt.ToString("yyyy-MM-dd"),
            to = toDt.ToString("yyyy-MM-dd"),
            hint = "Вызовите tinvest_get_broker_report с этим task_id (отчёт может формироваться несколько секунд)."
        }, JsonOptions);
    }

    private static async Task<string> HandleGetBrokerReportAsync(ApiContext ctx, Func<string, string> getArg, CancellationToken ct)
    {
        var taskId = getArg("task_id");
        if (string.IsNullOrWhiteSpace(taskId))
            throw new McpProtocolException("Укажите task_id из tinvest_generate_broker_report.", McpErrorCode.InvalidRequest);
        var page = 1;
        var pageStr = getArg("page");
        if (!string.IsNullOrWhiteSpace(pageStr) && int.TryParse(pageStr, out var p) && p >= 1)
            page = p;

        var getReq = new BrokerReportRequest
        {
            GetBrokerReportRequest = new GetBrokerReportRequest
            {
                TaskId = taskId.Trim(),
                Page = page
            }
        };
        var resp = await ctx.Operations.GetBrokerReportAsync(getReq, ctx.Metadata, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        var reportResp = resp.GetBrokerReportResponse;
        if (reportResp == null)
            throw new McpProtocolException("Ответ API не содержит отчёт (возможно, task_id ещё не готов — подождите и повторите).", McpErrorCode.InvalidRequest);

        var reportList = reportResp.BrokerReport.Select(r => new
        {
            trade_id = r.TradeId,
            order_id = r.OrderId,
            figi = r.Figi,
            ticker = r.Ticker,
            name = r.Name,
            direction = r.Direction,
            trade_datetime = r.TradeDatetime?.ToDateTime().ToString("O"),
            quantity = r.Quantity,
            price = ProtoHelpers.ToDecimal(r.Price),
            order_amount = ProtoHelpers.ToDecimal(r.OrderAmount),
            aci_value = ProtoHelpers.QuotationToDecimal(r.AciValue),
            total_order_amount = ProtoHelpers.ToDecimal(r.TotalOrderAmount),
            broker_commission = ProtoHelpers.ToDecimal(r.BrokerCommission),
            exchange_commission = ProtoHelpers.ToDecimal(r.ExchangeCommission),
            exchange_clearing_commission = ProtoHelpers.ToDecimal(r.ExchangeClearingCommission),
            clear_value_date = r.ClearValueDate?.ToDateTime().ToString("yyyy-MM-dd"),
            sec_value_date = r.SecValueDate?.ToDateTime().ToString("yyyy-MM-dd")
        }).ToList();
        return JsonSerializer.Serialize(new
        {
            task_id = taskId,
            page = reportResp.Page,
            pages_count = reportResp.PagesCount,
            items_count = reportResp.ItemsCount,
            broker_report = reportList
        }, JsonOptions);
    }
}
