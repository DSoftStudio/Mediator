// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Enriches database client spans (those carrying <c>db.system</c>) with a redaction-safe
/// <c>db.operation.name</c> and <c>db.sql.table</c> derived from the SQL statement, so that
/// downstream tooling (e.g. the Pipeline Explorer) can attribute time to the <em>specific</em>
/// operation — distinguishing a <c>SELECT</c> from an <c>INSERT</c> on the same connection instead
/// of collapsing every query into a single <c>"{system} → {host}"</c> dependency row.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <c>AddMediatorInstrumentation()</c> on the
/// <see cref="global::OpenTelemetry.Trace.TracerProviderBuilder"/>; no configuration is required.
/// It runs in-process, where the application owns its own SQL, so reading <c>db.statement</c> here
/// never crosses a trust boundary — and only the verb and a single bare table identifier are
/// copied onto the span. The raw statement, parameters and row values are never propagated, which
/// keeps the import-side contract intact (the trace consumer never has to read <c>db.statement</c>).
/// </para>
/// <para>
/// The enrichment is strictly additive: an attribute already supplied by native instrumentation
/// (a newer Npgsql / EF Core that emits <c>db.operation.name</c>) is never overwritten.
/// </para>
/// </remarks>
internal sealed class DatabaseSpanEnrichmentProcessor : global::OpenTelemetry.BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        // Cheap gate: only database client spans carry db.system / db.system.name.
        if (activity.GetTagItem("db.system") is null && activity.GetTagItem("db.system.name") is null)
            return;

        bool hasOperation = activity.GetTagItem("db.operation.name") is not null
                         || activity.GetTagItem("db.operation") is not null;
        bool hasTable = activity.GetTagItem("db.sql.table") is not null
                     || activity.GetTagItem("db.collection.name") is not null;
        bool hasProcedure = activity.GetTagItem("db.stored_procedure.name") is not null;

        if (hasOperation && hasTable && hasProcedure)
            return; // Native instrumentation already described the operation — nothing to add.

        var statement = (activity.GetTagItem("db.query.text") as string)
                     ?? (activity.GetTagItem("db.statement") as string);
        if (string.IsNullOrWhiteSpace(statement))
            return;

        if (!hasOperation && SqlStatementParser.Operation(statement) is { } operation)
            activity.SetTag("db.operation.name", operation);

        // DML target table (SELECT/INSERT/UPDATE/DELETE) — distinguishes queries on different tables.
        if (!hasTable && SqlStatementParser.Table(statement) is { } table)
            activity.SetTag("db.sql.table", table);

        // Stored-procedure / function name (CALL/EXEC) — distinguishes one procedure from another.
        if (!hasProcedure && SqlStatementParser.Procedure(statement) is { } procedure)
            activity.SetTag("db.stored_procedure.name", procedure);
    }
}
