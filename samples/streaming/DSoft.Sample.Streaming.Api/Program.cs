// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Text.Json;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.Streaming.Application.Streams;

var builder = WebApplication.CreateBuilder(args);

// Register mediator + handlers
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompileStreams();

var app = builder.Build();

// GET /countdown/{from} — streams integers from N to 1 as Server-Sent Events
app.MapGet("/countdown/{from:int}", async (int from, IMediator mediator, CancellationToken ct, HttpContext http) =>
{
    http.Response.ContentType = "text/event-stream";

    var stream = mediator.CreateStream(new CountdownStream(from), ct);

    await foreach (var tick in stream)
    {
        await http.Response.WriteAsync($"data: {tick}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    await http.Response.WriteAsync("data: [done]\n\n", ct);
    await http.Response.Body.FlushAsync(ct);
});

// GET /stocks/{symbol} — streams real-time stock price ticks as Server-Sent Events
app.MapGet("/stocks/{symbol}", async (string symbol, IMediator mediator, CancellationToken ct, HttpContext http) =>
{
    http.Response.ContentType = "text/event-stream";

    var stream = mediator.CreateStream(new StockPriceStream(symbol), ct);

    await foreach (var tick in stream)
    {
        var json = JsonSerializer.Serialize(tick);
        await http.Response.WriteAsync($"data: {json}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    await http.Response.WriteAsync("data: [done]\n\n", ct);
    await http.Response.Body.FlushAsync(ct);
});

app.Run();
