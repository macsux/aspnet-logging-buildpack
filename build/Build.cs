using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Octokit;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using FileMode = System.IO.FileMode;
using ZipFile = System.IO.Compression.ZipFile;

[assembly: InternalsVisibleTo("AspNetLoggingBuildpackTests")]
[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    [Flags]
    public enum StackType
    {
        Windows = 1,
        Linux = 2
    }
    public static int Main () => Execute<Build>(x => x.Publish);
    const string BuildpackProjectName = "AspNetLoggingBuildpack";
    string GetPackageZipName(string runtime) => $"{BuildpackProjectName}-{runtime}-{GitVersion.MajorMinorPatch}.zip";

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
  
    [Parameter("GitHub personal access token with access to the repo")]
    string GitHubToken;

    [Parameter("Application directory against which buildpack will be applied")]
    readonly string ApplicationDirectory;

    readonly string Framework = "net472";
    readonly string Runtime = "win-x64";
    

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    
    string[] LifecycleHooks = {"detect", "supply", "release", "finalize"};

    Target Clean => _ => _
        .Description("Cleans up **/bin and **/obj folders")
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
        });

    Target Compile => _ => _
        .Description("Compiles the buildpack")
        .DependsOn(Clean)
        .Executes(() =>
        {
            
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion));
        });
    
    Target Publish => _ => _
        .Description("Packages buildpack in Cloud Foundry expected format into /artifacts directory")
        .DependsOn(Clean)
        .Executes(() =>
        {
            
                var packageZipName = GetPackageZipName(Runtime);
                var workDirectory = TemporaryDirectory / "pack";
                EnsureCleanDirectory(TemporaryDirectory);
                var buildpackProject = Solution.GetProject(BuildpackProjectName);
                if(buildpackProject == null)
                    throw new Exception($"Unable to find project called {BuildpackProjectName} in solution {Solution.Name}");
                var buildDirectory = buildpackProject.Directory / "bin" / Configuration / Framework / Runtime;
                var workBinDirectory = workDirectory / "bin";
                var workLibDirectory = workDirectory / "lib";


                DotNetBuild(s => s
                    .SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    // .SetFramework(framework)
                    // .SetRuntime(runtime)
                    .SetAssemblyVersion(GitVersion.AssemblySemVer)
                    .SetFileVersion(GitVersion.AssemblySemFileVer)
                    .SetInformationalVersion(GitVersion.InformationalVersion)
                );

                var lifecycleBinaries = Solution.GetProjects("Lifecycle*")
                    .Select(x => x.Directory / "bin" / Configuration / Framework / Runtime)
                    .SelectMany(x => Directory.GetFiles(x).Where(path => LifecycleHooks.Any(hook => Path.GetFileName(path).StartsWith(hook))));

                foreach (var lifecycleBinary in lifecycleBinaries)
                {
                    CopyFileToDirectory(lifecycleBinary, workBinDirectory, FileExistsPolicy.OverwriteIfNewer);
                }
                
                CopyDirectoryRecursively(buildDirectory, workBinDirectory, DirectoryExistsPolicy.Merge);
                CopyLibs(workLibDirectory);
                File.WriteAllText(workDirectory / "manifest.yml", "stack: windows");
                
                var tempZipFile = TemporaryDirectory / packageZipName;

                ZipFile.CreateFromDirectory(workDirectory, tempZipFile, CompressionLevel.NoCompression, false);
                CopyFileToDirectory(tempZipFile, ArtifactsDirectory, FileExistsPolicy.Overwrite);
                Logger.Block(ArtifactsDirectory / packageZipName);
            
        });

    void CopyLibs(AbsolutePath libsDir)
    {
        var project = Solution.GetProject("AspNetLoggingBuildpackModule");
        var assemblyName = project.Name;
        
        var objFolder = project.Directory / "obj";
        // map side-by-side assembly loading from libs folder
        var assetsFile = objFolder / "project.assets.json";
	    var assetsDoc = JObject.Parse(File.ReadAllText(assetsFile));
        var referenceAssemblies = assetsDoc["targets"][".NETFramework,Version=v4.7.2"]
		    .Cast<JProperty>()
		    .Where(x => x.Value["type"].ToString() == "package")
		    .SelectMany(item =>
		    {
			    var assemblyNameAndVersion = item.Name;
			    var srcFolder = assetsDoc["libraries"][assemblyNameAndVersion]["path"].ToString();
                return ((JObject) item.Value["runtime"])
                    ?.Properties()
                    .Select(x => Path.Combine(srcFolder, x.Name).Replace('/', Path.DirectorySeparatorChar)) ?? Enumerable.Empty<string>();
            })
            .ToList();
        var projectDlls = assetsDoc["targets"][".NETFramework,Version=v4.7.2"]
            .Cast<JProperty>()
            .Where(x => x.Value["type"].ToString() == "project")
            .SelectMany(item => ((JObject) item.Value["runtime"]).Properties().Select(x => Path.GetFileName(x.Name)))
            .ToList();

        var userProfileDir = (AbsolutePath) Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	    var nugetCache = (AbsolutePath)Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? userProfileDir / ".nuget" / "packages";

        foreach (var file in referenceAssemblies)
        {
            CopyFile(nugetCache / file, libsDir / "libs" / file, FileExistsPolicy.OverwriteIfNewer);
        }
        var publishDir = project.Directory / "bin" / Configuration / "net472" / "win-x64" ;
        foreach (var projectDll in projectDlls)
        {
            CopyFile(publishDir / projectDll, libsDir / projectDll, FileExistsPolicy.OverwriteIfNewer);
        }

        var assemblyFileName = $"{assemblyName}.dll";
        File.WriteAllText(libsDir / ".httpModule", assemblyFileName);
        File.Copy(publishDir / assemblyFileName, libsDir / assemblyFileName);

    }
    Target Release => _ => _
        .Description("Creates a GitHub release (or amends existing) and uploads buildpack artifact")
        .DependsOn(Publish)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            
                var packageZipName = GetPackageZipName(Runtime);
                if (!GitRepository.IsGitHubRepository())
                    throw new Exception("Only supported when git repo remote is github");
    
                var client = new GitHubClient(new ProductHeaderValue(BuildpackProjectName))
                {
                    Credentials = new Credentials(GitHubToken, AuthenticationType.Bearer)
                };
                var gitIdParts = GitRepository.Identifier.Split("/");
                var owner = gitIdParts[0];
                var repoName = gitIdParts[1];
    
                var releaseName = $"v{GitVersion.MajorMinorPatch}";
                Release release;
                try
                {
                    release = await client.Repository.Release.Get(owner, repoName, releaseName);
                }
                catch (NotFoundException)
                {
                    var newRelease = new NewRelease(releaseName)
                    {
                        Name = releaseName,
                        Draft = false,
                        Prerelease = false
                    };
                    release = await client.Repository.Release.Create(owner, repoName, newRelease);
                }
    
                var existingAsset = release.Assets.FirstOrDefault(x => x.Name == packageZipName);
                if (existingAsset != null)
                {
                    await client.Repository.Release.DeleteAsset(owner, repoName, existingAsset.Id);
                }
    
                var zipPackageLocation = ArtifactsDirectory / packageZipName;
                var stream = File.OpenRead(zipPackageLocation);
                var releaseAssetUpload = new ReleaseAssetUpload(packageZipName, "application/zip", stream, TimeSpan.FromHours(1));
                var releaseAsset = await client.Repository.Release.UploadAsset(release, releaseAssetUpload);
    
                Logger.Block(releaseAsset.BrowserDownloadUrl);
            
        });

    Target Detect => _ => _
        .Description("Invokes buildpack 'detect' lifecycle event")
        .Requires(() => ApplicationDirectory)
        .Executes(() =>
        {
            try
            {
                DotNetRun(s => s
                    .SetProjectFile(Solution.GetProject("Lifecycle.Detect").Path)
                    .SetApplicationArguments(ApplicationDirectory)
                    .SetConfiguration(Configuration)
                    .SetFramework("net472"));
                Logger.Block("Detect returned 'true'");
            }
            catch (ProcessException)
            {
                Logger.Block("Detect returned 'false'");
            }
        });

    Target Supply => _ => _
        .Description("Invokes buildpack 'supply' lifecycle event")
        .Requires(() => ApplicationDirectory)
        .Executes(() =>
        {
            var home = (AbsolutePath)Path.GetTempPath() / Guid.NewGuid().ToString();
            var app = home / "app";
            var deps = home / "deps";
            var index = 0;
            var cache = home / "cache";
            CopyDirectoryRecursively(ApplicationDirectory, app);

            DotNetRun(s => s
                .SetProjectFile(Solution.GetProject("Lifecycle.Supply").Path)
                .SetApplicationArguments($"{app} {cache} {deps} {index}")
                .SetConfiguration(Configuration)
                .SetFramework("net472"));
            Logger.Block($"Buildpack applied. Droplet is available in {home}");

        });

}
