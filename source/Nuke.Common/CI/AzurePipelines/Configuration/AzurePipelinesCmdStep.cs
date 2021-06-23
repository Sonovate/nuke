// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System.Collections.Generic;
using JetBrains.Annotations;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.ValueInjection;

namespace Nuke.Common.CI.AzurePipelines.Configuration
{
    [PublicAPI]
    public class AzurePipelinesCmdStep : AzurePipelinesStep
    {
        public string[] InvokedTargets { get; set; }
        public string BuildCmdPath { get; set; }
        public int? PartitionSize { get; set; }
        public Dictionary<string, string> Imports { get; set; }
        public string WorkingDirectory { get; set; }
        public string GlobalNukeToolExe { get; set; }
        public string GlobalNukeToolPackage { get; set; }

        public override void Write(CustomFileWriter writer)
        {
            using (writer.WriteBlock("- task: CmdLine@2"))
            {
                var invokedTargets = InvokedTargets.JoinSpace();
                
                writer.WriteLine($"displayName: Nuke{invokedTargets}");
                writer.WriteLine($"name: CmdLine");
                
                var arguments = $"{invokedTargets} --skip";
                if (PartitionSize != null)
                    arguments += $" --partition $(System.JobPositionInPhase)/{PartitionSize}";

                var solution = EnvironmentInfo.GetParameter<string>("Solution");
                if (solution != null)
                    arguments += $" --solution {solution}";
                
                using (writer.WriteBlock("inputs:"))
                {
                    if (GlobalNukeToolExe != null || GlobalNukeToolPackage != null)
                    {
                        using (writer.WriteBlock("script: |"))
                        {
                            if (GlobalNukeToolPackage != null)
                            {
                                writer.WriteLine($"dotnet tool install -g {GlobalNukeToolPackage}");
                            }

                            writer.WriteLine($"{GlobalNukeToolExe ?? GlobalNukeToolPackage} {arguments}");
                        }
                        if (!string.IsNullOrEmpty(WorkingDirectory))
                        {
                            writer.WriteLine($"workingDirectory: {WorkingDirectory}");
                        }
                    }
                    else
                    {
                        writer.WriteLine($"script: 'chmod +x ./{BuildCmdPath}; ./{BuildCmdPath} {arguments}'");
                        if (!string.IsNullOrEmpty(WorkingDirectory))
                        {
                            writer.WriteLine($"workingDirectory: {WorkingDirectory}");
                        }   
                    }
                }

                if (Imports.Count > 0)
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
