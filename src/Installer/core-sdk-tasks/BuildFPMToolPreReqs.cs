// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// This task prepares the command line parameters for running a RPM build using FPM tool and also updates the copyright and changelog file tokens.
    /// If parses various values from the config json by first reading it into a model and then builds the required string for parameters and passes it back.
    /// 
    /// </summary>
    public class BuildFPMToolPreReqs : Task
    {
        [Required]
        public string InputDir { get; set; }

        [Required]
        public string OutputDir { get; set; }

        [Required]
        public string PackageVersion { get; set; }

        [Required]
        public string ConfigJsonFile { get; set; }

        [Output]
        public string FPMParameters { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(ConfigJsonFile))
            {
                throw new FileNotFoundException($"Expected file {ConfigJsonFile} was not found.");
            }

            // Open the Config Json and read the values into the model
            string jsonFileText = File.ReadAllText(ConfigJsonFile, Encoding.UTF8);
            ConfigJson configJson = JsonConvert.DeserializeObject<ConfigJson>(jsonFileText);

            // Update the Changelog and Copyright files by replacing tokens with values from config json
            UpdateChangelog(configJson, PackageVersion);
            UpdateCopyright(configJson);

            // Build the full list of parameters 
            FPMParameters = BuildCmdParameters(configJson, PackageVersion);
            Log.LogMessage(MessageImportance.Normal, "Generated RPM parameters:  " + FPMParameters);

            return !Log.HasLoggedErrors;
        }

        // Update the tokens in the changelog file from the config Json 
        private void UpdateChangelog(ConfigJson configJson, string package_version)
        {
            string changelogFile = Path.Combine(InputDir, "templates", "changelog");
            if (!File.Exists(changelogFile))
            {
                throw new FileNotFoundException($"Expected file {changelogFile} was not found.");
            }
            string str = File.ReadAllText(changelogFile);
            str = str.Replace("{PACKAGE_NAME}", configJson.Package_Name);
            str = str.Replace("{PACKAGE_VERSION}", package_version);
            str = str.Replace("{PACKAGE_REVISION}", configJson.Release.Package_Revision);
            str = str.Replace("{CHANGELOG_MESSAGE}", configJson.Release.Changelog_Message);
            str = str.Replace("{MAINTAINER_NAME}", configJson.Maintainer_Name);
            str = str.Replace("{MAINTAINER_EMAIL}", configJson.Maintainer_Email);
            // The date format needs to be like Wed May 17 2017
            str = str.Replace("{DATE}", DateTime.UtcNow.ToString("ddd MMM dd yyyy"));
            File.WriteAllText(changelogFile, str);
        }

        public void UpdateCopyright(ConfigJson configJson)
        {
            string copyrightFile = Path.Combine(InputDir, "templates", "copyright");
            if (!File.Exists(copyrightFile))
            {
                throw new FileNotFoundException($"Expected file {copyrightFile} was not found.");
            }
            string str = File.ReadAllText(copyrightFile);
            str = str.Replace("{COPYRIGHT_TEXT}", configJson.CopyRight);
            str = str.Replace("{LICENSE_NAME}", configJson.License.Type);
            str = str.Replace("{LICENSE_TEXT}", configJson.License.Full_Text);
            File.WriteAllText(copyrightFile, str);
        }

        private string BuildCmdParameters(ConfigJson configJson, string package_version)
        {
            // Parameter list that needs to be passed to FPM tool:
            //      -s : is the input source type(dir)  --Static  
            //      -t : is the type of package(rpm)    --Static
            //      -n : is for the name of the package --JSON
            //      -v : is the version to give to the package --ARG
            //      -a : architecture  --JSON
            //      -d : is for all dependent packages. This can be used multiple times to specify the dependencies of the package.   --JSON
            //      --rpm-os : the operating system to target this rpm  --Static
            //      --rpm-digest : rpm digest algorithm  --Static
            //      --rpm-changelog : the changelog from FILEPATH contents  --ARG
            //      --rpm-summary : it is the RPM summary that shows in the Title   --JSON
            //      --description : it is the description for the package   --JSON
            //      -p : The actual package name (with path) for your package. --ARG+JSON
            //      --conflicts : Other packages/versions this package conflicts with provided as CSV   --JSON
            //      --directories : Recursively add directories as being owned by the package.    --JSON
            //      --after-install : FILEPATH to the script to be run after install of the package     --JSON
            //      --after-remove : FILEPATH to the script to be run after package removal     --JSON
            //      --license : the licensing name for the package. This will include the license type in the meta-data for the package, but will not include the associated license file within the package itself.    --JSON
            //      --iteration : the iteration to give to the package. This comes from the package_revision    --JSON
            //      --url : url for this package.   --JSON
            //      --verbose : Set verbose output for FPM tool     --Static  
            //      <All folder mappings> : Add all the folder mappings for package_root, docs, man pages   --Static

            var parameters = new List<string>
            {
                "-s dir",
                "-t rpm",
                string.Concat("-n ", configJson.Package_Name),
                string.Concat("-v ", package_version),
                string.Concat("-a ", configJson.Control.Architecture)
            };

            // Build the list of dependencies as -d <dep1> -d <dep2>
            if (configJson.Rpm_Dependencies != null)
            {
                foreach (RpmDependency rpmdep in configJson.Rpm_Dependencies)
                {
                    if (rpmdep.Package_Name != "")
                    {
                        // If no version is specified then the dependency is just the package without >= check
                        string dependency = rpmdep.Package_Version != "" ?
                            string.Concat(rpmdep.Package_Name, " >= ", rpmdep.Package_Version) :
                            rpmdep.Package_Name;

                        parameters.Add(string.Concat("-d ", EscapeArg(dependency)));
                    }
                }
            }

            // Build the list of owned directories
            if (configJson.Directories != null)
            {
                foreach (string dir in configJson.Directories)
                {
                    if (dir != "")
                    {
                        parameters.Add(string.Concat("--directories ", EscapeArg(dir)));
                    }
                }
            }

            parameters.Add("--rpm-os linux");
            parameters.Add("--rpm-digest sha256");
            parameters.Add(string.Concat("--rpm-changelog ",
                EscapeArg(Path.Combine(InputDir, "templates", "changelog")))); // Changelog File
            parameters.Add(string.Concat("--rpm-summary ", EscapeArg(configJson.Short_Description)));
            parameters.Add(string.Concat("--description ", EscapeArg(configJson.Long_Description)));
            parameters.Add(string.Concat("--maintainer ",
                EscapeArg(configJson.Maintainer_Name + " <" + configJson.Maintainer_Email + ">")));
            parameters.Add(string.Concat("--vendor ", EscapeArg(configJson.Vendor)));
            parameters.Add(string.Concat("-p ", Path.Combine(OutputDir, configJson.Package_Name + ".rpm")));
            if (configJson.Package_Conflicts != null)
                parameters.Add(string.Concat("--conflicts ",
                    EscapeArg(string.Join(",", configJson.Package_Conflicts))));
            if (configJson.After_Install_Source != null)
                parameters.Add(string.Concat("--after-install ",
                    Path.Combine(InputDir, EscapeArg(configJson.After_Install_Source))));
            if (configJson.After_Remove_Source != null)
                parameters.Add(string.Concat("--after-remove ",
                    Path.Combine(InputDir, EscapeArg(configJson.After_Remove_Source))));
            parameters.Add(string.Concat("--license ", EscapeArg(configJson.License.Type)));
            parameters.Add(string.Concat("--iteration ", configJson.Release.Package_Revision));
            parameters.Add(string.Concat("--url ", "\"", EscapeArg(configJson.Homepage), "\""));
            parameters.Add("--verbose");

            // Map all the payload directories as they need to install on the system 
            if (configJson.Install_Root != null)
                parameters.Add(string.Concat(Path.Combine(InputDir, "package_root/="), configJson.Install_Root)); // Package Files
            if (configJson.Install_Man != null)
                parameters.Add(string.Concat(Path.Combine(InputDir, "docs", "host/="), configJson.Install_Man)); // Man Pages
            if (configJson.Install_Doc != null)
                parameters.Add(string.Concat(Path.Combine(InputDir, "templates", "copyright="), configJson.Install_Doc)); // CopyRight File

            return string.Join(" ", parameters);
        }

        private string EscapeArg(string arg)
        {
            var sb = new StringBuilder();

            bool quoted = ShouldSurroundWithQuotes(arg);
            if (quoted) sb.Append("\"");

            for (int i = 0; i < arg.Length; ++i)
            {
                var backslashCount = 0;

                // Consume All Backslashes
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashCount++;
                    i++;
                }

                // Escape any backslashes at the end of the arg
                // This ensures the outside quote is interpreted as
                // an argument delimiter
                if (i == arg.Length)
                {
                    sb.Append('\\', 2 * backslashCount);
                }
                // Escape any preceding backslashes and the quote
                else if (arg[i] == '"')
                {
                    sb.Append('\\', (2 * backslashCount) + 1);
                    sb.Append('"');
                }
                // Output any consumed backslashes and the character
                else
                {
                    sb.Append('\\', backslashCount);
                    sb.Append(arg[i]);
                }
            }

            if (quoted) sb.Append("\"");

            return sb.ToString();
        }

        private bool ShouldSurroundWithQuotes(string argument)
        {
            // Don't quote already quoted strings
            if (argument.StartsWith("\"", StringComparison.Ordinal) &&
                argument.EndsWith("\"", StringComparison.Ordinal))
            {
                return false;
            }

            // Only quote if whitespace exists in the string
            if (argument.Contains(" ") || argument.Contains("\t") || argument.Contains("\n"))
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Model classes for reading and storing the JSON. 
    /// </summary>
    public class ConfigJson
    {
        public string Maintainer_Name { get; set; }
        public string Maintainer_Email { get; set; }
        public string Vendor { get; set; }
        public string Package_Name { get; set; }
        public string Install_Root { get; set; }
        public string Install_Doc { get; set; }
        public string Install_Man { get; set; }
        public string Short_Description { get; set; }
        public string Long_Description { get; set; }
        public string Homepage { get; set; }
        public string CopyRight { get; set; }
        public Release Release { get; set; }
        public Control Control { get; set; }
        public License License { get; set; }
        public List<RpmDependency> Rpm_Dependencies { get; set; }
        public List<string> Package_Conflicts { get; set; }
        public List<string> Directories { get; set; }
        public string After_Install_Source { get; set; }
        public string After_Remove_Source { get; set; }
    }

    public class Release
    {
        public string Package_Version { get; set; }
        public string Package_Revision { get; set; }
        public string Changelog_Message { get; set; }
    }

    public class Control
    {
        public string Architecture { get; set; }
    }

    public class License
    {
        public string Type { get; set; }
        public string Full_Text { get; set; }
    }

    public class RpmDependency
    {
        public string Package_Name { get; set; }
        public string Package_Version { get; set; }
    }
}
