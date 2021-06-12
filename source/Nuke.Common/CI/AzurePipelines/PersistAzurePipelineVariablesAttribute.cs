// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System.Linq;
using Nuke.Common.Execution;
using Nuke.Common.Utilities;
using Nuke.Common.ValueInjection;

namespace Nuke.Common.CI.AzurePipelines
{
    public class PersistAzurePipelineVariablesAttribute : BuildExtensionAttributeBase, IOnTargetSucceeded, IOnTargetFailed
    {
        public void OnTargetSucceeded(NukeBuild build, ExecutableTarget target)
        {
            SetOutputVariables(build);
        }

        public void OnTargetFailed(NukeBuild build, ExecutableTarget target)
        {
            SetOutputVariables(build);
        }

        private static void SetOutputVariables(NukeBuild build)
        {
            var variables = ValueInjectionUtility.GetParameterMembers(build.GetType(), includeUnlisted: false)
                .Where(x => x.HasCustomAttribute<VariableAttribute>());

            foreach (var variable in variables)
            {
                AzurePipelines.Instance?.SetVariable(variable.Name, variable.GetValue(build) as string, isOutput: true);
            }
        }
    }
}
