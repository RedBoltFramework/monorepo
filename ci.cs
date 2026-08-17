#!/bin/sh
#if false
exec dotnet run --file "$0" -- "$@"
#endif

using System.Diagnostics;
using System.Runtime.CompilerServices;

return await CiApp.RunAsync(args);

static class CiApp
{
    private static readonly Module[] Modules =
    [
        new("RedBolt", "RedBolt.slnx"),
        new("RedBolt.2D", "RedBolt.2D.slnx"),
        new("RedBolt.Actions", "RedBolt.Actions.slnx"),
        new("RedBolt.CSG", "RedBolt.CSG.slnx"),
        new("RedBolt.Glow", "RedBolt.Glow.slnx"),
        new("RedBolt.IK", "RedBolt.IK.slnx"),
        new("RedBolt.Navigation", "RedBolt.Navigation.slnx"),
        new("RedBolt.Network", "RedBolt.Network.slnx"),
        new("RedBolt.Particles", "RedBolt.Particles.slnx"),
        new("RedBolt.PhysicsBox2D", "RedBolt.PhysicsBox2D.slnx"),
        new("RedBolt.PhysicsJolt", "RedBolt.PhysicsJolt.slnx", "RedBolt.Network"),
        new("RedBolt.RmlUI", "RedBolt.RmlUI.slnx"),
        new("RedBolt.XR", "RedBolt.XR.slnx"),
    ];

    private static readonly string[] SupportedPlatforms = ["linux", "windows", "android", "web"];
    private static readonly string[] SupportedArchitectures = ["x64", "arm64"];

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParseOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine($"ci.cs: {error}");
            Console.Error.WriteLine("Run './ci.cs --help' for usage.");
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var root = GetScriptDirectory();
        ConfigureEnvironment();
        if (!PrepareWorkspace(root, options, out var availableModules, out error))
        {
            Console.Error.WriteLine($"ci.cs: {error}");
            return 2;
        }

        var runner = new PipelineRunner(root, availableModules, options);
        return await runner.RunAsync();
    }

    private static bool TryParseOptions(string[] args, out Options options, out string? error)
    {
        options = new Options();
        error = null;
        var platformsSpecified = false;
        var architecturesSpecified = false;
        var modulesSpecified = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "--")
            {
                options.TestArguments.AddRange(args[(index + 1)..]);
                break;
            }

            var separator = argument.IndexOf('=');
            var name = separator >= 0 ? argument[..separator] : argument;
            var inlineValue = separator >= 0 ? argument[(separator + 1)..] : null;

            switch (name)
            {
                case "-h" or "--help":
                    options.ShowHelp = true;
                    break;
                case "--incremental":
                    options.Incremental = true;
                    break;
                case "--no-self-contained":
                    options.NoSelfContained = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--module" or "--modules":
                    if (!TryReadValue(args, ref index, name, inlineValue, out var modules, out error))
                        return false;
                    if (!modulesSpecified)
                    {
                        options.Modules.Clear();
                        modulesSpecified = true;
                    }
                    if (!TryAddSelections(modules, Modules.Select(module => module.Directory).ToArray(), options.Modules, "module", out error))
                        return false;
                    break;
                case "--build-config" or "--build-configuration":
                    if (!TryReadValue(args, ref index, name, inlineValue, out var buildConfiguration, out error) ||
                        !TryParseConfigurations(buildConfiguration, out var buildConfigurations, out error))
                        return false;
                    options.BuildConfigurations = buildConfigurations;
                    break;
                case "--test-config" or "--test-configuration":
                    if (!TryReadValue(args, ref index, name, inlineValue, out var testConfiguration, out error) ||
                        !TryParseConfigurations(testConfiguration, out var testConfigurations, out error))
                        return false;
                    options.TestConfigurations = testConfigurations;
                    break;
                case "--platform" or "--platforms":
                    if (!TryReadValue(args, ref index, name, inlineValue, out var platforms, out error))
                        return false;
                    if (!platformsSpecified)
                    {
                        options.Platforms.Clear();
                        platformsSpecified = true;
                    }
                    if (!TryAddSelections(platforms, SupportedPlatforms, options.Platforms, "platform", out error))
                        return false;
                    break;
                case "--arch" or "--architecture" or "--architectures":
                    if (!TryReadValue(args, ref index, name, inlineValue, out var architectures, out error))
                        return false;
                    if (!architecturesSpecified)
                    {
                        options.Architectures.Clear();
                        architecturesSpecified = true;
                    }
                    if (!TryAddSelections(architectures, SupportedArchitectures, options.Architectures, "architecture", out error))
                        return false;
                    break;
                case "--tests" or "--test-filter":
                    if (!TryReadValue(args, ref index, name, inlineValue, out var testFilter, out error))
                        return false;
                    options.TestArguments.Add("--filter");
                    options.TestArguments.Add(testFilter);
                    break;
                case "-j" or "--jobs":
                    if (!TryReadPositiveInteger(args, ref index, name, inlineValue, out var parallelization, out error))
                        return false;
                    options.Parallelization = parallelization;
                    break;
                case "--test-shards":
                    if (!TryReadPositiveInteger(args, ref index, name, inlineValue, out var shards, out error))
                        return false;
                    options.TestShards = shards;
                    break;
                default:
                    error = $"Unknown argument '{argument}'.";
                    return false;
            }
        }

        return true;
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string option,
        string? inlineValue,
        out string value,
        out string? error)
    {
        if (inlineValue is not null)
        {
            value = inlineValue;
        }
        else if (index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            value = args[++index];
        }
        else
        {
            value = "";
            error = $"{option} requires a value.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{option} requires a value.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadPositiveInteger(
        string[] args,
        ref int index,
        string option,
        string? inlineValue,
        out int value,
        out string? error)
    {
        if (!TryReadValue(args, ref index, option, inlineValue, out var text, out error))
        {
            value = 0;
            return false;
        }

        if (!int.TryParse(text, out value) || value < 1)
        {
            error = $"{option} must be a positive integer.";
            return false;
        }

        return true;
    }

    private static bool TryParseConfigurations(string value, out string[] configurations, out string? error)
    {
        var values = SplitValues(value).ToArray();
        if (values.Length == 1 && values[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            configurations = ["Debug", "Release"];
            error = null;
            return true;
        }

        if (values.Length == 1 && values[0].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            configurations = [];
            error = null;
            return true;
        }

        var selected = new List<string>();
        foreach (var item in values)
        {
            var configuration = item.Equals("debug", StringComparison.OrdinalIgnoreCase) ? "Debug" :
                item.Equals("release", StringComparison.OrdinalIgnoreCase) ? "Release" : null;
            if (configuration is null)
            {
                configurations = [];
                error = $"Unknown configuration '{item}'. Use all, Debug, Release, or none.";
                return false;
            }

            if (!selected.Contains(configuration, StringComparer.OrdinalIgnoreCase))
                selected.Add(configuration);
        }

        configurations = selected.ToArray();
        error = null;
        return true;
    }

    private static bool TryAddSelections(
        string value,
        IReadOnlyCollection<string> supported,
        List<string> selected,
        string kind,
        out string? error)
    {
        foreach (var item in SplitValues(value))
        {
            if (item.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                selected.Clear();
                selected.Add("all");
                error = null;
                return true;
            }

            var canonical = supported.FirstOrDefault(candidate => candidate.Equals(item, StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
            {
                error = $"Unknown {kind} '{item}'.";
                return false;
            }

            if (selected.Contains("all", StringComparer.OrdinalIgnoreCase))
                selected.Clear();
            if (!selected.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                selected.Add(canonical);
        }

        error = null;
        return true;
    }

    private static IEnumerable<string> SplitValues(string value) =>
        value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool PrepareWorkspace(
        string root,
        Options options,
        out Module[] availableModules,
        out string? error)
    {
        var presentModules = Modules
            .Where(module => File.Exists(Path.Combine(root, module.Directory, module.Solution)))
            .ToArray();

        var allModulesRequested = options.Modules.Contains("all", StringComparer.OrdinalIgnoreCase);
        var explicitlyRequestedModules = allModulesRequested
            ? Modules
            : Modules.Where(module => options.Modules.Contains(module.Directory, StringComparer.OrdinalIgnoreCase)).ToArray();
        var requestedModules = options.BuildConfigurations.Length > 0
            ? ExpandBuildDependencies(explicitlyRequestedModules)
            : explicitlyRequestedModules;

        foreach (var module in requestedModules.Except(explicitlyRequestedModules))
            Console.WriteLine($"[include] {module.Directory} is required by the selected modules.");

        var missingRequestedModules = requestedModules.Except(presentModules).ToArray();
        if (!allModulesRequested && missingRequestedModules.Length > 0)
        {
            error = $"Requested module repositories are missing: {string.Join(", ", missingRequestedModules.Select(module => module.Directory))}.";
            availableModules = [];
            return false;
        }

        availableModules = requestedModules;

        if (options.BuildConfigurations.Length > 0)
        {
            var engine = Modules[0];
            var engineAvailable = presentModules.Contains(engine);
            if (!engineAvailable)
            {
                Console.WriteLine("[skip] Module builds require the missing repository 'RedBolt'.");
                availableModules = [];
                options.BuildConfigurations = [];
            }
            else
            {
                foreach (var module in missingRequestedModules)
                    Console.WriteLine($"[skip] Module repository '{module.Directory}' is not available.");

                availableModules = availableModules.Intersect(presentModules).ToArray();
            }
        }

        if (options.TestConfigurations.Length > 0)
        {
            var testProject = Path.Combine(root, "RedBolt.Tests", "RedBolt.Tests.csproj");
            var testRunner = Path.Combine(root, "RedBolt.Tests", "scripts", "run_tests_parallel.sh");
            var allDependenciesAvailable = Modules.All(module =>
                File.Exists(Path.Combine(root, module.Directory, module.Solution)));
            if (!File.Exists(testProject) || !File.Exists(testRunner) || !allDependenciesAvailable)
            {
                Console.WriteLine("[skip] Tests require RedBolt.Tests and all module repositories.");
                options.TestConfigurations = [];
            }
        }

        foreach (var command in new[] { "dotnet", "cmake", "ninja" })
        {
            if (options.BuildConfigurations.Length > 0 && !CommandExists(command))
            {
                error = $"Required command '{command}' is not available through PATH.";
                return false;
            }
        }

        if (options.TestConfigurations.Length > 0 && !CommandExists("dotnet"))
        {
            error = "Required command 'dotnet' is not available through PATH.";
            return false;
        }

        error = null;
        return true;
    }

    private static Module[] ExpandBuildDependencies(IReadOnlyCollection<Module> selected)
    {
        var enabled = selected.Select(module => module.Directory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Any(module => module.Directory != "RedBolt"))
            enabled.Add("RedBolt");

        bool added;
        do
        {
            added = false;
            foreach (var dependency in Modules.Where(module => enabled.Contains(module.Directory)).SelectMany(module => module.Dependencies))
                added |= enabled.Add(dependency);
        }
        while (added);

        return Modules.Where(module => enabled.Contains(module.Directory)).ToArray();
    }

    private static bool CommandExists(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator).Any(directory => File.Exists(Path.Combine(directory, command)));
    }

    private static void ConfigureEnvironment()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            dataHome = Path.Combine(home, ".local", "share");
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataHome);
        }

        var androidRoot = Path.Combine(dataHome, "redbolt-android");
        SetDefaultEnvironment("JAVA_HOME", Path.Combine(androidRoot, "jdk"));
        SetDefaultEnvironment("ANDROID_HOME", Path.Combine(androidRoot, "sdk"));
        SetDefaultEnvironment("ANDROID_SDK_ROOT", Environment.GetEnvironmentVariable("ANDROID_HOME")!);
        SetDefaultEnvironment("ANDROID_NDK_ROOT", Path.Combine(Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")!, "ndk", "27.2.12479018"));
        SetDefaultEnvironment("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        var localRoot = Path.Combine(home, ".local");
        PrependExistingPath(DiscoverLocalToolDirectories(localRoot));
    }

    private static IEnumerable<string> DiscoverLocalToolDirectories(string localRoot)
    {
        yield return Path.Combine(localRoot, "bin");

        var optRoot = Path.Combine(localRoot, "opt");
        if (Directory.Exists(optRoot))
        {
            var toolDirectories = Directory.EnumerateDirectories(optRoot)
                .Select(directory => Path.Combine(directory, "bin"))
                .Where(ContainsCiTool)
                .OrderDescending();
            foreach (var directory in toolDirectories)
                yield return directory;
        }

        yield return Path.Combine(localRoot, "mingw-w64", "usr", "bin");
    }

    private static bool ContainsCiTool(string directory)
    {
        string[] commands =
        [
            "cmake",
            "dotnet",
            "ninja",
            "wine",
            "x86_64-w64-mingw32-clang",
            "x86_64-w64-mingw32-gcc",
            "x86_64-w64-mingw32-windres"
        ];
        return Directory.Exists(directory) && commands.Any(command => File.Exists(Path.Combine(directory, command)));
    }

    private static void SetDefaultEnvironment(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            Environment.SetEnvironmentVariable(name, value);
    }

    private static void PrependExistingPath(IEnumerable<string> directories)
    {
        var existing = directories.Where(Directory.Exists).Distinct(StringComparer.Ordinal);
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, existing.Append(path)));
    }

    private static string GetScriptDirectory([CallerFilePath] string sourcePath = "") =>
        Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: ./ci.cs [options] [-- TEST_ARGUMENTS...]");
        Console.WriteLine();
        Console.WriteLine("With no options, clean-build every module for all platforms and architectures");
        Console.WriteLine("in Debug and Release, then build and run Debug and Release tests.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --incremental                 Do not clean before builds");
        Console.WriteLine("  --no-self-contained           Build framework-dependent outputs");
        Console.WriteLine("  --module VALUE                all or a module directory; repeatable (default: all)");
        Console.WriteLine("  --build-config VALUE          all, Debug, Release, or none (default: all)");
        Console.WriteLine("  --test-config VALUE           all, Debug, Release, or none (default: all)");
        Console.WriteLine("  --tests FILTER                Run tests matching a VSTest filter");
        Console.WriteLine("  --platform VALUE              all, linux, windows, android, or web");
        Console.WriteLine("  --architectures VALUE         all, x64, or arm64");
        Console.WriteLine("  -j, --jobs COUNT              C# and CMake jobs (default: 4)");
        Console.WriteLine("  --test-shards COUNT           Parallel test processes (default: 4)");
        Console.WriteLine("  --dry-run                     Print commands without executing them");
        Console.WriteLine("  -h, --help                    Show this help");
        Console.WriteLine();
        Console.WriteLine("Selection values can be comma-separated or supplied through repeated options.");
        Console.WriteLine("Arguments after '--' are forwarded to the parallel dotnet test runner.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ./ci.cs --incremental --module RedBolt.PhysicsJolt --build-config Debug --test-config none --platform linux --architectures x64");
        Console.WriteLine("  ./ci.cs --incremental --build-config none --test-config Debug --tests 'FullyQualifiedName~PhysicsJolt'");
        Console.WriteLine("  ./ci.cs --test-config Release --tests 'FullyQualifiedName~PhysicsJolt'");
        Console.WriteLine("  ./ci.cs --test-config none --platform linux,android --architectures x64");
    }

    private sealed record Module(string Directory, string Solution, params string[] Dependencies);

    private sealed class Options
    {
        public bool ShowHelp { get; set; }
        public bool Incremental { get; set; }
        public bool NoSelfContained { get; set; }
        public bool DryRun { get; set; }
        public string[] BuildConfigurations { get; set; } = ["Debug", "Release"];
        public string[] TestConfigurations { get; set; } = ["Debug", "Release"];
        public List<string> Modules { get; } = ["all"];
        public List<string> Platforms { get; } = ["all"];
        public List<string> Architectures { get; } = ["all"];
        public int Parallelization { get; set; } = 4;
        public int TestShards { get; set; } = 4;
        public List<string> TestArguments { get; } = [];
    }

    private sealed class PipelineRunner(
        string root,
        IReadOnlyList<Module> availableModules,
        Options options)
    {
        private readonly Stopwatch stopwatch = new();
        public async Task<int> RunAsync()
        {
            stopwatch.Start();
            Console.WriteLine("RedBolt CI pipeline");
            Console.WriteLine($"  clean={!options.Incremental} build={Format(options.BuildConfigurations)} test={Format(options.TestConfigurations)}");
            Console.WriteLine($"  modules={Format(availableModules.Select(module => module.Directory))}");
            Console.WriteLine($"  platforms={Format(options.Platforms)} architectures={Format(options.Architectures)} parallelization={options.Parallelization} test-shards={options.TestShards}");

            if (!options.Incremental && options.BuildConfigurations.Length > 0 && !CleanBuildOutputs())
                return 1;

            foreach (var configuration in options.BuildConfigurations)
            {
                foreach (var module in availableModules)
                {
                    if (await RunBuildCommandAsync(module, configuration) != 0)
                        return 1;
                }
            }

            foreach (var configuration in options.TestConfigurations)
            {
                if (await RunTestBuildCommandAsync(configuration) != 0)
                    return 1;
                if (await RunTestsAsync(configuration) != 0)
                    return 1;
            }

            Console.WriteLine($"{Environment.NewLine}RedBolt CI pipeline completed in {stopwatch.Elapsed.TotalSeconds:F1}s.");
            return 0;
        }

        private bool CleanBuildOutputs()
        {
            Console.WriteLine($"{Environment.NewLine}[clean] generated build outputs");
            foreach (var directory in availableModules.Select(module => Path.Combine(root, module.Directory, "build")))
            {
                Console.WriteLine($"  delete {directory}");
                if (options.DryRun || !Directory.Exists(directory))
                    continue;

                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"ci.cs: Cannot clean '{directory}': {exception.Message}");
                    return false;
                }
            }

            return true;
        }

        private Task<int> RunBuildCommandAsync(Module module, string configuration)
        {
            var arguments = new List<string>
            {
                "build",
                module.Solution,
                "--configuration", configuration,
                $"-m:{options.Parallelization}",
                $"-p:RedBoltParallelization={options.Parallelization}",
                "-p:RedBoltDisablePlatforms=all",
                $"-p:RedBoltEnablePlatforms={MsBuildList(options.Platforms)}",
                "-p:RedBoltDisableArchitectures=all",
                $"-p:RedBoltEnableArchitectures={MsBuildList(options.Architectures)}",
                "-p:RedBoltDisableModules=all",
                "-p:RedBoltEnableModules=all",
                "-p:RedBoltTestsEnabled=false",
            };
            if (options.NoSelfContained)
                arguments.Add("--no-self-contained");
            if (module.Directory != "RedBolt")
                arguments.Add("-p:RedBoltReuseCoreProjectReferences=true");
            if (module.Dependencies.Contains("RedBolt.Network", StringComparer.OrdinalIgnoreCase))
                arguments.Add("-p:RedBoltReuseNetworkProjectReferences=true");

            Console.WriteLine($"{Environment.NewLine}[build] {module.Directory} {configuration} ({Format(options.Platforms)}/{Format(options.Architectures)})");
            return RunProcessAsync(Path.Combine(root, module.Directory), "dotnet", arguments);
        }

        private Task<int> RunTestBuildCommandAsync(string configuration)
        {
            var arguments = new List<string>
            {
                "build",
                "RedBolt.Tests.csproj",
                "--configuration", configuration,
                $"-m:{options.Parallelization}",
                $"-p:RedBoltParallelization={options.Parallelization}",
                "-p:RedBoltDisablePlatforms=all",
                "-p:RedBoltEnablePlatforms=linux",
                "-p:RedBoltDisableArchitectures=all",
                "-p:RedBoltEnableArchitectures=x64",
                "-p:RedBoltDisableModules=all",
                "-p:RedBoltEnableModules=all",
                "-p:RedBoltKeepParentRuntimeAssemblies=true",
                "-p:BuildProjectReferences=false",
            };
            if (options.NoSelfContained)
                arguments.Add("--no-self-contained");

            Console.WriteLine($"{Environment.NewLine}[test-build] {configuration}");
            return RunProcessAsync(Path.Combine(root, "RedBolt.Tests"), "dotnet", arguments);
        }

        private Task<int> RunTestsAsync(string configuration)
        {
            var arguments = new List<string>
            {
                "--logger", "console;verbosity=minimal",
            };
            arguments.AddRange(options.TestArguments);

            var environment = new Dictionary<string, string?>
            {
                ["REDBOLT_TEST_CONFIGURATION"] = configuration,
                ["REDBOLT_TEST_SHARD_COUNT"] = options.TestShards.ToString(),
            };

            Console.WriteLine($"{Environment.NewLine}[test-run] {configuration} ({options.TestShards} isolated shards)");
            return RunProcessAsync(
                Path.Combine(root, "RedBolt.Tests"),
                Path.Combine("scripts", "run_tests_parallel.sh"),
                arguments,
                environment);
        }

        private async Task<int> RunProcessAsync(
            string workingDirectory,
            string fileName,
            IReadOnlyCollection<string> arguments,
            IReadOnlyDictionary<string, string?>? environment = null)
        {
            Console.WriteLine($"  in {workingDirectory}");
            Console.WriteLine($"  + {Quote(fileName)} {string.Join(' ', arguments.Select(Quote))}");
            if (options.DryRun)
                return 0;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                },
            };
            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);
            if (environment is not null)
            {
                foreach (var (name, value) in environment)
                    process.StartInfo.Environment[name] = value;
            }

            try
            {
                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Console.Error.WriteLine($"ci.cs: Cannot run '{fileName}': {exception.Message}");
                return 1;
            }
        }

        private static string MsBuildList(IEnumerable<string> values) =>
            string.Join("%2c", values);

        private static string Format(IEnumerable<string> values) => string.Join(',', values);

        private static string Quote(string value) =>
            value.Any(char.IsWhiteSpace) ? $"'{value.Replace("'", "'\\''")}'" : value;
    }
}
