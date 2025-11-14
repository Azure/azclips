namespace Microsoft.Azure.Cirrus.Resources.Cmdlets
{
    public class NewResouorceGroupCommand: SharedContracts.BaseProcessor
    {
        public string Name { get; set; }

        public string Location { get; set; }

        public Hashtable Tag { get; set; }

        public override async global::System.Threading.Tasks.Task Process()
        {
            
        }
    }
}
