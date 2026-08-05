namespace Microsoft.Azure.Azclips.Extensions.Web.Processors;

public class ListWebappProcessor_Orchestrator : WebProcessorBase<object>
{
    public string ResourceGroupName { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        WebProcessorBase processor;
        if (ResourceGroupName != null)
        {
            processor =
                CreateExtendedProcessor<ListWebappByResourceGroupProcessor>(p =>
                {
                    p.ResourceGroupName = ResourceGroupName;
                });
        }
        else
        {
            processor =
                CreateExtendedProcessor<ListWebappProcessor>(p =>
                {
                });
        }
        return await processor.SendAsync();
    }
}
