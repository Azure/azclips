// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Shared orchestrator base for deployment commands (create, validate and what-if) at
    /// resource-group and subscription scope. Exposes the common user-facing input surface
    /// (template-source, parameters-source, mode, inline parameters, validation-level), delegating
    /// its validation and the template/parameters/bicep building to the scope-neutral
    /// <see cref="DeploymentRequestBuilder"/>. Scope inputs (resource group, location),
    /// command-specific options (rollback, result-format) and the request dispatch live in the
    /// derived orchestrators.
    /// </summary>
    public abstract class DeploymentOrchestratorBase<TRequest, TResponse> : ResourcesProcessorBase<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        private readonly DeploymentRequestBuilder requestBuilder = new();

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get => requestBuilder.Name; set => requestBuilder.Name = value; }

        public string? TemplateUri { get => requestBuilder.TemplateUri; set => requestBuilder.TemplateUri = value; }

        public string? TemplateFile { get => requestBuilder.TemplateFile; set => requestBuilder.TemplateFile = value; }

        public string? TemplateSpecId { get => requestBuilder.TemplateSpecId; set => requestBuilder.TemplateSpecId = value; }

        /// <summary>
        /// A hash table holding the ARM template itself, the fourth template source alongside
        /// TemplateFile/TemplateUri/TemplateSpecId. PowerShell-only: the CLI has no equivalent.
        /// </summary>
        public Hashtable? TemplateObject { get => requestBuilder.TemplateObject; set => requestBuilder.TemplateObject = value; }

        public string? QueryString { get => requestBuilder.QueryString; set => requestBuilder.QueryString = value; }

        public string? ParametersUri { get => requestBuilder.ParametersUri; set => requestBuilder.ParametersUri = value; }

        public string? ParametersFile { get => requestBuilder.ParametersFile; set => requestBuilder.ParametersFile = value; }

        public string? Mode { get => requestBuilder.Mode; set => requestBuilder.Mode = value; }

        public string[]? Parameter { get => requestBuilder.Parameter; set => requestBuilder.Parameter = value; }

        public string? ValidationLevel { get => requestBuilder.ValidationLevel; set => requestBuilder.ValidationLevel = value; }

        // Subscription deployments skip the resource-group requirement.
        protected void ValidateCommonInputs(bool requireResourceGroup = true)
        {
            if (requireResourceGroup && string.IsNullOrWhiteSpace(ResourceGroupName))
            {
                throw AzclipsErrorException.InvalidInput("ResourceGroupName must be provided.");
            }
            requestBuilder.ValidateCommonInputs();
        }

        protected static void ValidateResultFormat(string? resultFormat)
        {
            if (!string.IsNullOrWhiteSpace(resultFormat) && NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.NormalizeResultFormatOrNull(resultFormat) is null)
            {
                throw AzclipsErrorException.InvalidInput("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.");
            }
        }

        // Builds a clear, flattened message from the (possibly nested) ARM error so a template error
        // is readable instead of a wall of raw JSON.
        protected internal static string BuildErrorMessage(string header, ErrorResponse? error)
        {
            StringBuilder builder = new StringBuilder(header);
            AppendError(builder, error, indent: 1);
            return builder.ToString();
        }

        private static void AppendError(StringBuilder builder, ErrorResponse? error, int indent)
        {
            if (error is null)
            {
                return;
            }

            string pad = new string(' ', indent * 2);
            string code = string.IsNullOrWhiteSpace(error.Code) ? "(no code)" : error.Code!;
            string message = string.IsNullOrWhiteSpace(error.Message) ? "(no message)" : error.Message!;
            builder.Append(Environment.NewLine).Append(pad).Append(code).Append(": ").Append(message);
            if (!string.IsNullOrWhiteSpace(error.Target))
            {
                builder.Append(" (target: ").Append(error.Target).Append(')');
            }

            if (error.Details is { Count: > 0 })
            {
                foreach (ErrorResponse detail in error.Details)
                {
                    AppendError(builder, detail, indent + 1);
                }
            }
        }

        protected static DeploymentMode ParseMode(string? mode) => DeploymentRequestBuilder.ParseMode(mode);

        protected object? BuildTemplate(out TemplateLink? templateLink) => requestBuilder.BuildTemplate(out templateLink);

        protected object? BuildParameters(out ParametersLink? parametersLink) => requestBuilder.BuildParameters(out parametersLink);

        protected static string? NormalizeValidationLevel(string? validationLevel) => DeploymentRequestBuilder.NormalizeValidationLevel(validationLevel);
    }
}
