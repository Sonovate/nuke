// Copyright 2020 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Nuke.Common.CI.AzurePipelines.Configuration;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.ValueInjection;
using static Nuke.Common.IO.PathConstruction;

namespace Nuke.Common.CI.AzurePipelines
{
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class AzurePipelinesAttribute : ChainedConfigurationAttributeBase
    {
        private readonly string _suffix;
        private readonly AzurePipelinesImage[] _images;

        private bool? _triggerBatch;

        public AzurePipelinesAttribute(
            AzurePipelinesImage image,
            params AzurePipelinesImage[] images)
            : this(suffix: null, image, images)
        {
        }

        public AzurePipelinesAttribute(
            [CanBeNull] string suffix,
            AzurePipelinesImage image,
            params AzurePipelinesImage[] images)
        {
            _suffix = suffix;
            _images = new[] { image }.Concat(images).ToArray();
        }

        public override string IdPostfix => _suffix;

        public string GlobalNukeToolPackage { get; set; }
        public string GlobalNukeToolExe { get; set; }

        public override Type HostType => typeof(AzurePipelines);
        public override string ConfigurationFile => ConfigurationDirectory / ConfigurationFileName;
        public override IEnumerable<string> GeneratedFiles => new[] { ConfigurationFile };
        protected virtual AbsolutePath ConfigurationDirectory => NukeBuild.RootDirectory;
        private string ConfigurationFileName => _suffix != null ? $"azure-pipelines.{_suffix}.yml" : "azure-pipelines.yml";

        public override IEnumerable<string> RelevantTargetNames => InvokedTargets;

        public string[] InvokedTargets { get; set; } = new string[0];

        public string[] PipelineParameters { get; set; } = new string[0];
        public string CmdWorkingDirectory { get; set; }

        public bool TriggerDisabled { get; set; }

        public bool TriggerBatch
        {
            set => _triggerBatch = value;
            get => throw new NotSupportedException();
        }

        public string[] TriggerBranchesInclude { get; set; } = new string[0];
        public string[] TriggerBranchesExclude { get; set; } = new string[0];
        public string[] TriggerTagsInclude { get; set; } = new string[0];
        public string[] TriggerTagsExclude { get; set; } = new string[0];
        public string[] TriggerPathsInclude { get; set; } = new string[0];
        public string[] TriggerPathsExclude { get; set; } = new string[0];

        public bool PullRequestsAutoCancel { get; set; }
        public string[] PullRequestsBranchesInclude { get; set; } = new string[0];
        public string[] PullRequestsBranchesExclude { get; set; } = new string[0];
        public string[] PullRequestsPathsInclude { get; set; } = new string[0];
        public string[] PullRequestsPathsExclude { get; set; } = new string[0];

        public string[] CacheKeyFiles { get; set; } = { "**/global.json", "**/*.csproj" };
        public string CachePath { get; set; } = "~/.nuget/packages";

        public string[] ImportVariableGroups { get; set; } = new string[0];
        public string[] ImportSecrets { get; set; } = new string[0];
        public string ImportSystemAccessTokenAs { get; set; }

        protected override string BuildCmdPath =>
            NukeBuild.RootDirectory.GlobFiles("build.sh", "*/build.sh")
                .Select(x => NukeBuild.RootDirectory.GetUnixRelativePathTo(x))
                .FirstOrDefault().NotNull("BuildCmdPath != null");
        
        public override CustomFileWriter CreateWriter(StreamWriter streamWriter)
        {
            return new CustomFileWriter(streamWriter, indentationFactor: 2, commentPrefix: "#");
        }

        public override ConfigurationEntity GetConfiguration(NukeBuild build, IReadOnlyCollection<ExecutableTarget> relevantTargets)
        {
            var variables = GetVariables(build, relevantTargets).ToArray();
            var parameters = GetParameters(build, relevantTargets).ToArray();
            var parameterVariables = parameters.Select(x => new AzurePipelinesVariable{Name = x.Name, DefaultValue = x.Name, IsParameterVariable = true});
            var azurePipelinesStages = _images.Select(x => GetStage(x, relevantTargets, parameters, variables)).ToArray();

            var azurePipelinesConfiguration = new AzurePipelinesConfiguration
            {
                VariableGroups = ImportVariableGroups,
                VcsPushTrigger = GetVcsPushTrigger(),
                Stages = azurePipelinesStages,
                Parameters = parameters,
                Variables = parameterVariables.Union(variables).ToArray(),
                Resources = GetResources(azurePipelinesStages)
            };

            return azurePipelinesConfiguration;
        }

        private static AzurePipelineResource[] GetResources(AzurePipelinesStage[] stages)
        {
            var azurePipelineResources = stages
                .SelectMany(x => x.Jobs.SelectMany(job => job.Steps)).Where(step => step is AzurePipelinesTemplate)
                .Cast<AzurePipelinesTemplate>()
                .Select(x => new AzurePipelineResource
                {
                    Name = x.TemplateName.Split(new[]{ "@" }, StringSplitOptions.None).Last(),
                    Repository = x.TemplateName.Split(new[] { "@" }, StringSplitOptions.None).Last()
                }).Distinct(resource => resource.Repository).ToArray();
            return azurePipelineResources;
        }

        protected virtual IEnumerable<AzurePipelinesVariable> GetVariables(NukeBuild build, IReadOnlyCollection<ExecutableTarget> relevantTargets)
        {
            return ValueInjectionUtility.GetParameterMembers(build.GetType(), includeUnlisted: false)
                .Where(x => x.HasCustomAttribute<VariableAttribute>())
                .Select(x => GetVariable(x, build, required: false));
        }
        
        protected virtual IEnumerable<AzurePipelinesParameter> GetParameters(NukeBuild build, IReadOnlyCollection<ExecutableTarget> relevantTargets)
        {
            return ValueInjectionUtility.GetParameterMembers(build.GetType(), includeUnlisted: false)
                .Except(relevantTargets.SelectMany(x => x.Requirements
                    .Where(y => y is not Expression<Func<bool>>)
                    .Select(y => y.GetMemberInfo())))
                .Where(x => !x.HasCustomAttribute<SecretAttribute>() || ImportSecrets.Contains(ParameterService.GetParameterMemberName(x)))
                .Where(x => x.DeclaringType != typeof(NukeBuild) || x.Name == nameof(NukeBuild.Verbosity))
                .Where(x => PipelineParameters.Contains(x.Name))
                .Select(x => GetParameter(x, build, required: false));
        }
        
        protected virtual AzurePipelinesVariable GetVariable(MemberInfo member, NukeBuild build, bool required)
        {  
            return new() 
                  {
                      Name = ParameterService.GetParameterMemberName(member),
                      DefaultValue = member.GetValue(build) as string
                  };
        }
        
        protected virtual AzurePipelinesParameter GetParameter(MemberInfo member, NukeBuild build, bool required)
        {
            var attribute = member.GetCustomAttribute<ParameterAttribute>();
            var valueSet = ParameterService.GetParameterValueSet(member, build);

            var numericTypes = new HashSet<Type>
            {
                typeof(decimal), typeof(short), typeof(long), typeof(int), typeof(float), typeof(double)
            };
            
            string type;
            var memberType = member.GetMemberType();
            if (memberType == typeof(string))
            {
                type = "string";
            }
            else if (numericTypes.Contains(memberType))
            {
                type = "number";
            }else if (memberType == typeof(bool))
            {
                type = "boolean";
            }
            else
            {
                type = "object";
            }
            
            return new AzurePipelinesParameter
           {
               Name = ParameterService.GetParameterMemberName(member),

               Description = attribute.Description,
               Options = valueSet?.ToDictionary(x => x.Item1, x => x.Item2),
               Type = type,
               DefaultValue = member.GetValue(build) as string,
               // Display = required ? TeamCityParameterDisplay.Prompt : TeamCityParameterDisplay.Normal,
               // AllowMultiple = member.GetMemberType().IsArray && valueSet is not null,
               // ValueSeparator = valueSeparator
           };
        }

        [CanBeNull]
        protected AzurePipelinesVcsPushTrigger GetVcsPushTrigger()
        {
            if (!TriggerDisabled &&
                _triggerBatch == null &&
                TriggerBranchesInclude.Length == 0 &&
                TriggerBranchesExclude.Length == 0 &&
                TriggerTagsInclude.Length == 0 &&
                TriggerTagsExclude.Length == 0 &&
                TriggerPathsInclude.Length == 0 &&
                TriggerPathsExclude.Length == 0)
                return null;

            return new AzurePipelinesVcsPushTrigger
                   {
                       Disabled = TriggerDisabled,
                       Batch = _triggerBatch,
                       BranchesInclude = TriggerBranchesInclude,
                       BranchesExclude = TriggerBranchesExclude,
                       TagsInclude = TriggerTagsInclude,
                       TagsExclude = TriggerTagsExclude,
                       PathsInclude = TriggerPathsInclude,
                       PathsExclude = TriggerPathsExclude,
                   };
        }

        protected virtual AzurePipelinesStage GetStage(
            AzurePipelinesImage image,
            IReadOnlyCollection<ExecutableTarget> relevantTargets,
            AzurePipelinesParameter[] parameters,
            AzurePipelinesVariable[] variables)
        {
            var lookupTable = new LookupTable<ExecutableTarget, AzurePipelinesJob>();
            var jobs = relevantTargets
                .Select(x => (ExecutableTarget: x, Job: GetJob(x, lookupTable, relevantTargets, parameters, variables)))
                .ForEachLazy(x => lookupTable.Add(x.ExecutableTarget, x.Job))
                .Select(x => x.Job).ToArray();

            return new AzurePipelinesStage
                   {
                       Name = image.GetValue().Replace("-", "_").Replace(".", "_"),
                       DisplayName = image.GetValue(),
                       Image = image,
                       Dependencies = new AzurePipelinesStage[0],
                       Jobs = jobs
                   };
        }

        protected virtual AzurePipelinesJob GetJob(
            ExecutableTarget executableTarget,
            LookupTable<ExecutableTarget, AzurePipelinesJob> jobs,
            IReadOnlyCollection<ExecutableTarget> relevantTargets,
            AzurePipelinesParameter[] parameters,
            AzurePipelinesVariable[] variables)
        {
            var totalPartitions = executableTarget.PartitionSize ?? 0;
            var dependencies = GetTargetDependencies(executableTarget).SelectMany(x => jobs[x]).ToArray();
            return new AzurePipelinesJob
                   {
                       Name = executableTarget.Name,
                       DisplayName = executableTarget.Name,
                       Dependencies = dependencies,
                       Parallel = totalPartitions,
                       Steps = GetSteps(executableTarget, relevantTargets, parameters).ToArray(),
                       Variables = variables
                   };
        }

        protected virtual IEnumerable<AzurePipelinesStep> GetSteps(
            ExecutableTarget executableTarget,
            IReadOnlyCollection<ExecutableTarget> relevantTargets,
            AzurePipelinesParameter[] azurePipelinesParameters)
        {
            if (CacheKeyFiles.Any())
            {
                yield return new AzurePipelinesCacheStep
                             {
                                 KeyFiles = CacheKeyFiles,
                                 Path = CachePath
                             };
            }

            static string GetArtifactPath(AbsolutePath path)
                => NukeBuild.RootDirectory.Contains(path)
                    ? NukeBuild.RootDirectory.GetUnixRelativePathTo(path)
                    : (string) path;

            var publishedArtifacts = executableTarget.ArtifactProducts
                .Select(x => (AbsolutePath) x)
                .Select(x => x.DescendantsAndSelf(y => y.Parent).FirstOrDefault(y => !y.ToString().ContainsOrdinalIgnoreCase("*")))
                .Distinct()
                .Select(GetArtifactPath).ToArray();

            var artifactDependencies = from artifactDependency in ArtifactExtensions.ArtifactDependencies[executableTarget.Definition]
                where executableTarget.ExecutionDependencies.Any()
                let dependency = executableTarget.ExecutionDependencies.Single(x => x.Factory == artifactDependency.Item1)
                let rules = (artifactDependency.Item2.Any()
                        ? artifactDependency.Item2
                        : ArtifactExtensions.ArtifactProducts[dependency.Definition])
                    .Select(x => (AbsolutePath) x)
                    .Select(GetArtifactPath).ToArray()
                select rules;

            var dependencies = artifactDependencies as string[][] ?? artifactDependencies.ToArray();
            foreach (var rule in dependencies.SelectMany(rules => rules))
            {
                var artifactName = rule.Split('/').Last();
                
                yield return new AzurePipelinesDownloadStep
                       {
                           ArtifactName = artifactName,
                           DownloadPath = "./"
                       };
            }

            if (AzurePipelinesTargetDefinitionExtensions.PreSteps.TryGetValue(executableTarget.Name, out var preSteps))
            {
                foreach (var preStep in preSteps)
                {
                    yield return preStep;
                }
            }
            
            var chainLinkTargets = GetInvokedTargets(executableTarget, relevantTargets).ToArray();
            yield return new AzurePipelinesCmdStep
                         {
                             BuildCmdPath = BuildCmdPath,
                             PartitionSize = executableTarget.PartitionSize,
                             InvokedTargets = chainLinkTargets.Select(x => x.Name).ToArray(),
                             Imports = GetImports().ToDictionary(x => x.Key, x => x.Value),
                             WorkingDirectory = CmdWorkingDirectory,
                             GlobalNukeToolPackage = GlobalNukeToolPackage,
                             GlobalNukeToolExe = GlobalNukeToolExe
                         };

            if (AzurePipelinesTargetDefinitionExtensions.PostSteps.TryGetValue(executableTarget.Name, out var postSteps))
            {
                foreach (var postStep in postSteps)
                {
                    yield return postStep;
                }
            }

            
            foreach (var publishedArtifact in publishedArtifacts)
            {
                var artifactName = publishedArtifact.Split('/').Last();
                yield return new AzurePipelinesPublishStep
                             {
                                 ArtifactName = artifactName,
                                 PathToPublish = publishedArtifact
                             };
            }
        }

        protected virtual IEnumerable<(string Key, string Value)> GetImports()
        {
            static string GetSecretValue(string secret) => $"$({secret})";

            if (ImportSystemAccessTokenAs != null)
                yield return (ImportSystemAccessTokenAs, GetSecretValue("System.AccessToken"));

            foreach (var secret in ImportSecrets)
                yield return (secret, GetSecretValue(secret));

        }

        protected virtual string GetArtifact(string artifact)
        {
            if (NukeBuild.RootDirectory.Contains(artifact))
                artifact = GetRelativePath(NukeBuild.RootDirectory, artifact);

            return HasPathRoot(artifact)
                ? artifact
                : (UnixRelativePath) artifact;
        }
    }
}
