// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common.Utilities;

namespace Nuke.Common.CI.AzurePipelines
{
    public class AzurePipelinesParameter : ConfigurationEntity
    {
        public override void Write(CustomFileWriter writer)
        {
            writer.WriteLine($"- name: {Name}");
            writer.WriteLine($"  type: {Type}");
                        
            if(!string.IsNullOrEmpty(DefaultValue))
                writer.WriteLine($"  default: {Type}");
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Options { get; set; }
        public string Type { get; set; }
        public string DefaultValue { get; set; }
    }
}
