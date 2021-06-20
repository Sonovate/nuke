using System;
using System.Collections.Generic;
using System.Reflection;
using Nuke.Common.CI.AzurePipelines.Configuration;

namespace Nuke.Common.CI.AzurePipelines
{
    public static class AzurePipelinesTargetDefinitionExtensions
    {
        public static readonly Dictionary<string, List<AzurePipelinesStep>> PreSteps = new();   
        public static readonly Dictionary<string, List<AzurePipelinesStep>> PostSteps = new();   
        
        public static ITargetDefinition AzurePipelineSteps(this ITargetDefinition definition, Func<AzurePipelinesStep[]> tasks)
        {
            var target = definition.GetType().GetProperty("Target", BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(definition) as PropertyInfo;
            
            var actions = definition.GetType().GetProperty("Actions", BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(definition) as List<Action>;

            if (actions == null)
            {
                PreSteps[target.Name] = new List<AzurePipelinesStep>(tasks());
            }
            else
            {
                PostSteps[target.Name] = new List<AzurePipelinesStep>(tasks());
            }
            
            return definition;
        }
        
        public static ITargetDefinition AzurePipelineSteps(this ITargetDefinition definition, params AzurePipelinesStep[] tasks)
        {
            return definition.AzurePipelineSteps(() => tasks);
        }
    }
}
