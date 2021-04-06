// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildIntrospectionTest : AspNetSdkTest
    {
        public BuildIntrospectionTest(ITestOutputHelper log) : base(log) {}

        [Fact(Skip = "tmp")]
        public void RazorSdk_AddsCshtmlFilesToUpToDateCheckInput()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateCheck")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"UpToDateCheckInput: {Path.Combine("Views", "Home", "Index.cshtml")}")
                .And.HaveStdOutContaining($"UpToDateCheckInput: {Path.Combine("Views", "_ViewStart.cshtml")}");
        }

        [Fact(Skip = "tmp")]
        public void UpToDateReloadFileTypes_Default()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;.cshtml");
        }

        [Fact(Skip = "tmp")]
        public void UpToDateReloadFileTypes_WithRuntimeCompilation()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "RazorUpToDateReloadFileTypes", @"$(RazorUpToDateReloadFileTypes.Replace('.cshtml', ''))"));
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;");
        }

        [Fact(Skip = "tmp")]
        public void UpToDateReloadFileTypes_WithwWorkAroundRemoved()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;.cshtml");
        }

        [Fact(Skip = "tmp")]
        public void UpToDateReloadFileTypes_WithRuntimeCompilationAndWorkaroundRemoved()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "RazorUpToDateReloadFileTypes", @"$(RazorUpToDateReloadFileTypes.Replace('.cshtml', ''))"));
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes", "/p:_RazorUpToDateReloadFileTypesAllowWorkaround=false")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;");
        }

        [Fact(Skip = "tmp")]
        public void IntrospectRazorSdkWatchItems()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new MSBuildCommand(Log, "_IntrospectWatchItems", projectDirectory.Path);
            build.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Watch: Index.razor")
                .And.HaveStdOutContaining("Watch: Index.razor.css");
        }

        [Fact(Skip = "tmp")]
        public void IntrospectRazorDesignTimeTargets()
        {
            var expected1 = Path.Combine("Components", "App.razor");
            var expected2 = Path.Combine("Components", "Shared", "MainLayout.razor");
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new MSBuildCommand(Log, "_IntrospectRazorGenerateComponentDesignTime", projectDirectory.Path);
            build.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"RazorComponentWithTargetPath: App {expected1}")
                .And.HaveStdOutContaining($"RazorComponentWithTargetPath: MainLayout {expected2}");
        }
    }
}
