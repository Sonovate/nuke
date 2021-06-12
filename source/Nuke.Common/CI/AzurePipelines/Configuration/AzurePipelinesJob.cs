// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;

namespace Nuke.Common.CI.AzurePipelines.Configuration
{
    [PublicAPI]
    public class AzurePipelinesJob : ConfigurationEntity
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public AzurePipelinesImage? Image { get; set; }
        public AzurePipelinesJob[] Dependencies { get; set; }
        public int Parallel { get; set; }
        public AzurePipelinesStep[] Steps { get; set; }
        public AzurePipelinesVariable[] Variables { get; set; }

        public override void Write(CustomFileWriter writer)
        {
            using (writer.WriteBlock($"- job: {Name}"))
            {
                writer.WriteLine($"displayName: {DisplayName.SingleQuote()}");
                writer.WriteLine($"dependsOn: [ {Dependencies.Select(x => x.Name).JoinComma()} ]");

                if (Image != null)
                {
                    using (writer.WriteBlock("pool:"))
                    {
                        writer.WriteLine($"vmImage: {Image.Value.GetValue().SingleQuote().SingleQuote()}");
                    }
                }

                if (Parallel > 1)
                {
                    using (writer.WriteBlock("strategy:"))
                    {
                        writer.WriteLine($"parallel: {Parallel}");
                    }
                }

                var lastJob = Dependencies.LastOrDefault()?.Name;

                if(lastJob != null)
                {
                    using (writer.WriteBlock("variables:"))
                    {
                        Variables.ForEach(x =>  writer.WriteLine($"{x.Name}: $[ dependencies.{lastJob}.outputs['CmdLine.{x.Name}'] ]"));
                    }
                }
                
                using (writer.WriteBlock("steps:"))
                {
                    Steps.ForEach(x => x.Write(writer));
                }
            }
        }
    }
}
