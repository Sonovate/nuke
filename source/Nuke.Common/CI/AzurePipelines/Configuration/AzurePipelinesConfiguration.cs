// Copyright 2020 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;

namespace Nuke.Common.CI.AzurePipelines.Configuration
{
    [PublicAPI]
    public class AzurePipelinesConfiguration : ConfigurationEntity
    {
        public string[] VariableGroups { get; set; }

        [CanBeNull]
        public AzurePipelinesVcsPushTrigger VcsPushTrigger { get; set; }

        public AzurePipelinesStage[] Stages { get; set; }
        public AzurePipelinesParameter[] Parameters { get; set; }
        public AzurePipelinesVariable[] Variables { get; set; }
        
        public AzurePipelineResource[] Resources { get; set; }

        public override void Write(CustomFileWriter writer)
        {
            if (Parameters.Length > 0)
            {
                using (writer.WriteBlock("parameters:"))
                {
                    Parameters.ForEach(x =>
                    {
                        x.Write(writer);
                    });
                    writer.WriteLine();
                }
            }

            if (Resources.Length > 0)
            {
                using (writer.WriteBlock("resources:"))
                using (writer.WriteBlock("repositories:"))
                {
                    Resources.ForEach(x =>
                    {
                        x.Write(writer);
                    });
                    writer.WriteLine();
                }
            }
            
            if (VariableGroups.Length > 0)
            {
                using (writer.WriteBlock("variables:"))
                {
                    VariableGroups.ForEach(x => writer.WriteLine($"- group: {x}"));
                    Variables.ForEach(x => x.Write(writer));
                    writer.WriteLine();
                }
            }

            if (VcsPushTrigger != null)
            {
                using (writer.WriteBlock("trigger:"))
                {
                    VcsPushTrigger.Write(writer);
                    writer.WriteLine();
                }
            }

            using (writer.WriteBlock("stages:"))
            {
                Stages.ForEach(x => x.Write(writer));
            }
        }
    }

    public class AzurePipelineResource : ConfigurationEntity
    {
        public string Repository { get; set; }
        public string Name { get; set; }
        
        public override void Write(CustomFileWriter writer)
        {
            
            using (writer.WriteBlock($"- repository: {Repository}"))
            {
                writer.WriteLine("type: git");
                writer.WriteLine($"name: {Name}");
            }
        }
    }
}
