// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Linq;
using Nuke.Common.Utilities;

namespace Nuke.Common.CI.AzurePipelines
{
    public class AzurePipelinesVariable: ConfigurationEntity
    {
        public override void Write(CustomFileWriter writer)
        {
            writer.WriteLine($"- {Name}: {DefaultValue}");
        }

        public string Name { get; set; }
        public string DefaultValue { get; set; }
    }
}
