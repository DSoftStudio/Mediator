// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Extracts the <em>redaction-safe</em> shape of a SQL statement — its leading operation
/// (<c>SELECT</c>/<c>INSERT</c>/…) and target table — without ever surfacing parameters,
/// predicates or row values.
/// </summary>
/// <remarks>
/// <para>
/// Used by <see cref="DatabaseSpanEnrichmentProcessor"/> to derive <c>db.operation.name</c> /
/// <c>db.sql.table</c> from <c>db.statement</c> when the underlying database instrumentation
/// (e.g. an older Npgsql) only emits the raw statement. This runs in-process, where the
/// application owns its own SQL; only the verb and a single bare identifier are read out — never
/// the statement text itself.
/// </para>
/// <para>
/// The scanner tokenises at parenthesis depth zero, skipping line/block comments, single-quoted
/// and dollar-quoted string literals, and quoted identifiers, so a keyword appearing inside a
/// sub-select, a string or a comment never masquerades as the top-level operation.
/// </para>
/// </remarks>
internal static class SqlStatementParser
{
    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CALL", "EXEC", "EXECUTE",
    };

    /// <summary>The canonical upper-case operation verb, or <c>null</c> if none can be identified.</summary>
    public static string? Operation(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return null;

        foreach (var token in DepthZeroTokens(sql))
        {
            if (Verbs.Contains(token))
                return Canonical(token);
        }
        return null;
    }

    /// <summary>
    /// The single target table/identifier the operation reads or writes, or <c>null</c> when it
    /// cannot be determined unambiguously (multi-table joins return the first source table only).
    /// </summary>
    public static string? Table(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return null;

        var tokens = DepthZeroTokens(sql);

        int opIndex = -1;
        string? op = null;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (Verbs.Contains(tokens[i])) { opIndex = i; op = Canonical(tokens[i]); break; }
        }
        if (op is null) return null;

        // UPDATE: the table is the identifier immediately after the verb.
        if (op == "UPDATE")
            return QualifiedNameAfter(tokens, opIndex);

        var anchor = op switch
        {
            "SELECT" or "DELETE" => "FROM",
            "INSERT" or "MERGE"  => "INTO",
            _ => null, // CALL/EXECUTE have no table.
        };
        if (anchor is null) return null;

        for (int i = opIndex + 1; i < tokens.Count; i++)
        {
            if (string.Equals(tokens[i], anchor, StringComparison.OrdinalIgnoreCase))
                return QualifiedNameAfter(tokens, i);
        }
        return null;
    }

    /// <summary>
    /// The invoked stored-procedure / function name for a <c>CALL</c> / <c>EXEC(UTE)</c> statement,
    /// or <c>null</c> when the statement is not a procedure call (or the name is dynamic).
    /// </summary>
    public static string? Procedure(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return null;

        var tokens = DepthZeroTokens(sql);
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Equals("CALL", StringComparison.OrdinalIgnoreCase)
             || tokens[i].Equals("EXEC", StringComparison.OrdinalIgnoreCase)
             || tokens[i].Equals("EXECUTE", StringComparison.OrdinalIgnoreCase))
            {
                return QualifiedNameAfter(tokens, i);
            }
        }
        return null;
    }

    private static string Canonical(string verb)
        => verb.Equals("EXEC", StringComparison.OrdinalIgnoreCase)
            ? "EXECUTE"
            : verb.ToUpperInvariant();

    /// <summary>
    /// Reads the (possibly schema-qualified) identifier that follows <paramref name="keywordIndex"/>
    /// and returns its final segment — e.g. <c>public . orders</c> → <c>orders</c>.
    /// </summary>
    private static string? QualifiedNameAfter(List<string> tokens, int keywordIndex)
    {
        int i = keywordIndex + 1;
        if (i >= tokens.Count || tokens[i] == ".") return null;

        var last = tokens[i];
        i++;
        while (i + 1 < tokens.Count && tokens[i] == ".")
        {
            last = tokens[i + 1];
            i += 2;
        }
        return last == "." ? null : last;
    }

    /// <summary>
    /// Yields word tokens and bare <c>.</c> separators that sit at parenthesis depth zero,
    /// skipping comments, string literals and tracking quoted identifiers as single words.
    /// </summary>
    private static List<string> DepthZeroTokens(string sql)
    {
        var tokens = new List<string>();
        int depth = 0;
        int n = sql.Length;

        for (int i = 0; i < n;)
        {
            char c = sql[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Line comment: -- … <eol>
            if (c == '-' && i + 1 < n && sql[i + 1] == '-')
            {
                i += 2;
                while (i < n && sql[i] != '\n') i++;
                continue;
            }

            // Block comment: /* … */
            if (c == '/' && i + 1 < n && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i += 2;
                continue;
            }

            // Single-quoted string literal (with '' escape).
            if (c == '\'')
            {
                i++;
                while (i < n)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < n && sql[i + 1] == '\'') { i += 2; continue; }
                        i++; break;
                    }
                    i++;
                }
                continue;
            }

            // Dollar-quoted string literal (PostgreSQL): $tag$ … $tag$
            if (c == '$')
            {
                int tagEnd = i + 1;
                while (tagEnd < n && (char.IsLetterOrDigit(sql[tagEnd]) || sql[tagEnd] == '_')) tagEnd++;
                if (tagEnd < n && sql[tagEnd] == '$')
                {
                    var tag = sql.Substring(i, tagEnd - i + 1);
                    int close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                    i = close < 0 ? n : close + tag.Length;
                    continue;
                }
                i++; // Lone '$' — treat as punctuation.
                continue;
            }

            // Quoted identifier: "…" (with "" escape) — a single identifier token.
            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < n)
                {
                    if (sql[i] == '"')
                    {
                        if (i + 1 < n && sql[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                        i++; break;
                    }
                    sb.Append(sql[i]); i++;
                }
                if (depth == 0 && sb.Length > 0) tokens.Add(sb.ToString());
                continue;
            }

            if (c == '(') { depth++; i++; continue; }
            if (c == ')') { if (depth > 0) depth--; i++; continue; }

            if (c == '.')
            {
                if (depth == 0) tokens.Add(".");
                i++;
                continue;
            }

            // Bare word (keyword / identifier).
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                i++;
                while (i < n && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_' || sql[i] == '$')) i++;
                if (depth == 0) tokens.Add(sql.Substring(start, i - start));
                continue;
            }

            i++; // Any other punctuation (commas, operators, parameters …) is irrelevant.
        }

        return tokens;
    }
}
