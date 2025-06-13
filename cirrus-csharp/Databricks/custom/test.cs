using System;
using System.Management.Automation;
using System.Collections.ObjectModel;

namespace MyPowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Get, "DynamicParameterExample")]
    public class GetDynamicParameterExampleCommand : Cmdlet, IDynamicParameters
    {
        [Parameter(Position = 0, Mandatory = true)]
        public string Path { get; set; }

        private RuntimeDefinedParameterDictionary dynamicParams;

        public object GetDynamicParameters()
        {
            dynamicParams = new RuntimeDefinedParameterDictionary();

            if (System.IO.Directory.Exists(Path))
            {
                var attributes = new Collection<Attribute>
                {
                    new ParameterAttribute { Mandatory = false }
                };

                var fileTypeParam = new RuntimeDefinedParameter("FileType", typeof(string), attributes);
                dynamicParams.Add("FileType", fileTypeParam);
            }
            else
            { 
                var attributes = new Collection<Attribute>
                {
                    new ParameterAttribute { Mandatory = false }
                };

                var fileTypeParam = new RuntimeDefinedParameter("FileTypeB", typeof(string), attributes);
                dynamicParams.Add("FileTypeB", fileTypeParam);
                
            }

            return dynamicParams;
        }

        protected override void ProcessRecord()
        {
            if (dynamicParams.ContainsKey("FileType"))
            {
                WriteObject("FileType parameter is available.");
            }
            else
            {
                WriteObject("FileType parameter is not available.");
            }

            // Additional processing logic here
        }
    }
}
