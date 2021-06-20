using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Nuke.Common.CI.AzurePipelines.Configuration
{
    [PublicAPI]
    public class AzurePipelinesTask : AzurePipelinesStep
    {
        readonly dynamic Inputs;

        public AzurePipelinesTask(string taskName, dynamic inputs = null)
        {
            Inputs = inputs;
            TaskName = taskName;
            Imports = new Dictionary<string, string>();
        }

        public string[] InvokedTargets { get; set; }
        public string PartitionName { get; set; }
        public Dictionary<string, string> Imports { get; set; }

        public string TaskName { get; set; }
    
        public override void Write(CustomFileWriter writer)
        {
            using (writer.WriteBlock($"- task: {TaskName}"))
            {
                if (Inputs != null)
                {
                    using (writer.WriteBlock("inputs:"))
                    {
                        var serializer = new SerializerBuilder()
                            .WithNamingConvention(CamelCaseNamingConvention.Instance)
                            .Build();
                        var yaml = (string)serializer.Serialize(Inputs);
                        var lines = yaml.Split(new[]{ Environment.NewLine }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
            
                if (Imports.Any())
                {
                    using (writer.WriteBlock("env:"))
                    {
                        Imports.ForEach(x => writer.WriteLine($"{x.Key}: {x.Value}"));
                    }
                }
            }
        }
    }
}
