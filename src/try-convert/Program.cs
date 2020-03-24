﻿using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Build.Locator;

using MSBuild.Abstractions;
using MSBuild.Conversion.Project;

namespace MSBuild.Conversion
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var parser = new CommandLineBuilder(new Command("try-convert", handler: CommandHandler.Create(typeof(Program).GetMethod(nameof(Run)))))
                .UseParseDirective()
                .UseHelp()
                .UseDebugDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseParseErrorReporting()
                .UseExceptionHandler()
                .AddOption(new Option(new[] { "-p", "--project" }, "The path to a project to convert", new Argument<string>()))
                .AddOption(new Option(new[] { "-w", "--workspace" }, "The solution or project file to operate on. If a project is not specified, the command will search the current directory for one.", new Argument<string>()))
                .AddOption(new Option(new[] { "-m", "--msbuild-path" }, "The path to an MSBuild.exe, if you prefer to use that", new Argument<string>()))
                .AddOption(new Option(new[] { "-tfm", "--target-framework" }, "The name of the framework you would like to upgrade to", new Argument<string>()))
                .AddOption(new Option(new[] { "--diff-only" }, "Produces a diff of the project to convert; no conversion is done", new Argument<bool>()))
                .AddOption(new Option(new[] { "--no-backup" }, "Converts projects and does not create a backup of the originals.", new Argument<bool>()))
                .Build();

            return await parser.InvokeAsync(args.Length > 0 ? args : new string[] { "-h" }).ConfigureAwait(false);
        }

        public static int Run(string project, string workspace, string msbuildPath, string tfm, bool diffOnly, bool noBackup)
        {
            if (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(workspace))
            {
                Console.WriteLine("Cannot specify both a project and a workspace.");
                return -1;
            }


            try
            {
                msbuildPath = MSBuildHelpers.HookAssemblyResolveForMSBuild(msbuildPath);
                if (string.IsNullOrWhiteSpace(msbuildPath))
                {
                    Console.WriteLine("Could not find an MSBuild.");
                    return -1;
                }

                if (tfm is null)
                {
                    tfm = FindHighestInstalledTargetFramework();
                    if (tfm is null)
                    {
                        tfm = "netcoreapp3.1";
                    }
                }

                var currentDirectory = Environment.CurrentDirectory;
                var workspacePath = string.Empty;
                MSBuildWorkspaceType workspaceType;

                if (!string.IsNullOrWhiteSpace(project))
                {
                    workspacePath = Path.GetFullPath(project, Environment.CurrentDirectory);
                    workspaceType = MSBuildWorkspaceType.Project;
                }
                else if (!string.IsNullOrWhiteSpace(workspace))
                {
                    var (isSolution, workspaceFilePath) = MSBuildWorkspaceFinder.FindWorkspace(currentDirectory, workspace);
                    workspaceType = isSolution ? MSBuildWorkspaceType.Solution : MSBuildWorkspaceType.Project;
                    workspacePath = workspaceFilePath;
                }
                else
                {
                    throw new ArgumentException("No valid arguments to fulfill a workspace are given.");
                }

                var workspaceLoader = new MSBuildWorkspaceLoader(workspacePath, workspaceType);
                var msbuildWorkspace = workspaceLoader.LoadWorkspace(workspacePath, noBackup);

                foreach (var item in msbuildWorkspace.WorkspaceItems)
                {
                    if (diffOnly)
                    {
                        var differ = new Differ(item.UnconfiguredProject.FirstConfiguredProject, item.SdkBaselineProject.Project.FirstConfiguredProject);
                        differ.GenerateReport(Directory.GetParent(workspacePath).FullName);
                    }
                    else
                    {
                        var converter = new Converter(item.UnconfiguredProject, item.SdkBaselineProject, item.ProjectRootElement);
                        converter.Convert(tfm, item.ProjectRootElement.FullPath);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return -1;
            }

            Console.WriteLine("Conversion complete!");
            return 0;
        }

        private static string FindHighestInstalledTargetFramework()
        {
            // Finds SDK path
            string sdkPath = null;
            try
            {
                sdkPath = Path.GetFullPath(Path.Combine(MSBuildLocator.QueryVisualStudioInstances().Single().VisualStudioRootPath, "..", ".."));
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to find the .NET SDK on this machine, manually pass '-tfm'");
                throw;
            }

            // Find templates path
            var templatesPath = Path.Combine(sdkPath, "templates");

            // Find highest SDK path (should include previews?)
            var largestVersion = SemanticVersion.Min;
            var templatePath = string.Empty;
            foreach (var templateDirectory in Directory.EnumerateDirectories(templatesPath))
            {
                var templatesVersion = SemanticVersion.Parse(Path.GetFileName(templateDirectory));
                if (templatesVersion > largestVersion)
                {
                    largestVersion = templatesVersion;
                    templatePath = Path.GetFullPath(templateDirectory);
                }
            }

            // upzip the common project templates into memory
            var templateNugetPackagePath = Directory.EnumerateFiles(templatePath, "microsoft.dotnet.common.projecttemplates.*.nupkg", SearchOption.TopDirectoryOnly).Single();
            using var templateNugetPackageFile = File.OpenRead(templateNugetPackagePath);
            using var templateNugetPackage = new ZipArchive(templateNugetPackageFile, ZipArchiveMode.Read);
            var templatesJsonFile = templateNugetPackage.Entries
                .Where(x => x.Name.Equals("template.json", StringComparison.OrdinalIgnoreCase) &&
                            x.FullName.Contains("ClassLibrary-CSharp", StringComparison.OrdinalIgnoreCase)).Single();
            using var templatesJson = templatesJsonFile.Open();

            // read the template.json file to see what the tfm is called
            var doc = JsonDocument.ParseAsync(templatesJson).GetAwaiter().GetResult();

            return doc.RootElement.GetProperty("baselines").GetProperty("app").GetProperty("defaultOverrides").GetProperty("Framework").GetString();
        }
    }
}
