using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sprawl {

    public class BuildTools {

        private static string GetArgument(string argument, string default_value) {
            string plus_argument = string.Format("+{0}", argument);
            string[] commandline_args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < commandline_args.Length - 1; ++i) {
                if (commandline_args[i].Equals(plus_argument)) {
                    return commandline_args[i + 1];
                }
            }
            return default_value;
        }

        private static bool GetArgument(string argument, bool default_value) {
            string plus_argument = string.Format("+{0}", argument);
            string plus_no_argument = string.Format("+no-{0}", argument);
            string[] commandline_args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < commandline_args.Length; ++i) {
                if (commandline_args[i].Equals(plus_argument)) {
                    return true;
                }
                if (commandline_args[i].Equals(plus_no_argument)) {
                    return false;
                }
            }
            return default_value;
        }

        private static BuildTarget GetTarget() {
            string target_string = GetArgument("target", null);
            if (target_string == "Linux") {
                return BuildTarget.StandaloneLinux64;
            } else if (target_string == "Darwin") {
                return BuildTarget.StandaloneOSX;
            } else {
                Application.Quit(1);
                return BuildTarget.StandaloneLinux64;
            }
        }

        private static string GetDirectory(BuildTarget target, string base_dir, string name) {
            if (target == BuildTarget.StandaloneLinux64) {
                return string.Format("{0}/{1}/Linux-x86_64", base_dir, name);
            } else if (target == BuildTarget.StandaloneOSX) {
                return string.Format("{0}/{1}/Darwin-x86_64", base_dir, name);
            } else {
                return null;
            }
        }

        private static string GetBinary(BuildTarget target, string location_dir, string name) {
            if (target == BuildTarget.StandaloneLinux64) {
                return string.Format("{0}/{1}.x86_64", location_dir, name);
            } else if (target == BuildTarget.StandaloneOSX) {
                return string.Format("{0}/{1}.app", location_dir, name);
            } else {
                return null;
            }
        }

        static void BuildCommandline() {
            string name = GetArgument("name", null);
            string scene = GetArgument("scene", null);
            string base_dir = GetArgument("base_dir", null);

            if (name == null || scene == null || base_dir == null) {
                System.Console.WriteLine("Need to provide name, scene & base_dir.");
                return;
            } else {
                System.Console.WriteLine("Building: {0}, {1}, {2}.", name, scene, base_dir);
            }

            BuildDefault(BuildTarget.StandaloneOSX, base_dir, scene, name);
            BuildDefault(BuildTarget.StandaloneLinux64, base_dir, scene, name);
        }

        static void BuildTargetCommandline() {
            string directory = GetArgument("directory", null);
            string name = GetArgument("name", null);
            string scene = GetArgument("scene", null);
            BuildTarget target = GetTarget();

            if (directory == null || name == null || scene == null) {
                System.Console.WriteLine("Need to provide directory, name, scene & target.");
                return;
            } else {
                System.Console.WriteLine("Building: {0}, {1}, {2}, {3}.", directory, name, scene, target);
            }

            string binary = GetBinary(target, directory, name);

            Build(target, directory, binary, scene);
        }

        private static BuildOptions GetOptions(BuildTarget target) {
            if (GetArgument("graphics", true) == false) {
                System.Console.WriteLine("Building headless.");
                return BuildOptions.EnableHeadlessMode;
            } else {
                System.Console.WriteLine("Building standard.");
                return BuildOptions.None;
            }
        }

        private static void BuildDefault(BuildTarget target, string bin_dir, string scene, string name) {
            string directory = GetDirectory(target, bin_dir, name);
            string binary = GetBinary(target, directory, name);

            Build(target, directory, binary, scene);
        }

        private static void Build(BuildTarget target, string directory, string binary, string scene) {

            System.Console.WriteLine(string.Format("Building: {0}", binary));

            Directory.CreateDirectory(directory);
            BuildPlayerOptions options = new BuildPlayerOptions {
                options = GetOptions(target),
                scenes = new string[] { scene },
                target = target,
                locationPathName = binary
            };

            BuildPipeline.BuildPlayer(options);
        }

    }

}