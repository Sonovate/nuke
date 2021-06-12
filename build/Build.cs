// Copyright 2019 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Components;
using static Nuke.Common.ControlFlow;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.ReSharper.ReSharperTasks;
using static Nuke.Common.Tools.GitVersion.GitVersionTasks;

[DotNetVerbosityMapping]
[ShutdownDotNetAfterServerBuild]
[PersistAzurePipelineVariables]
partial class Build
    : NukeBuild
{
    
    public static int Main() => Execute<Build>(x => x.B);

    [Variable] public string StringVariable { get; set; } = "var";

    // [Variable] GitVersion GitVersion { get; set; }
    
    Target A => _ => _
        .Executes(() =>
        {
            Logger.Normal(Verbosity.ToString());
            Logger.Normal(StringVariable);

            StringVariable = "A";

            // GitVersion = GitVersion(s => s.SetFramework("net5.0")).Result;
        });
            
    Target B => _ => _
        .DependsOn(A)
        .Executes(() =>
        {
            Logger.Normal(StringVariable);
            // Logger.Normal(GitVersion.NuGetVersionV2);
        });

    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath SourceDirectory => RootDirectory / "source";

    const string MasterBranch = "master";
    const string DevelopBranch = "develop";
    const string ReleaseBranchPrefix = "release";
    const string HotfixBranchPrefix = "hotfix";


    [Parameter] [Secret] readonly string PublicNuGetApiKey;

}
