// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience helper used by orchestrators that let a caller identify a group by object ID
    /// or by display name / prefix. The atomic <see cref="Processors.GetGroupProcessor"/>,
    /// <see cref="Processors.CheckGroupMemberProcessor"/>, and
    /// <see cref="Processors.ListGroupMemberProcessor"/> take only the resolved object ID.
    /// </summary>
    internal static class GroupResolver
    {
        /// <summary>
        /// Runs the caller-prepared <see cref="ListGroupProcessor"/> child, deserialises the
        /// response envelope, and returns the single matching group's object ID. Throws a
        /// deterministic <see cref="InvalidOperationException"/> when zero or more than one
        /// group matches.
        /// </summary>
        public static async Task<string> PickSingleGroupIdAsync(
            ListGroupProcessor listProcessor,
            JsonSerializerOptions jsonOptions,
            string identifier)
        {
            using HttpResponseMessage response = await listProcessor.SendAsync();
            string rawBody = await response.Content.ReadAsStringAsync();
            GroupList? envelope = JsonSerializer.Deserialize<GroupList>(rawBody, jsonOptions);
            Group[] matches = envelope?.Value ?? [];

            if (matches.Length == 0)
            {
                throw AzclipsErrorException.InvalidInput($"Group '{identifier}' is not found in Graph.");
            }
            if (matches.Length > 1)
            {
                throw AzclipsErrorException.InvalidInput($"More than one group matched '{identifier}'. Use the object id instead.");
            }

            string? id = matches[0].Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException($"Group resolved for '{identifier}' has no id.");
            }
            return id;
        }
    }
}
