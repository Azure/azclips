// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.MsGraph;
using Core.Abstractions;
using System.Net;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Shared helpers used across orchestrators for OData composition and Select-clause
    /// default merging. Keeps that convenience logic out of the atomic (spec-derived)
    /// processors.
    /// </summary>
    internal static class SelectClauseHelpers
    {
        /// <summary>
        /// Escapes a value for embedding in an OData string literal (single quotes are doubled).
        /// </summary>
        public static string EscapeODataString(string value) => value.Replace("'", "''");

        /// <summary>
        /// Combines a raw <c>Filter</c> string with a set of convenience-shortcut clauses using
        /// OData <c>and</c>. Returns null if every input is empty.
        /// </summary>
        public static string? CombineFilterClauses(string? filter, params string?[] shortcuts)
        {
            var clauses = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                clauses.Add(filter);
            }
            foreach (var clause in shortcuts)
            {
                if (!string.IsNullOrWhiteSpace(clause))
                {
                    clauses.Add(clause);
                }
            }
            return clauses.Count == 0 ? null : string.Join(" and ", clauses);
        }

        public static string[]? MergeUserSelect(string[]? select, bool appendSelected) =>
            SplitClause(GraphUserDefaults.BuildSelectClause(select, appendSelected));

        public static string[]? MergeGroupSelect(string[]? select, bool appendSelected) =>
            SplitClause(GraphGroupDefaults.BuildSelectClause(select, appendSelected));

        /// <summary>
        /// Re-throws a user-facing InvalidInput error when Graph rejects a query because the
        /// <c>ConsistencyLevel:eventual</c> header is missing. Leaves unrelated errors untouched.
        /// </summary>
        public static void RethrowMissingConsistencyLevelAsInvalidInput(HttpRequestException ex)
        {
            if (ex.StatusCode != HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(ex.Message))
            {
                return;
            }

            string message = ex.Message;
            bool isUnsupportedQuery = message.IndexOf("Request_UnsupportedQuery", StringComparison.OrdinalIgnoreCase) >= 0;
            bool missingConsistencyLevel = message.IndexOf("ConsistencyLevel:eventual", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isUnsupportedQuery && !missingConsistencyLevel)
            {

                throw AzclipsErrorException.InvalidInput(
                    "Microsoft Graph rejected this query because it requires ConsistencyLevel 'eventual'. Rerun with --consistency-level eventual (CLI) or -ConsistencyLevel eventual (PowerShell).",
                    innerException: ex);
            }
        }

        private static string[]? SplitClause(string? clause)
        {
            if (clause is null)
            {
                return null;
            }
            return clause.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
