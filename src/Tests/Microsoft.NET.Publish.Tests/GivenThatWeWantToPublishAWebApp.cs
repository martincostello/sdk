// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAWebApp : SdkTest
    {
        public GivenThatWeWantToPublishAWebApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact(Skip = "tmp")]
        public void It_publishes_as_framework_dependent_by_default()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("WebApp")
                .WithSource();

            var args = new[]
            {
                "-p:Configuration=Release"
            };

            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand
                .Execute(args)
                .Should()
                .Pass();

            var command = new PublishCommand(testAsset);

            command
                .Execute(args)
                .Should()
                .Pass();

            var publishDirectory =
                command.GetOutputDirectory(targetFramework: "netcoreapp2.0", configuration: "Release");

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().OnlyHaveFiles(new[] {
                "web.config",
                "web.deps.json",
                "web.dll",
                "web.pdb",
                "web.PrecompiledViews.dll",
                "web.PrecompiledViews.pdb",
                "web.runtimeconfig.json",
            });
        }

        [Fact(Skip = "tmp")]
        public void It_should_publish_self_contained_for_2x()
        {
            var tfm = "netcoreapp2.2";

            var testProject = new TestProject()
            {
                Name = "WebTest",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.App"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var command = new PublishCommand(testProjectInstance);

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            command
                .Execute($"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(
                targetFramework: tfm,
                runtimeIdentifier: rid);

            output.Should().HaveFiles(new[] {
                $"{testProject.Name}{Constants.ExeSuffix}",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
            });

            output.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });

            new RunExeCommand(Log, Path.Combine(output.FullName, $"{testProject.Name}{Constants.ExeSuffix}"))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }


        [Theory(Skip = "tmp")]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void It_should_publish_framework_dependent_for_2x(string platformLibrary)
        {
            var tfm = "netcoreapp2.2";

            var testProject = new TestProject()
            {
                Name = "WebTest",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference(platformLibrary));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var command = new PublishCommand(testProjectInstance);

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            command
                .Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=false")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(
                targetFramework: tfm,
                runtimeIdentifier: rid);

            output.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}{Constants.ExeSuffix}",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
            });
        }

        [Theory(Skip = "tmp")]
        [InlineData(null, null)]
        [InlineData(false, null)]
        [InlineData(true, null)]
        [InlineData(null, false)]
        [InlineData(null, true)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void It_publishes_with_a_publish_profile(bool? selfContained, bool? useAppHost)
        {
            var tfm = "netcoreapp2.2";
            var rid = EnvironmentInfo.GetCompatibleRid(tfm);

            var testProject = new TestProject()
            {
                Name = "WebWithPublishProfile",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.App"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
            Directory.CreateDirectory(publishProfilesDirectory);

            File.WriteAllText(Path.Combine(publishProfilesDirectory, "test.pubxml"), $@"
<Project>
  <PropertyGroup>
    <RuntimeIdentifier>{rid}</RuntimeIdentifier>
    {(selfContained.HasValue ? $"<SelfContained>{selfContained}</SelfContained>" : "")}
    {((!(selfContained ?? true) && useAppHost.HasValue) ? $"<UseAppHost>{useAppHost}</UseAppHost>" : "")}
  </PropertyGroup>
</Project>
");

            var command = new PublishCommand(testProjectInstance);
            command
                .Execute("/p:PublishProfile=test")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(targetFramework: tfm, runtimeIdentifier: rid);

            output.Should().HaveFiles(new[] {
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
            });

            if (selfContained ?? true)
            {
                output.Should().HaveFiles(new[] {
                    $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                    $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                });
            }
            else
            {
                output.Should().NotHaveFiles(new[] {
                    $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                    $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                });
            }

            if ((selfContained ?? true) || (useAppHost ?? true))
            {
                output.Should().HaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }
            else
            {
                output.Should().NotHaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }
        }
    }
}
