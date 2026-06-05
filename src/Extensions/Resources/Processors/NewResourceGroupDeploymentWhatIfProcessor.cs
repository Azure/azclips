using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;
using Microsoft.Azure.Cirrus.Extensions.Resources;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    /// <summary>
    /// Runs an ARM what-if operation at the resource group scope
    /// (POST .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}/whatIf)
    /// and renders the predicted change list. This is the Cirrus equivalent of
    /// <c>az deployment group what-if</c> / <c>New-AzResourceGroupDeploymentWhatIf</c>.
    /// What-if is read-only: it never creates or modifies resources.
    /// </summary>
    public class NewResourceGroupDeploymentWhatIfProcessor : DeploymentRequestProcessorBase<WhatIfOperationResult>
    {
        [ArgumentInfo(
        Required = false,
        Description = @"The validation level of the what-if. Template, Provider, or ProviderNoRbac.",
        SerializedName = @"ValidationLevel")]
        public string? ValidationLevel { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The result format of the what-if. ResourceIdOnly or FullResourcePayloads (default).",
        SerializedName = @"ResultFormat")]
        public string? ResultFormat { get; set; }

        // The what-if operation is long-running: the initial POST returns 202 + Location + Retry-After,
        // and polling the Location header returns 200 with the full WhatIfOperationResult body.
        // The Azure-AsyncOperation header (if present) only carries a bare {status} WITHOUT the changes
        // payload, so it is intentionally NOT used here. The original POST /whatIf URI is POST-only,
        // so the final GET is skipped (FetchFinalState=false) and the last Location poll (the 200 with
        // the result body) is returned and deserialized.
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");
        protected override bool FetchFinalState => false;

        // What-if always waits for the LRO to complete (there is no -NoWait).
        internal override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return SendAsyncLRO();
        }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            ValidateInputs();

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            DeploymentWhatIf body = new DeploymentWhatIf
            {
                Properties = new DeploymentWhatIfProperties
                {
                    Mode = ParseMode(Mode),
                    Template = BuildTemplate(out TemplateLink? templateLink),
                    TemplateLink = templateLink,
                    Parameters = BuildParameters(out ParametersLink? parametersLink),
                    ParametersLink = parametersLink,
                    ValidationLevel = NormalizeValidationLevel(ValidationLevel),
                    WhatIfSettings = new DeploymentWhatIfSettings { ResultFormat = NormalizeResultFormat(ResultFormat) },
                }
            };

            string uri = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/whatIf?api-version={ApiVersion}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override void ValidateInputs()
        {
            base.ValidateInputs();

            if (!string.IsNullOrWhiteSpace(ValidationLevel) && NormalizeValidationLevel(ValidationLevel) is null)
            {
                throw new ArgumentException("ValidationLevel must be one of 'Template', 'Provider', or 'ProviderNoRbac'.");
            }

            if (!string.IsNullOrWhiteSpace(ResultFormat) && NormalizeResultFormatOrNull(ResultFormat) is null)
            {
                throw new ArgumentException("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.");
            }
        }

        // The what-if API returns HTTP 200 with status Succeeded even when the template has an error;
        // the error lives in result.error. Override ProcessAsync so a template error is surfaced as a
        // thrown exception instead of being silently presented as a successful no-change result.
        public override async Task ProcessAsync()
        {
            EnsureRuntimeContext();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                using HttpResponseMessage response = await SendAsync();

                if (response.Content is null)
                {
                    await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                    return;
                }

                string rawBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                    await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                    return;
                }

                WhatIfOperationResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<WhatIfOperationResult>(rawBody, JsonOptions);
                }
                catch (JsonException ex)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {nameof(WhatIfOperationResult)}. Raw body: {rawBody}", ex);
                }

                if (result is null)
                {
                    return;
                }

                bool failed = string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase);
                if (failed || result.Error is not null)
                {
                    throw new InvalidOperationException(BuildErrorMessage(result.Error));
                }

                await WriteOutput(result);
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        // Builds a clear, flattened message from the (possibly nested) ARM error so a template error
        // is not silently presented as a successful no-change result.
        private static string BuildErrorMessage(ErrorResponse? error)
        {
            StringBuilder builder = new StringBuilder("What-if operation failed.");
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

        // Returns the canonical-cased result format, defaulting to FullResourcePayloads when not
        // supplied. ValidateInputs rejects unrecognized values.
        private static string NormalizeResultFormat(string? resultFormat) =>
            NormalizeResultFormatOrNull(resultFormat) ?? "FullResourcePayloads";

        private static string? NormalizeResultFormatOrNull(string? resultFormat) =>
            string.IsNullOrWhiteSpace(resultFormat)
                ? "FullResourcePayloads"
                : resultFormat.Trim().ToLowerInvariant() switch
                {
                    "resourceidonly" => "ResourceIdOnly",
                    "fullresourcepayloads" => "FullResourcePayloads",
                    _ => null,
                };

        // Returns the canonical-cased validation level when valid, or null when the input is
        // empty (not supplied) or unrecognized. ValidateInputs rejects unrecognized values.
        private static string? NormalizeValidationLevel(string? validationLevel) =>
            string.IsNullOrWhiteSpace(validationLevel)
                ? null
                : validationLevel.Trim().ToLowerInvariant() switch
                {
                    "template" => "Template",
                    "provider" => "Provider",
                    "providernorbac" => "ProviderNoRbac",
                    _ => null,
                };
    }
}
