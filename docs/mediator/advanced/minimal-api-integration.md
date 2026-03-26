---
layout: default
title: "Minimal API Integration - DSoftStudio.Mediator"
description: "Patterns for wiring DSoftStudio.Mediator with ASP.NET Core Minimal APIs."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Minimal API Integration

DSoftStudio.Mediator works with ASP.NET Core Minimal APIs out of the box — no extra packages, no code generation, no magic conventions. Inject `ISender` into your endpoint and call `Send`.

Every endpoint follows the same shape:

```csharp
app.MapGet("/path", async (ISender sender, CancellationToken ct) =>
    await sender.Send(new YourRequest(), ct));
```

The mediator handles dispatch, pipeline behaviors, and handler resolution. The endpoint only handles HTTP concerns: routing, parameter binding, status codes, and OpenAPI metadata.

---

## Recipes

### 1. Query with Route Parameter → GET with NotFound

Fetch a single resource by ID. Return `200 OK` with the data, or `404 Not Found` when the resource doesn't exist.

**Request:**
```csharp
public record GetUserQuery(int Id) : IQuery<UserDto?>;
```

**Endpoint:**
```csharp
app.MapGet("/users/{id}", async (int id, ISender sender, CancellationToken ct) =>
    await sender.Send(new GetUserQuery(id), ct) is { } user
        ? Results.Ok(user)
        : Results.NotFound())
    .WithName("GetUser")
    .Produces<UserDto>(200)
    .ProducesProblem(404);
```

---

### 2. Command with Body → POST with Created

Accept a JSON body, create a resource, and return `201 Created` with a `Location` header pointing to the new resource.

**Request:**
```csharp
public record CreateOrderCommand(string CustomerId, List<OrderItem> Items)
    : ICommand<OrderId>;
```

**Endpoint:**
```csharp
app.MapPost("/orders", async (CreateOrderCommand cmd, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(cmd, ct);
    return Results.Created($"/orders/{result.Value}", result);
})
    .WithName("CreateOrder")
    .Produces<OrderId>(201);
```

> **Note:** ASP.NET Core Minimal APIs bind the body from JSON automatically for complex types in POST/PUT/PATCH endpoints.

---

### 3. Void Command → DELETE / PUT with NoContent

Execute a side-effect command that returns no data. Return `204 No Content` on success.

**Request:**
```csharp
public record DeleteOrderCommand(int Id) : ICommand<Unit>;
```

**Endpoint (DELETE):**
```csharp
app.MapDelete("/orders/{id}", async (int id, ISender sender, CancellationToken ct) =>
{
    await sender.Send(new DeleteOrderCommand(id), ct);
    return Results.NoContent();
})
    .WithName("DeleteOrder")
    .Produces(204);
```

**Endpoint (PUT):**
```csharp
app.MapPut("/orders/{id}/cancel", async (int id, ISender sender, CancellationToken ct) =>
{
    await sender.Send(new CancelOrderCommand(id), ct);
    return Results.NoContent();
})
    .WithName("CancelOrder")
    .Produces(204);
```

---

### 4. Query with Pagination → GET with QueryString

Bind multiple query string parameters to a request record. Return a paginated response.

**Request:**
```csharp
public record ListOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null) : IQuery<PagedResult<OrderSummaryDto>>;
```

**Endpoint:**
```csharp
app.MapGet("/orders", async ([AsParameters] ListOrdersQuery query, ISender sender, CancellationToken ct) =>
    Results.Ok(await sender.Send(query, ct)))
    .WithName("ListOrders")
    .Produces<PagedResult<OrderSummaryDto>>(200);
```

`[AsParameters]` binds query string parameters directly to the record constructor — no manual mapping:

```
GET /orders?page=2&pageSize=10&status=shipped
→ new ListOrdersQuery(Page: 2, PageSize: 10, Status: "shipped")
```

---

### 5. Command with Authorization → POST with RequireAuthorization

Protect an endpoint with ASP.NET Core authorization. Authorization is an HTTP concern — it stays on the endpoint, not on the mediator pipeline.

**Request:**
```csharp
public record RefundOrderCommand(int OrderId, decimal Amount, string Reason)
    : ICommand<RefundResult>;
```

**Endpoint:**
```csharp
app.MapPost("/orders/{orderId}/refund", async (
    int orderId, RefundOrderCommand cmd, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(cmd with { OrderId = orderId }, ct);
    return Results.Ok(result);
})
    .RequireAuthorization("ManagerPolicy")
    .WithName("RefundOrder")
    .Produces<RefundResult>(200)
    .ProducesProblem(403);
```

For role-based or policy-based authorization:

```csharp
// Single policy
.RequireAuthorization("AdminPolicy")

// Multiple policies (ALL must pass)
.RequireAuthorization("AdminPolicy", "MfaRequired")

// Anonymous access (override global auth)
.AllowAnonymous()
```

> **Why not authorize in the pipeline?** A `ValidationBehavior` or `AuthorizationBehavior` in the mediator pipeline *can* check business rules ("can this user refund orders over $500?"). But HTTP-level auth ("is this request authenticated?") belongs on the endpoint. Mixing them creates confusion about which layer rejects the request and what status code the client receives.

---

## Putting It Together

Group related endpoints in a static class and register them in `Program.cs`:

```csharp
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders");

        group.MapGet("/", ListOrders);
        group.MapPost("/", CreateOrder).RequireAuthorization();
        group.MapDelete("/{id}", DeleteOrder).RequireAuthorization("AdminPolicy");
        group.MapPost("/{orderId}/refund", RefundOrder).RequireAuthorization("ManagerPolicy");
    }

    // ... endpoint methods from the recipes above
}
```

```csharp
// Program.cs
var app = builder.Build();

app.MapOrderEndpoints();
app.MapUserEndpoints();

app.Run();
```

---

## Why Not a Source Generator for Endpoints?

DSoftStudio.Mediator intentionally does **not** auto-generate Minimal API endpoints from request types:

1. **HTTP is opinionated** — Every endpoint requires decisions about status codes, route design, authorization, versioning, content negotiation, and OpenAPI metadata. A generator either makes those decisions for you (wrong) or forces you to configure them via attributes (same effort as writing the endpoint).

2. **The boilerplate is minimal** — Each endpoint is 3–5 lines. For 20 endpoints, that's ~80 lines in a single file. The explicitness is a feature, not a bug.

3. **Full control** — You own the HTTP surface. Need `Results.Created` with a custom location header? Need `[Authorize("AdminPolicy")]` on one endpoint but not another? Need endpoint filters for rate limiting? It's all standard ASP.NET Core — no framework-specific workarounds.

The mediator's job is to dispatch requests through the pipeline. The endpoint's job is to translate HTTP into requests. Keeping them separate is intentional.
