// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Shared orchestrator base for resource-group-scoped deployment commands (create, validate,
    /// and what-if). Owns ResourceGroupName and exposes the common user-facing input surface,
    /// delegating its validation and the template/parameters/bicep building to the scope-neutral
    /// <see cref="DeploymentRequestBuilder"/>. Command-specific options (rollback for create,
    /// result-format for what-if) and the request dispatch live in the derived orchestrators.
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

        // Checks the resource group first, then the shared deployment inputs. Derived orchestrators
        // call this first, then add their own command-specific rules.
        protected void ValidateCommonInputs()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroupName))
            {
                throw AzclipsErrorException.InvalidInput("ResourceGroupName must be provided.");
            }
            requestBuilder.ValidateCommonInputs();
        }

        protected static DeploymentMode ParseMode(string? mode) => DeploymentRequestBuilder.ParseMode(mode);

        protected object? BuildTemplate(out TemplateLink? templateLink) => requestBuilder.BuildTemplate(out templateLink);

        protected object? BuildParameters(out ParametersLink? parametersLink) => requestBuilder.BuildParameters(out parametersLink);

        protected static string? NormalizeValidationLevel(string? validationLevel) => DeploymentRequestBuilder.NormalizeValidationLevel(validationLevel);
    }
}
