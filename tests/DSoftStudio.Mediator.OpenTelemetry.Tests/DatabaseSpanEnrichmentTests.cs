// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

/// <summary>
/// Covers the redaction-safe SQL shape parser, the <see cref="DatabaseSpanEnrichmentProcessor"/>
/// rules, and the automatic wiring through <c>AddMediatorInstrumentation()</c> — proving a
/// statement-only database span is split into a distinct operation in the live tracer pipeline.
/// </summary>
[Collection("OTel")]
public class DatabaseSpanEnrichmentTests
{
    // ── Parser: operation ─────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT i.unit_price FROM inventory i WHERE i.sku = $1", "SELECT")]
    [InlineData("INSERT INTO orders (id, total) VALUES ($1, $2)", "INSERT")]
    [InlineData("update inventory set qty = qty - $1 where sku = $2", "UPDATE")]
    [InlineData("DELETE FROM orders WHERE id = $1", "DELETE")]
    [InlineData("  \n\t SELECT 1", "SELECT")]
    [InlineData("/* hint */ -- comment\n SELECT 1", "SELECT")]
    [InlineData("WITH recent AS (SELECT id FROM orders) INSERT INTO audit SELECT id FROM recent", "INSERT")]
    [InlineData("EXEC sp_DoThing", "EXECUTE")]
    public void Operation_extracts_the_leading_verb(string sql, string expected)
        => SqlStatementParser.Operation(sql).ShouldBe(expected);

    [Fact]
    public void Operation_ignores_keywords_inside_string_literals()
        // The 'INSERT' here is a value, not the operation — the statement is a SELECT.
        => SqlStatementParser.Operation("SELECT 'INSERT INTO x' AS note FROM t").ShouldBe("SELECT");

    // ── ORM-generated SQL (the real shape an EF Core → Npgsql span carries) ─

    [Theory]
    // EF Core quotes every identifier and parameterizes values — the parser unwraps the quotes and the alias.
    [InlineData("SELECT i.\"UnitPrice\" FROM \"Inventory\" AS i WHERE i.\"Sku\" = @__sku_0", "SELECT", "Inventory")]
    [InlineData("INSERT INTO \"Orders\" (\"Id\", \"Total\") VALUES (@p0, @p1)", "INSERT", "Orders")]
    [InlineData("UPDATE \"Inventory\" SET \"Qty\" = @p0 WHERE \"Id\" = @p1", "UPDATE", "Inventory")]
    [InlineData("SELECT o.\"Id\" FROM \"public\".\"Orders\" AS o", "SELECT", "Orders")] // schema-qualified → table segment
    public void Orm_generated_sql_is_parsed_for_operation_and_table(string sql, string op, string table)
    {
        SqlStatementParser.Operation(sql).ShouldBe(op);
        SqlStatementParser.Table(sql).ShouldBe(table);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("BEGIN TRANSACTION")]
    [InlineData("(SELECT 1)")] // wrapped sub-select has no depth-0 verb
    public void Operation_returns_null_when_no_verb_is_found(string? sql)
        => SqlStatementParser.Operation(sql).ShouldBeNull();

    // ── Parser: table ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT * FROM inventory WHERE sku = $1", "inventory")]
    [InlineData("SELECT i.unit_price FROM public.inventory i", "inventory")]
    [InlineData("INSERT INTO orders (id) VALUES ($1)", "orders")]
    [InlineData("UPDATE inventory SET qty = $1", "inventory")]
    [InlineData("DELETE FROM orders WHERE id = $1", "orders")]
    [InlineData("SELECT * FROM \"Order Items\" oi", "Order Items")]
    [InlineData("SELECT a.x FROM orders a JOIN items b ON a.id = b.oid", "orders")] // first source table
    public void Table_extracts_the_target_identifier(string sql, string expected)
        => SqlStatementParser.Table(sql).ShouldBe(expected);

    [Theory]
    [InlineData("EXEC sp_DoThing")]   // no table anchor
    [InlineData("SELECT 1")]          // no FROM
    [InlineData("")]
    public void Table_returns_null_when_indeterminate(string sql)
        => SqlStatementParser.Table(sql).ShouldBeNull();

    // ── Parser: stored procedure ──────────────────────────────────────────

    [Theory]
    [InlineData("CALL create_order($1, $2)", "create_order")]
    [InlineData("CALL billing.charge_card($1)", "charge_card")]
    [InlineData("EXEC sp_PlaceOrder @customerId = $1", "sp_PlaceOrder")]
    [InlineData("EXECUTE dbo.RecalculateTotals", "RecalculateTotals")]
    public void Procedure_extracts_the_invoked_routine(string sql, string expected)
        => SqlStatementParser.Procedure(sql).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT * FROM orders")] // not a procedure call
    [InlineData("EXEC ('dynamic sql here')")] // dynamic EXEC — no static name
    [InlineData("")]
    public void Procedure_returns_null_when_not_a_call(string sql)
        => SqlStatementParser.Procedure(sql).ShouldBeNull();

    // ── Processor rules ───────────────────────────────────────────────────

    private static Activity NewSpan(Action<Activity> configure)
    {
        var activity = new Activity("db.query");
        activity.Start();
        configure(activity);
        return activity;
    }

    [Fact]
    public void Processor_enriches_a_statement_only_db_span()
    {
        var processor = new DatabaseSpanEnrichmentProcessor();
        using var span = NewSpan(a =>
        {
            a.SetTag("db.system", "postgresql");
            a.SetTag("db.statement", "INSERT INTO orders (id) VALUES ($1)");
        });

        processor.OnEnd(span);

        span.GetTagItem("db.operation.name").ShouldBe("INSERT");
        span.GetTagItem("db.sql.table").ShouldBe("orders");
    }

    [Fact]
    public void Processor_enriches_a_stored_procedure_call()
    {
        var processor = new DatabaseSpanEnrichmentProcessor();
        using var span = NewSpan(a =>
        {
            a.SetTag("db.system", "postgresql");
            a.SetTag("db.statement", "CALL create_order($1, $2)");
        });

        processor.OnEnd(span);

        span.GetTagItem("db.operation.name").ShouldBe("CALL");
        span.GetTagItem("db.stored_procedure.name").ShouldBe("create_order");
        span.GetTagItem("db.sql.table").ShouldBeNull(); // a CALL has no DML table
    }

    [Fact]
    public void Processor_never_overwrites_native_operation()
    {
        var processor = new DatabaseSpanEnrichmentProcessor();
        using var span = NewSpan(a =>
        {
            a.SetTag("db.system", "postgresql");
            a.SetTag("db.operation.name", "BATCH"); // supplied by native instrumentation
            a.SetTag("db.statement", "INSERT INTO orders (id) VALUES ($1)");
        });

        processor.OnEnd(span);

        span.GetTagItem("db.operation.name").ShouldBe("BATCH");
    }

    [Fact]
    public void Processor_ignores_non_database_spans()
    {
        var processor = new DatabaseSpanEnrichmentProcessor();
        using var span = NewSpan(a => a.SetTag("http.request.method", "GET"));

        processor.OnEnd(span);

        span.GetTagItem("db.operation.name").ShouldBeNull();
    }

    [Fact]
    public void Processor_is_a_no_op_when_statement_is_absent()
    {
        var processor = new DatabaseSpanEnrichmentProcessor();
        using var span = NewSpan(a => a.SetTag("db.system", "postgresql"));

        processor.OnEnd(span);

        span.GetTagItem("db.operation.name").ShouldBeNull();
    }

    // ── End-to-end wiring (proves the automatic registration runs) ─────────

    [Fact]
    public void AddMediatorInstrumentation_auto_enriches_db_spans_in_the_pipeline()
    {
        using var dbSource = new ActivitySource("Test.Db.Enrichment");
        using var provider = global::OpenTelemetry.Sdk.CreateTracerProviderBuilder()
            .AddMediatorInstrumentation() // registers the enricher — no extra config
            .AddSource(dbSource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        var span = dbSource.StartActivity("ordersdb", ActivityKind.Client);
        span.ShouldNotBeNull();
        span.SetTag("db.system", "postgresql");
        span.SetTag("db.statement", "SELECT i.unit_price FROM inventory i WHERE i.sku = $1");
        span.Stop(); // triggers the processor's OnEnd in the provider pipeline

        span.GetTagItem("db.operation.name").ShouldBe("SELECT");
        span.GetTagItem("db.sql.table").ShouldBe("inventory");
    }
}
