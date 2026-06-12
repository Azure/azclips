namespace Microsoft.Azure.Cirrus.Extensions.Web.Processors;

public class DeleteWebappProcessor_Orchestrator : WebProcessorBase<object>
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string? Slot { get; set; } = null!;
    public bool? DeleteMetrics { get; set; } = null!;
    public bool? DeleteEmptyServerFarm { get; set; } = null!;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        WebProcessorBase processor;
        if (Slot != null)
        {
            processor =
                CreateExtendedProcessor<DeleteSlotWebappProcessor>(p =>
                {
                    p.Name = Name;
                    p.ResourceGroupName = ResourceGroupName;
                    p.Slot = Slot;
                    p.DeleteMetrics = DeleteMetrics;
                    p.DeleteEmptyServerFarm = DeleteEmptyServerFarm;
                });
        }
        else
        {
            processor =
                CreateExtendedProcessor<DeleteWebappProcessor>(p =>
                {
                    p.Name = Name;
                    p.ResourceGroupName = ResourceGroupName;
                    p.DeleteMetrics = DeleteMetrics;
                    p.DeleteEmptyServerFarm = DeleteEmptyServerFarm;
                });
        }
        return await processor.SendAsync();
    }
}
