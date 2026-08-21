#!/bin/sh
#if false
exec dotnet run --file "$0" -- "$@"
#endif

using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

return await MonorepoApp.RunAsync(args);

static class MonorepoApp
{
    private const string GitHubOrganization = "RedBoltFramework";

    private static readonly Repository[] Repositories =
    [
        new("RedBolt", "RedBolt", true),
        new("RedBolt.2D", "RedBolt.2D", true),
        new("RedBolt.Actions", "RedBolt.Actions", true),
        new("RedBolt.CSG", "RedBolt.CSG", true),
        new("RedBolt.Glow", "RedBolt.Glow", true),
        new("RedBolt.IK", "RedBolt.IK", true),
        new("RedBolt.Navigation", "RedBolt.Navigation", true),
        new("RedBolt.Network", "RedBolt.Network", true),
        new("RedBolt.Particles", "RedBolt.Particles", true),
        new("RedBolt.PhysicsBox2D", "RedBolt.PhysicsBox2D", true),
        new("RedBolt.PhysicsJolt", "RedBolt.PhysicsJolt", true),
        new("RedBolt.Tests", "RedBolt.Tests", false),
        new("RedBolt.RmlUI", "RedBolt.RmlUI", true),
        new("RedBolt.XR", "RedBolt.XR", true),
        new("RedBoltFramework.github.io", "RedBoltFramework.github.io", true),
        new("SharpCpp", Path.Combine("extern", "SharpCpp"), false),
        new("imgui", Path.Combine("extern", "imgui"), false),
    ];

    private static readonly WebModule[] WebModules =
    [
        new("RedBolt", "redbolt", "Core", "The engine core, native renderer, audio, editor, utilities, and foundational sample catalog."),
        new("RedBolt.2D", "2d", "Rendering", "Sprites, particle effects, Spriter animation, stretchable sprites, and tile maps for 2D worlds."),
        new("RedBolt.Actions", "actions", "Systems", "Composable, time-based actions for sequencing movement, animation, and gameplay behavior."),
        new("RedBolt.CSG", "csg", "Rendering", "Constructive solid geometry for building and combining procedural shapes at runtime."),
        new("RedBolt.Glow", "glow", "Rendering", "Lightmap UV generation, baking, runtime lightmaps, probes, and editor integration."),
        new("RedBolt.IK", "ik", "Simulation", "Runtime inverse-kinematics solvers, scene components, retargeting tools, and editor support."),
        new("RedBolt.Navigation", "navigation", "Simulation", "Recast and Detour navigation meshes, dynamic obstacles, pathfinding, and crowd simulation."),
        new("RedBolt.Network", "network", "Systems", "Networking, replication, HTTP, LAN discovery, and browser-native WebRTC transports."),
        new("RedBolt.Particles", "particles", "Rendering", "Programmable particle graphs and emitters for scalable real-time visual effects."),
        new("RedBolt.PhysicsBox2D", "physics-box2d", "Simulation", "Box2D 3 integration with rigid bodies, joints, constraints, ropes, and 2D platforming."),
        new("RedBolt.PhysicsJolt", "physics-jolt", "Simulation", "Jolt-powered 3D physics, character controllers, vehicles, raycasts, and network integration."),
        new("RedBolt.RmlUI", "rmlui", "Rendering", "RmlUi integration for rich HTML- and CSS-inspired game interfaces and editor extensions."),
        new("RedBolt.XR", "xr", "Systems", "OpenXR integration with native bindings, managed VR systems, action manifests, and cross-platform XR support."),
    ];

    private static readonly SampleBundle PublishedSampleBundle = new("all",
        ["RedBolt", "RedBolt.2D", "RedBolt.Actions", "RedBolt.CSG", "RedBolt.IK", "RedBolt.Navigation", "RedBolt.Network", "RedBolt.Particles",
            "RedBolt.PhysicsBox2D", "RedBolt.PhysicsJolt", "RedBolt.RmlUI"]);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        if (string.Equals(args[0], "pull", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 2 && args[1] is "-h" or "--help")
            {
                PrintPullHelp();
                return 0;
            }

            if (args.Length > 2 || args.Length == 2 && string.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("The pull subcommand accepts at most one non-empty ref.");
                PrintPullHelp();
                return 1;
            }

            return await PullAsync(args.Length == 2 ? args[1] : "master");
        }

        if (string.Equals(args[0], "generate-samples", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 2 && args[1] is "-h" or "--help")
            {
                PrintGenerateSamplesHelp();
                return 0;
            }

            if (!TryParseGenerateSamplesOptions(args[1..], out var generateOptions, out var generateError))
            {
                Console.Error.WriteLine(generateError);
                PrintGenerateSamplesHelp();
                return 1;
            }

            return GenerateSamples(generateOptions);
        }

        if (string.Equals(args[0], "publish-samples", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 2 && args[1] is "-h" or "--help")
            {
                PrintPublishSamplesHelp();
                return 0;
            }

            if (!TryParsePublishOptions(args[1..], out var publishOptions, out var publishError))
            {
                Console.Error.WriteLine(publishError);
                PrintPublishSamplesHelp();
                return 1;
            }

            return await PublishSamplesAsync(publishOptions);
        }

        if (!string.Equals(args[0], "init", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown subcommand '{args[0]}'.");
            PrintHelp();
            return 1;
        }

        if (args.Length == 2 && args[1] is "-h" or "--help")
        {
            PrintInitHelp();
            return 0;
        }

        if (!TrySelectRepositories(args[1..], out var selected, out var error))
        {
            Console.Error.WriteLine(error);
            PrintInitHelp();
            return 1;
        }

        return await InitializeAsync(selected);
    }

    private static bool TrySelectRepositories(
        string[] args,
        out IReadOnlyList<Repository> selected,
        out string? error)
    {
        var enabled = Repositories
            .Where(repository => repository.EnabledByDefault)
            .Select(repository => repository.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length;)
        {
            var argument = args[index];
            var separator = argument.IndexOf('=');
            var option = separator >= 0 ? argument[..separator] : argument;
            var values = new List<string>();

            if (option is not "--with-modules")
            {
                selected = [];
                error = $"Unknown option '{argument}'.";
                return false;
            }

            if (separator >= 0)
            {
                values.AddRange(SplitModuleNames(argument[(separator + 1)..]));
                index++;
            }
            else
            {
                index++;
                while (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
                {
                    values.AddRange(SplitModuleNames(args[index]));
                    index++;
                }
            }

            if (values.Count == 0)
            {
                selected = [];
                error = $"Option '{option}' requires at least one module name.";
                return false;
            }

            foreach (var value in values)
            {
                if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
                {
                    enabled.UnionWith(Repositories.Select(repository => repository.Name));
                    continue;
                }

                var repository = Repositories.FirstOrDefault(
                    candidate => string.Equals(candidate.Name, value, StringComparison.OrdinalIgnoreCase));

                if (repository is null)
                {
                    selected = [];
                    error = $"Unknown module '{value}'.";
                    return false;
                }

                enabled.Add(repository.Name);
            }
        }

        selected = Repositories.Where(repository => enabled.Contains(repository.Name)).ToArray();
        error = null;
        return true;
    }

    private static IEnumerable<string> SplitModuleNames(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryParseGenerateSamplesOptions(
        string[] args,
        out GenerateSamplesOptions options,
        out string? error)
    {
        options = new GenerateSamplesOptions();
        error = null;
        for (var index = 0; index < args.Length;)
        {
            var argument = args[index];
            var separator = argument.IndexOf('=');
            var name = separator >= 0 ? argument[..separator] : argument;
            var value = separator >= 0 ? argument[(separator + 1)..] : null;

            if (name == "--check")
            {
                if (value is not null)
                {
                    error = "Option '--check' does not accept a value.";
                    return false;
                }
                options.Check = true;
                index++;
                continue;
            }

            if (name is "--module" or "--modules")
            {
                var values = new List<string>();
                if (value is not null)
                {
                    values.AddRange(SplitModuleNames(value));
                    index++;
                }
                else
                {
                    index++;
                    while (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
                    {
                        values.AddRange(SplitModuleNames(args[index]));
                        index++;
                    }
                }

                if (values.Count == 0)
                {
                    error = $"Option '{name}' requires at least one module name.";
                    return false;
                }
                options.Modules.AddRange(values);
                continue;
            }

            if (name is not ("--output" or "--name"))
            {
                error = $"Unknown generate-samples option '{argument}'.";
                return false;
            }
            if (value is null && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                value = args[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"Option '{name}' requires a value.";
                return false;
            }

            if (name == "--output")
                options.Output = value;
            else
                options.Name = value;
            index++;
        }

        if (options.Name is not null && options.Output is null)
        {
            error = "Option '--name' requires '--output'.";
            return false;
        }
        return true;
    }

    private static int GenerateSamples(GenerateSamplesOptions options)
    {
        var root = GetScriptDirectory();
        try
        {
            var available = LoadSampleModules(root);
            var selected = SelectSampleModules(available, options.Modules);
            var templates = LoadSampleTemplates(root);
            VerifyGeneratedRegionReplacement();
            VerifySampleOrdering();
            var stale = false;

            if (options.Output is null)
            {
                foreach (var module in selected)
                {
                    var output = Path.Combine(module.Root, "src", "RedBolt", "Sample");
                    GenerateSampleHost(root, output, module.Definition.Name, [module], aggregate: false,
                        options.Check, templates, ref stale);
                }
            }
            else
            {
                var output = Path.GetFullPath(options.Output, root);
                EnsureOutputIsInWorkspace(root, output);
                var name = options.Name ?? Path.GetFileName(output.TrimEnd(Path.DirectorySeparatorChar));
                GenerateSampleHost(root, output, name, selected, aggregate: true,
                    options.Check, templates, ref stale);
            }

            if (options.Check && stale)
            {
                Console.Error.WriteLine("Generated sample hosts are stale. Run generate-samples without '--check'.");
                return 1;
            }
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"Cannot generate sample hosts: {exception.Message}");
            return 1;
        }
    }

    private static SampleTemplates LoadSampleTemplates(string root)
    {
        var sampleRoot = Path.Combine(root, "RedBolt", "src", "RedBolt", "Sample");
        return new SampleTemplates(
            File.ReadAllText(Path.Combine(sampleRoot, "Web", "Program.cs")),
            File.ReadAllText(Path.Combine(sampleRoot, "Web", "RedBolt.Sample.Web.csproj")),
            File.ReadAllText(Path.Combine(sampleRoot, "Web", "wwwroot", "index.html")),
            File.ReadAllText(Path.Combine(sampleRoot, "Android", "Program.cs")),
            File.ReadAllText(Path.Combine(sampleRoot, "Android", "RedBolt.Sample.Android.csproj")));
    }

    private static IReadOnlyList<SampleModule> LoadSampleModules(string root)
    {
        var modules = new List<SampleModule>();
        foreach (var repository in Repositories)
        {
            var repositoryRoot = Path.Combine(root, repository.RelativePath);
            var catalogPath = Path.Combine(repositoryRoot, "src", "RedBolt", "Sample", "SampleCatalog.json");
            if (!File.Exists(catalogPath))
                continue;

            var definition = JsonSerializer.Deserialize(File.ReadAllText(catalogPath), SampleJsonContext.Default.SampleModuleDefinition)
                ?? throw new InvalidDataException($"Empty sample catalog: {catalogPath}");
            ValidateSampleModule(repository.Name, repositoryRoot, catalogPath, definition);
            modules.Add(new SampleModule(repositoryRoot, catalogPath, definition));
        }

        if (modules.Count == 0)
            throw new InvalidDataException("No module sample catalogs were found.");
        return modules;
    }

    private static void ValidateSampleModule(
        string repositoryName,
        string repositoryRoot,
        string catalogPath,
        SampleModuleDefinition definition)
    {
        if (!definition.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Catalog name '{definition.Name}' does not match repository '{repositoryName}': {catalogPath}");
        if (!definition.Web && !definition.Android)
            throw new InvalidDataException($"Catalog supports neither web nor Android: {catalogPath}");
        if (definition.Android && string.IsNullOrWhiteSpace(definition.ApplicationId))
            throw new InvalidDataException($"Android catalog has no application id: {catalogPath}");
        if (definition.Samples.Count == 0)
            throw new InvalidDataException($"Catalog has no samples: {catalogPath}");
        if (definition.Samples.Select(sample => sample.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definition.Samples.Count)
            throw new InvalidDataException($"Catalog has duplicate sample identifiers: {catalogPath}");
        if (definition.Samples.Select(sample => sample.ApplicationType).Distinct(StringComparer.Ordinal).Count() != definition.Samples.Count)
            throw new InvalidDataException($"Catalog has duplicate application types: {catalogPath}");

        foreach (var sample in definition.Samples)
        {
            if (string.IsNullOrWhiteSpace(sample.Id) || string.IsNullOrWhiteSpace(sample.Name)
                || string.IsNullOrWhiteSpace(sample.ApplicationType) || string.IsNullOrWhiteSpace(sample.Source))
                throw new InvalidDataException($"Catalog contains an incomplete sample entry: {catalogPath}");
            if (!Regex.IsMatch(sample.Id, "^[a-z0-9]+(?:-[a-z0-9]+)*$")
                || !Regex.IsMatch(sample.ApplicationType, "^[A-Za-z_][A-Za-z0-9_.]*$"))
                throw new InvalidDataException($"Catalog contains an invalid sample id or application type: {catalogPath}");
            var sampleRoot = Path.Combine(repositoryRoot, "src", "RedBolt", "Sample");
            var source = Path.GetFullPath(sample.Source, sampleRoot);
            EnsurePathIsWithin(sampleRoot, source, "Sample source");
            if (!Directory.Exists(source))
                throw new InvalidDataException($"Sample source does not exist: {source}");
            foreach (var platform in sample.Platforms)
            {
                if (platform is not ("web" or "android"))
                    throw new InvalidDataException($"Unknown sample platform '{platform}': {catalogPath}");
            }
        }

        foreach (var sourcePattern in definition.AdditionalSources)
        {
            if (Path.IsPathRooted(sourcePattern) || sourcePattern.Split('/', '\\').Contains(".."))
                throw new InvalidDataException($"Additional sample source must stay inside the sample directory: {sourcePattern}");
        }

        foreach (var reference in definition.ProjectReferences.Concat(definition.PlatformIndependentProjectReferences))
        {
            var path = Path.GetFullPath(reference, repositoryRoot);
            if (!File.Exists(path))
                throw new InvalidDataException($"Sample project reference does not exist: {path}");
        }
        if (definition.WebExtension is not null && !File.Exists(Path.GetFullPath(definition.WebExtension, repositoryRoot)))
            throw new InvalidDataException($"Web extension does not exist: {definition.WebExtension}");
        if (definition.AndroidManifest is not null && !File.Exists(Path.GetFullPath(definition.AndroidManifest, repositoryRoot)))
            throw new InvalidDataException($"Android manifest does not exist: {definition.AndroidManifest}");
    }

    private static IReadOnlyList<SampleModule> SelectSampleModules(
        IReadOnlyList<SampleModule> available,
        IReadOnlyList<string> requested)
    {
        if (requested.Count == 0 || requested.Any(name => name.Equals("all", StringComparison.OrdinalIgnoreCase)))
            return available;

        var selected = new List<SampleModule>();
        foreach (var name in requested)
        {
            var module = available.FirstOrDefault(candidate => candidate.Definition.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Unknown sample module '{name}'.");
            if (!selected.Contains(module))
                selected.Add(module);
        }
        return selected;
    }

    private static void EnsureOutputIsInWorkspace(string root, string output)
    {
        EnsurePathIsWithin(root, output, "Sample output");
    }

    private static void EnsurePathIsWithin(string root, string path, string description)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
            throw new InvalidDataException($"{description} must stay inside '{root}': {path}");
    }

    private static void GenerateSampleHost(
        string root,
        string output,
        string name,
        IReadOnlyList<SampleModule> modules,
        bool aggregate,
        bool check,
        SampleTemplates templates,
        ref bool stale)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(character => !char.IsLetterOrDigit(character)
                && character is not ('-' or '_' or '.')))
            throw new InvalidDataException($"Invalid sample host name '{name}'.");
        var nativeOwner = aggregate ? null : FindNativeOwner(modules);
        var webModules = modules.Where(module => module.Definition.Web).ToArray();
        var androidModules = modules.Where(module => module.Definition.Android).ToArray();
        if (webModules.Length == 0 && androidModules.Length == 0)
            throw new InvalidDataException("The selected modules have no web or Android samples.");

        if (aggregate)
            GenerateAggregateInfrastructure(root, output, name, check, ref stale);
        if (webModules.Length > 0)
            GenerateWebHost(root, output, name, webModules, nativeOwner, aggregate, check, templates, ref stale);
        if (androidModules.Length > 0)
            GenerateAndroidHost(root, output, name, androidModules, nativeOwner, aggregate, check, templates, ref stale);

        Console.WriteLine($"{(check ? "Checked" : "Generated")} {name}: {string.Join(", ", modules.Select(module => module.Definition.Name))}");
    }

    private static SampleModule? FindNativeOwner(IReadOnlyList<SampleModule> modules)
    {
        var owners = FindNativeOwners(modules);
        if (owners.Length == 0)
            return null;
        if (owners.Length != 1)
        {
            var names = string.Join(", ", owners.Select(module => module.Definition.Name));
            throw new InvalidDataException($"Selected modules require incompatible native sample hosts: {names}.");
        }
        return owners[0];
    }

    private static SampleModule[] FindNativeOwners(IReadOnlyList<SampleModule> modules)
    {
        var nativeModules = modules.Where(module => module.Definition.Native).ToArray();
        var dependencies = nativeModules.SelectMany(module => module.Definition.NativeDependencies)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return nativeModules.Where(module => !dependencies.Contains(module.Definition.Name)).ToArray();
    }

    private static void GenerateAggregateInfrastructure(
        string root,
        string output,
        string name,
        bool check,
        ref bool stale)
    {
        var coreRoot = Path.Combine(root, "RedBolt");
        var propsSource = Path.Combine(coreRoot, "src", "Directory.Build.props");
        var rootRelative = ToProjectPath(Path.GetRelativePath(output, coreRoot));
        var buildRelative = ToProjectPath(Path.GetRelativePath(output, Path.Combine(root, "build")));
        var propsRelative = ToProjectPath(Path.GetRelativePath(output, propsSource));
        var props = $"""
            <!-- Generated by ./monorepo.cs generate-samples. -->
            <Project>
              <PropertyGroup>
                <RedBoltRoot>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory){rootRelative}'))</RedBoltRoot>
                <RedBoltExpectedBuildRoot>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory){buildRelative}'))</RedBoltExpectedBuildRoot>
                <RedBoltRestoreProjectName>Samples/{Xml(ToIdentifier(name))}/$(MSBuildProjectName)</RedBoltRestoreProjectName>
              </PropertyGroup>
              <Import Project="$(MSBuildThisFileDirectory){propsRelative}" />
              <Import Project="$(RedBoltRoot)/eng/RedBolt.ModuleBuild/Sample.props" />
            </Project>
            """ + Environment.NewLine;
        var targets = """
            <!-- Generated by ./monorepo.cs generate-samples. -->
            <Project>
              <Import Project="$(RedBoltRoot)/eng/RedBolt.ModuleBuild/Sample.targets" />
              <ItemGroup>
                <ProjectReference Update="@(ProjectReference)">
                  <AdditionalProperties>%(ProjectReference.AdditionalProperties);RedBoltBuildRoot=$(RedBoltBuildRoot);RedBoltExpectedBuildRoot=$(RedBoltExpectedBuildRoot)</AdditionalProperties>
                </ProjectReference>
              </ItemGroup>
              <Target Name="RedBoltConfigureAggregateProjectReferences"
                      AfterTargets="_SplitProjectReferencesByFileExistence"
                      BeforeTargets="_GetProjectReferenceTargetFrameworkProperties;ResolveProjectReferences">
                <ItemGroup>
                  <_MSBuildProjectReferenceExistent Update="@(_MSBuildProjectReferenceExistent)">
                    <SetConfiguration>%(_MSBuildProjectReferenceExistent.SetConfiguration);RedBoltBuildRoot=$(RedBoltBuildRoot);RedBoltExpectedBuildRoot=$(RedBoltExpectedBuildRoot)</SetConfiguration>
                  </_MSBuildProjectReferenceExistent>
                </ItemGroup>
              </Target>
              <Target Name="RedBoltConfigureAggregateRestoreGraph"
                      BeforeTargets="_FilterRestoreGraphProjectInputItems;_GenerateRestoreProjectPathItemsPerFramework;_GenerateProjectRestoreGraphPerFramework">
                <PropertyGroup>
                  <_GenerateRestoreGraphProjectEntryInputProperties>$(_GenerateRestoreGraphProjectEntryInputProperties);RedBoltBuildRoot=$(RedBoltBuildRoot);RedBoltExpectedBuildRoot=$(RedBoltExpectedBuildRoot)</_GenerateRestoreGraphProjectEntryInputProperties>
                </PropertyGroup>
              </Target>
            </Project>
            """ + Environment.NewLine;
        WriteGeneratedFile(Path.Combine(output, "Directory.Build.props"), props, check, ref stale);
        WriteGeneratedFile(Path.Combine(output, "Directory.Build.targets"), targets, check, ref stale);
    }

    private static void GenerateWebHost(
        string root,
        string output,
        string name,
        IReadOnlyList<SampleModule> modules,
        SampleModule? nativeOwner,
        bool aggregate,
        bool check,
        SampleTemplates templates,
        ref bool stale)
    {
        var directory = Path.Combine(output, "Web");
        var assemblyName = aggregate ? $"RedBolt.Samples.{ToIdentifier(name)}.Web" : $"{modules[0].Definition.Name}.Sample.Web";
        const string projectName = "RedBolt.Sample.Web.csproj";
        var project = GenerateWebProject(templates.WebProject, root, directory, name, assemblyName, modules, nativeOwner, aggregate);
        var program = GenerateWebProgram(templates.WebProgram, modules);
        var index = GenerateWebIndex(templates.WebIndex, aggregate ? $"RedBolt {name}" : modules[0].Definition.Name, modules, aggregate);

        WriteGeneratedFile(Path.Combine(directory, projectName), project, check, ref stale);
        WriteGeneratedFile(Path.Combine(directory, "Program.cs"), program, check, ref stale);
        WriteGeneratedFile(Path.Combine(directory, "wwwroot", "index.html"), index, check, ref stale);
    }

    private static string GenerateWebProject(
        string template,
        string root,
        string projectDirectory,
        string name,
        string assemblyName,
        IReadOnlyList<SampleModule> modules,
        SampleModule? nativeOwner,
        bool aggregate)
    {
        var properties = new StringBuilder();
        properties.AppendLine($"    <AssemblyName>{Xml(assemblyName)}</AssemblyName>");
        properties.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
        if (aggregate)
        {
            properties.AppendLine($"    <RedBoltWebOutputDir>$(RedBoltExpectedBuildRoot)/bin/$(Configuration)/browser-wasm/web/{Xml(name)}</RedBoltWebOutputDir>");
        }
        if (nativeOwner?.Definition.WebNative == true)
            properties.AppendLine($"    <RedBoltExternalWebArchive>$(RedBoltNativeBuildDir)/lib/Release/wasm/{Xml(nativeOwner.Definition.Name)}.Native.Web.a</RedBoltExternalWebArchive>");

        var pinvokeModules = modules.SelectMany(module => module.Definition.WebPInvokeModules)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (pinvokeModules.Length > 0)
            properties.AppendLine($"    <RedBoltExternalWebPInvokeModules>{Xml(string.Join(';', pinvokeModules))}</RedBoltExternalWebPInvokeModules>");
        var extensions = modules.Where(module => module.Definition.WebExtension is not null)
            .Select(module => Path.GetFullPath(module.Definition.WebExtension!, module.Root))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (extensions.Length > 1)
            throw new InvalidDataException($"Selected modules require different web extension scripts: {string.Join(", ", extensions)}");
        if (extensions.Length == 1)
            properties.AppendLine($"    <RedBoltWebExtensionScript>{Xml(RelativeProjectPath(projectDirectory, extensions[0]))}</RedBoltWebExtensionScript>");

        var items = new StringBuilder();
        items.AppendLine("    <Compile Include=\"Program.cs\" />");
        AppendProjectItems(items, projectDirectory, modules, web: true);
        if (aggregate)
        {
            foreach (var module in FindNativeOwners(modules).Where(module => module.Definition.WebNative))
            {
                var sampleProject = Path.Combine(module.Root, "src", "RedBolt", "Sample", "Web", "RedBolt.Sample.Web.csproj");
                var bindingProjects = Path.Combine(module.Root, "src", "RedBolt", "Native", "**", "*.csproj");
                var archive = $"$(RedBoltBuildRoot)/native/{module.Definition.Name}/browser-wasm/lib/Release/wasm/{module.Definition.Name}.Native.Web.a";
                items.AppendLine($"    <RedBoltAggregateWebNativeProject Include=\"{Xml(RelativeProjectPath(projectDirectory, sampleProject))}\" />");
                items.AppendLine($"    <ProjectReference Include=\"{Xml(RelativeProjectPath(projectDirectory, bindingProjects))}\" />");
                items.AppendLine($"    <NativeFileReference Include=\"{Xml(archive)}\" />");
            }
        }

        var import = $"  <Import Project=\"{Xml(RelativeProjectPath(projectDirectory, Path.Combine(root, "RedBolt", "eng", "RedBolt.ModuleBuild", "WebSample.targets")))}\" />";
        var targets = new StringBuilder();
        if (aggregate)
        {
            targets.AppendLine("  <Target Name=\"RedBoltBuildAggregateWebNativeArchives\" BeforeTargets=\"ResolveReferences\" Condition=\"'@(RedBoltAggregateWebNativeProject)' != ''\">");
            targets.AppendLine("    <MSBuild Projects=\"@(RedBoltAggregateWebNativeProject)\" Targets=\"Restore;RedBoltBuildWebNativeArchive\" BuildInParallel=\"false\"");
            targets.AppendLine("             Properties=\"Configuration=$(Configuration);RedBoltBuildRoot=$(RedBoltBuildRoot);RedBoltExpectedBuildRoot=$(RedBoltExpectedBuildRoot);RedBoltParallelization=$(RedBoltParallelization);RedBoltPlatform=browser-wasm;RuntimeIdentifier=browser-wasm;MSBuildEnableWorkloadResolver=false\" />");
            targets.AppendLine("  </Target>");
        }

        return ReplaceGeneratedRegion(
            ReplaceGeneratedRegion(
                ReplaceGeneratedRegion(
                    ReplaceGeneratedRegion(template, "web-project-properties", properties.ToString(), GeneratedComment.Xml),
                    "web-project-items", items.ToString(), GeneratedComment.Xml),
                "web-project-import", import, GeneratedComment.Xml),
            "web-project-targets", targets.ToString(), GeneratedComment.Xml);
    }

    private static void GenerateAndroidHost(
        string root,
        string output,
        string name,
        IReadOnlyList<SampleModule> modules,
        SampleModule? nativeOwner,
        bool aggregate,
        bool check,
        SampleTemplates templates,
        ref bool stale)
    {
        var directory = Path.Combine(output, "Android");
        var assemblyName = aggregate ? $"RedBolt.Samples.{ToIdentifier(name)}.Android" : $"{modules[0].Definition.Name}.Sample.Android";
        var applicationId = aggregate
            ? $"org.redbolt.samples.{ToIdentifier(name).ToLowerInvariant()}"
            : modules[0].Definition.ApplicationId;
        const string projectName = "RedBolt.Sample.Android.csproj";
        var manifests = modules.Where(module => module.Definition.AndroidManifest is not null)
            .Select(module => Path.GetFullPath(module.Definition.AndroidManifest!, module.Root))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (manifests.Length > 1)
            throw new InvalidDataException($"Selected modules require different Android manifests: {string.Join(", ", manifests)}");

        var project = GenerateAndroidProject(templates.AndroidProject, root, directory, assemblyName, applicationId, modules, nativeOwner,
            customManifest: manifests.Length == 1);
        var program = GenerateAndroidProgram(templates.AndroidProgram, modules, aggregate);
        WriteGeneratedFile(Path.Combine(directory, projectName), project, check, ref stale);
        WriteGeneratedFile(Path.Combine(directory, "Program.cs"), program, check, ref stale);
        if (manifests.Length == 1)
            WriteGeneratedFile(Path.Combine(directory, "AndroidManifest.xml"), File.ReadAllText(manifests[0]), check, ref stale);
    }

    private static string GenerateAndroidProject(
        string template,
        string root,
        string projectDirectory,
        string assemblyName,
        string applicationId,
        IReadOnlyList<SampleModule> modules,
        SampleModule? nativeOwner,
        bool customManifest)
    {
        var properties = new StringBuilder();
        properties.AppendLine($"    <AssemblyName>{Xml(assemblyName)}</AssemblyName>");
        properties.AppendLine($"    <ApplicationId>{Xml(applicationId)}</ApplicationId>");
        properties.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
        if (nativeOwner is not null)
            properties.AppendLine("    <RedBoltExternalAndroidNativeOutput>$(RedBoltNativeOutputRoot)</RedBoltExternalAndroidNativeOutput>");
        if (customManifest)
            properties.AppendLine("    <AndroidManifest>AndroidManifest.xml</AndroidManifest>");

        var items = new StringBuilder();
        items.AppendLine("    <Compile Include=\"Program.cs\" />");
        AppendProjectItems(items, projectDirectory, modules, web: false);
        if (nativeOwner is null)
        {
            foreach (var module in FindNativeOwners(modules))
            {
                var sampleProject = Path.Combine(module.Root, "src", "RedBolt", "Sample", "Android", "RedBolt.Sample.Android.csproj");
                items.AppendLine($"    <RedBoltAggregateAndroidNativeProject Include=\"{Xml(RelativeProjectPath(projectDirectory, sampleProject))}\" />");
            }
        }

        var import = $"  <Import Project=\"{Xml(RelativeProjectPath(projectDirectory, Path.Combine(root, "RedBolt", "eng", "RedBolt.ModuleBuild", "AndroidSample.targets")))}\" />";
        var targets = new StringBuilder();

        if (nativeOwner is null)
        {
            targets.AppendLine("  <Target Name=\"RedBoltBuildAggregateAndroidNativeModules\" BeforeTargets=\"RedBoltPrepareAndroidRuntime\" Condition=\"'@(RedBoltAggregateAndroidNativeProject)' != ''\">");
            targets.AppendLine("    <MSBuild Projects=\"@(RedBoltAggregateAndroidNativeProject)\" Targets=\"Restore;RedBoltPrepareAndroidRuntime\" BuildInParallel=\"false\"");
            targets.AppendLine("             Properties=\"Configuration=$(Configuration);RedBoltBuildRoot=$(RedBoltBuildRoot);RedBoltExpectedBuildRoot=$(RedBoltExpectedBuildRoot);RedBoltParallelization=$(RedBoltParallelization);RedBoltPlatform=$(RedBoltPlatform);RuntimeIdentifier=$(RuntimeIdentifier);RedBoltBuildAllAndroidPackages=false;MSBuildEnableWorkloadResolver=false\" />");
            targets.AppendLine("  </Target>");
        }

        var nativeLibraries = modules.SelectMany(module => module.Definition.AndroidNativeLibraries)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (nativeLibraries.Length > 0)
        {
            targets.AppendLine("  <Target Name=\"RedBoltCollectAdditionalAndroidSampleLibraries\" BeforeTargets=\"_BuildApkEmbed;_BuildApkFastDev\" DependsOnTargets=\"RedBoltPrepareAndroidRuntime\">");
            targets.AppendLine("    <ItemGroup>");
            foreach (var library in nativeLibraries)
                targets.AppendLine($"      <AndroidNativeLibrary Include=\"{Xml(library)}\" />");
            targets.AppendLine("    </ItemGroup>");
            targets.AppendLine("  </Target>");
        }

        return ReplaceGeneratedRegion(
            ReplaceGeneratedRegion(
                ReplaceGeneratedRegion(
                    ReplaceGeneratedRegion(template, "android-project-properties", properties.ToString(), GeneratedComment.Xml),
                    "android-project-items", items.ToString(), GeneratedComment.Xml),
                "android-project-import", import, GeneratedComment.Xml),
            "android-project-targets", targets.ToString(), GeneratedComment.Xml);
    }

    private static void AppendProjectItems(
        StringBuilder builder,
        string projectDirectory,
        IReadOnlyList<SampleModule> modules,
        bool web)
    {
        var references = modules.SelectMany(module => module.Definition.ProjectReferences
                .Select(path => (Path: Path.GetFullPath(path, module.Root), PlatformIndependent: false))
                .Concat(module.Definition.PlatformIndependentProjectReferences
                    .Select(path => (Path: Path.GetFullPath(path, module.Root), PlatformIndependent: true))))
            .GroupBy(reference => reference.Path, StringComparer.Ordinal)
            .Select(group => (Path: group.Key, PlatformIndependent: group.Any(reference => reference.PlatformIndependent)));
        foreach (var reference in references)
        {
            var metadata = reference.PlatformIndependent
                ? " GlobalPropertiesToRemove=\"RedBoltPlatform;RedBoltTargetFramework;RuntimeIdentifier;RuntimeIdentifiers;SelfContained;PublishSelfContained\""
                : "";
            builder.AppendLine($"    <ProjectReference Include=\"{Xml(RelativeProjectPath(projectDirectory, reference.Path))}\"{metadata} />");
        }

        foreach (var module in modules)
        {
            var moduleName = ToIdentifier(module.Definition.Name);
            foreach (var sample in module.Definition.Samples.Where(sample => SupportsPlatform(sample, web)))
            {
                var source = Path.Combine(module.Root, "src", "RedBolt", "Sample", sample.Source, "**", "*.cs");
                builder.AppendLine($"    <Compile Include=\"{Xml(RelativeProjectPath(projectDirectory, source))}\" Link=\"Samples/{moduleName}/{Xml(sample.Source)}/%(RecursiveDir)%(Filename)%(Extension)\" />");
            }
            foreach (var sourcePattern in module.Definition.AdditionalSources)
            {
                var source = Path.Combine(module.Root, "src", "RedBolt", "Sample", sourcePattern);
                builder.AppendLine($"    <Compile Include=\"{Xml(RelativeProjectPath(projectDirectory, source))}\" Link=\"Samples/{moduleName}/%(RecursiveDir)%(Filename)%(Extension)\" />");
            }
        }

        if (web)
        {
            foreach (var package in GetWebResourcePackages(modules))
            {
                builder.AppendLine($"    <RedBoltWebResourcePackage Include=\"{Xml(package.Name)}\" SourceDirectory=\"{Xml(RelativeProjectPath(projectDirectory, package.Directory))}\" />");
            }
            foreach (var assembly in modules.SelectMany(module => module.Definition.TrimmerRootAssemblies).Distinct(StringComparer.Ordinal))
                builder.AppendLine($"    <TrimmerRootAssembly Include=\"{Xml(assembly)}\" />");
        }
        else
        {
            foreach (var resource in modules.SelectMany(module => module.Definition.CoreData
                         .Select(path => Path.GetFullPath(path, module.Root))).Distinct(StringComparer.Ordinal))
                builder.AppendLine($"    <AndroidAsset Include=\"{Xml(RelativeProjectPath(projectDirectory, resource))}\" Link=\"CoreData/%(RecursiveDir)%(Filename)%(Extension)\" />");
            foreach (var resource in modules.SelectMany(module => module.Definition.Data
                         .Select(path => Path.GetFullPath(path, module.Root))).Distinct(StringComparer.Ordinal))
                builder.AppendLine($"    <AndroidAsset Include=\"{Xml(RelativeProjectPath(projectDirectory, resource))}\" Link=\"Data/%(RecursiveDir)%(Filename)%(Extension)\" />");
        }
    }

    private static bool SupportsPlatform(SampleDefinition sample, bool web) =>
        sample.Platforms.Count == 0 || sample.Platforms.Contains(web ? "web" : "android", StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<(SampleModule Module, SampleDefinition Sample)> GetSamples(
        IReadOnlyList<SampleModule> modules,
        bool web) =>
        modules.SelectMany(module => module.Definition.Samples
                .Where(sample => SupportsPlatform(sample, web))
                .Select(sample => (Module: module, Sample: sample)))
            .OrderBy(entry => entry.Sample.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Module.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Sample.Id, StringComparer.OrdinalIgnoreCase);

    private static void VerifySampleOrdering()
    {
        var definition = new SampleModuleDefinition
        {
            Name = "Ordering",
            Samples =
            [
                new SampleDefinition { Id = "zulu", Name = "Zulu" },
                new SampleDefinition { Id = "alpha", Name = "alpha" },
            ],
        };
        var module = new SampleModule("", "", definition);
        var names = GetSamples([module], web: true).Select(entry => entry.Sample.Name).ToArray();
        if (!names.SequenceEqual(["alpha", "Zulu"], StringComparer.Ordinal))
            throw new InvalidDataException("Sample ordering regression detected.");
    }

    private static string GenerateWebProgram(string template, IReadOnlyList<SampleModule> modules)
    {
        var resourcePaths = new[] { "RedBolt.CoreData", "RedBolt.Data" }
            .Concat(GetWebResourcePackages(modules).Select(package => package.Name));
        var resourcePath = $"    internal const string ResourcePaths = \"{CSharp(string.Join(';', resourcePaths))}\";";
        var factory = new StringBuilder();
        foreach (var (_, sample) in GetSamples(modules, web: true))
            factory.AppendLine($"        \"{CSharp(sample.ApplicationType)}\" => new global::{sample.ApplicationType}(),");

        return ReplaceGeneratedRegion(
            ReplaceGeneratedRegion(template, "web-resource-paths", resourcePath, GeneratedComment.CSharp),
            "web-sample-factory", factory.ToString(), GeneratedComment.CSharp);
    }

    private static IReadOnlyList<WebResourcePackage> GetWebResourcePackages(IReadOnlyList<SampleModule> modules)
    {
        var packages = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            var packageName = module.Definition.ResourcePackageName ?? module.Definition.Name;
            AddWebResourcePackages(packages, module, module.Definition.CoreData, $"{packageName}.CoreData");
            AddWebResourcePackages(packages, module, module.Definition.Data, $"{packageName}.Data");
        }

        return packages.Select(package => new WebResourcePackage(package.Key, package.Value)).ToArray();
    }

    private static void AddWebResourcePackages(
        Dictionary<string, string> packages,
        SampleModule module,
        IReadOnlyList<string> resources,
        string packageName)
    {
        foreach (var resource in resources)
        {
            const string recursiveGlob = "/**/*";
            if (!resource.EndsWith(recursiveGlob, StringComparison.Ordinal))
                throw new InvalidDataException($"Web resource '{resource}' must end with '{recursiveGlob}'.");

            var directory = Path.GetFullPath(resource[..^recursiveGlob.Length], module.Root);
            if (packages.TryGetValue(packageName, out var existingDirectory))
            {
                if (!existingDirectory.Equals(directory, StringComparison.Ordinal))
                    throw new InvalidDataException($"Web resource package '{packageName}' has more than one source directory.");
                continue;
            }

            packages.Add(packageName, directory);
        }
    }

    private static string GenerateAndroidProgram(string template, IReadOnlyList<SampleModule> modules, bool aggregate)
    {
        var entries = new StringBuilder();
        foreach (var (module, sample) in GetSamples(modules, web: false))
        {
            var slug = GetModuleSlug(module.Definition.Name);
            var id = aggregate ? $"{slug}:{sample.Id}" : sample.Id;
            entries.AppendLine($"        new(\"{CSharp(id)}\", \"{CSharp(sample.Name)}\", static () => new global::{sample.ApplicationType}()),");
        }
        return ReplaceGeneratedRegion(template, "android-sample-entries", entries.ToString(), GeneratedComment.CSharp);
    }

    private static string GenerateWebIndex(
        string template,
        string title,
        IReadOnlyList<SampleModule> modules,
        bool aggregate)
    {
        var html = ReplaceGeneratedRegion(template, "web-title",
            $"  <title>{WebUtility.HtmlEncode(title)} samples</title>", GeneratedComment.Xml);
        html = ReplaceGeneratedRegion(html, "web-heading",
            $"      <h1>{WebUtility.HtmlEncode(title)} samples</h1>", GeneratedComment.Xml);
        var options = string.Join(Environment.NewLine, GetSamples(modules, web: true).Select(entry =>
        {
            var slug = GetModuleSlug(entry.Module.Definition.Name);
            var id = aggregate ? $"{slug}:{entry.Sample.Id}" : entry.Sample.Id;
            return $"        <option value=\"{WebUtility.HtmlEncode(id)}\" data-application=\"{WebUtility.HtmlEncode(entry.Sample.ApplicationType)}\">{WebUtility.HtmlEncode(entry.Sample.Name)}</option>";
        }));
        return ReplaceGeneratedRegion(html, "web-sample-options", options, GeneratedComment.Xml);
    }

    private static string ReplaceGeneratedRegion(
        string template,
        string name,
        string content,
        GeneratedComment comment)
    {
        var startMarker = comment == GeneratedComment.CSharp
            ? $"// <redbolt-generated name=\"{name}\">"
            : $"<!-- <redbolt-generated name=\"{name}\"> -->";
        var endMarker = comment == GeneratedComment.CSharp
            ? $"// </redbolt-generated name=\"{name}\">"
            : $"<!-- </redbolt-generated name=\"{name}\"> -->";
        var normalized = template.Replace("\r\n", "\n");
        var start = normalized.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0 || start != normalized.LastIndexOf(startMarker, StringComparison.Ordinal))
            throw new InvalidDataException($"Template must contain one generated region named '{name}'.");

        var contentStart = normalized.IndexOf('\n', start + startMarker.Length);
        if (contentStart < 0)
            throw new InvalidDataException($"Generated region '{name}' has no content line.");
        contentStart++;
        var end = normalized.IndexOf(endMarker, contentStart, StringComparison.Ordinal);
        if (end < 0 || end != normalized.LastIndexOf(endMarker, StringComparison.Ordinal))
            throw new InvalidDataException($"Generated region '{name}' must contain one closing marker.");
        var endLineStart = normalized.LastIndexOf('\n', end) + 1;

        var replacement = content.Replace("\r\n", "\n").TrimEnd('\n');
        if (replacement.Length > 0)
            replacement += "\n";
        return normalized[..contentStart] + replacement + normalized[endLineStart..];
    }

    private static void VerifyGeneratedRegionReplacement()
    {
        const string fixture = "before\n// <redbolt-generated name=\"test\">\nold\n// </redbolt-generated name=\"test\">\nafter\n";
        const string expected = "before\n// <redbolt-generated name=\"test\">\nnew\n// </redbolt-generated name=\"test\">\nafter\n";
        if (ReplaceGeneratedRegion(fixture, "test", "new", GeneratedComment.CSharp) != expected)
            throw new InvalidDataException("Generated region replacement did not preserve template source.");
    }

    private static string GetModuleSlug(string moduleName) =>
        WebModules.FirstOrDefault(module => module.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase))?.Slug
        ?? moduleName.ToLowerInvariant().Replace('.', '-');

    private static string RelativeProjectPath(string directory, string path) =>
        ToProjectPath(Path.GetRelativePath(directory, path));

    private static string ToProjectPath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    private static string ToIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        var uppercase = true;
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                uppercase = true;
                continue;
            }
            builder.Append(uppercase ? char.ToUpperInvariant(character) : character);
            uppercase = false;
        }
        if (builder.Length == 0 || !char.IsLetter(builder[0]))
            builder.Insert(0, 'S');
        return builder.ToString();
    }

    private static string Xml(string value) => WebUtility.HtmlEncode(value);

    private static string CSharp(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void WriteGeneratedFile(string path, string content, bool check, ref bool stale)
    {
        content = content.Replace("\r\n", "\n");
        if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == content)
            return;
        if (check)
        {
            Console.Error.WriteLine($"Stale generated file: {path}");
            stale = true;
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static async Task<int> InitializeAsync(IReadOnlyList<Repository> repositories)
    {
        var root = GetScriptDirectory();
        var ready = new List<Repository>();
        var failed = false;

        foreach (var repository in repositories)
        {
            var destination = Path.Combine(root, repository.RelativePath);
            if (Directory.Exists(Path.Combine(destination, ".git")) || File.Exists(Path.Combine(destination, ".git")))
            {
                Console.WriteLine($"Already cloned: {repository.RelativePath}");
                ready.Add(repository);
                continue;
            }

            if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
            {
                Console.Error.WriteLine($"Cannot clone {repository.Name}: '{destination}' exists and is not a Git repository.");
                failed = true;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Console.WriteLine($"Cloning {repository.Name} into {repository.RelativePath}...");

            var exitCode = await RunGitCloneAsync(repository, destination);
            if (exitCode == 0)
                ready.Add(repository);
            else
                failed = true;
        }

        return failed ? 1 : 0;
    }

    private static async Task<int> PullAsync(string reference)
    {
        var root = GetScriptDirectory();
        var failed = false;
        var found = 0;

        Console.WriteLine("Pulling monorepo...");
        if (await RunGitPullAsync(root, reference) != 0)
            failed = true;

        foreach (var repository in Repositories)
        {
            var directory = Path.Combine(root, repository.RelativePath);
            if (!Directory.Exists(Path.Combine(directory, ".git")) && !File.Exists(Path.Combine(directory, ".git")))
                continue;

            found++;
            Console.WriteLine($"Pulling {repository.RelativePath}...");
            if (await RunGitPullAsync(directory, reference) != 0)
                failed = true;
        }

        if (found == 0)
            Console.WriteLine("No cloned module repositories found.");

        return failed ? 1 : 0;
    }

    private static bool TryParsePublishOptions(string[] args, out PublishOptions options, out string? error)
    {
        options = new PublishOptions();
        error = null;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            var separator = argument.IndexOf('=');
            var name = separator >= 0 ? argument[..separator] : argument;
            var value = separator >= 0 ? argument[(separator + 1)..] : null;

            if (name == "--build")
            {
                options.Build = true;
                continue;
            }
            if (name != "--build-config")
            {
                error = $"Unknown publish-samples option '{argument}'.";
                return false;
            }
            if (value is null && index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
                value = args[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"Option '{name}' requires a value.";
                return false;
            }

            if (name == "--build-config")
            {
                if (value.Equals("debug", StringComparison.OrdinalIgnoreCase))
                    options.Configuration = "Debug";
                else if (value.Equals("release", StringComparison.OrdinalIgnoreCase))
                    options.Configuration = "Release";
                else
                {
                    error = $"Unknown build configuration '{value}'. Use Debug or Release.";
                    return false;
                }
                continue;
            }
        }

        return true;
    }

    private static async Task<int> PublishSamplesAsync(PublishOptions options)
    {
        var root = GetScriptDirectory();
        var website = Path.Combine(root, "RedBoltFramework.github.io");
        if (!Directory.Exists(Path.Combine(website, ".git")))
        {
            Console.Error.WriteLine($"Website repository is missing: {website}");
            return 1;
        }

        if (options.Build)
        {
            var generateOptions = new GenerateSamplesOptions
            {
                Name = PublishedSampleBundle.Slug,
                Output = "Samples",
            };
            generateOptions.Modules.AddRange(PublishedSampleBundle.Modules);
            if (GenerateSamples(generateOptions) != 0)
                return 1;
            if (await BuildWebSamplesAsync(root, options) != 0)
                return 1;
        }

        var catalog = WebModules.Select(module => ReadWebModule(root, module, options.Configuration)).ToArray();
        var available = catalog.Where(module => module.Available).ToArray();
        if (available.Length == 0)
        {
            Console.Error.WriteLine($"No {options.Configuration} web sample bundle was found. Build the web platform first or pass --build.");
            return 1;
        }

        var stage = Path.Combine(website, $".samples-stage-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stage);
            var outputDirectories = available.Select(module => module.Bundle!.OutputDirectory)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (outputDirectories.Length != 1)
                throw new InvalidDataException("The web catalog must use one unified sample bundle.");
            CopyDirectory(outputDirectories[0], stage);
            Console.WriteLine($"Published {string.Join(", ", available.Select(module => module.Name))}: samples/");

            var samplesDirectory = Path.Combine(website, "samples");
            if (Directory.Exists(samplesDirectory))
                Directory.Delete(samplesDirectory, recursive: true);
            Directory.Move(stage, samplesDirectory);
            WriteCatalog(Path.Combine(website, "catalog.json"), catalog);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"Cannot publish web samples: {exception.Message}");
            return 1;
        }
        finally
        {
            if (Directory.Exists(stage))
                Directory.Delete(stage, recursive: true);
        }

        foreach (var module in catalog.Where(module => !module.Available && module.Samples.Count > 0))
            Console.WriteLine($"Skipped {module.Name}: no {options.Configuration} web bundle found.");
        Console.WriteLine($"Updated {Path.Combine(website, "catalog.json")}.");
        return 0;
    }

    private static async Task<int> BuildWebSamplesAsync(string root, PublishOptions options)
    {
        var bundle = PublishedSampleBundle;
        var project = Path.Combine(root, "Samples", "Web", "RedBolt.Sample.Web.csproj");
        var output = Path.Combine(root, "build", "bin", options.Configuration, "browser-wasm", "web", bundle.Slug);
        Console.WriteLine($"Building {options.Configuration} unified WebAssembly bundle '{bundle.Slug}'...");
        if (Directory.Exists(output))
            Directory.Delete(output, recursive: true);

        var restoreResult = await RunProcessAsync(root, "dotnet", new[]
        {
            "restore", project,
            "-p:MSBuildEnableWorkloadResolver=false",
            "-p:RedBoltParallelization=1",
            "-p:RedBoltPlatform=browser-wasm",
            "-p:RedBoltEnablePlatforms=web",
            "-p:RuntimeIdentifier=browser-wasm",
        });
        if (restoreResult != 0)
            return restoreResult;

        // The entry project needs the WebAssembly workload for its final native link.
        var publishArguments = new[]
        {
            "publish", project,
            "--configuration", options.Configuration,
            "--runtime", "browser-wasm",
            "--no-restore",
            "-m:1",
            "-p:RedBoltParallelization=1",
            "-p:RedBoltPlatform=browser-wasm",
            "-p:RedBoltEnablePlatforms=web",
        };
        return await RunProcessAsync(root, "dotnet", publishArguments);
    }

    private static PublishedModule ReadWebModule(string root, WebModule module, string configuration)
    {
        var repository = Path.Combine(root, module.Name);
        var catalogPath = Path.Combine(repository, "src", "RedBolt", "Sample", "SampleCatalog.json");
        var definition = File.Exists(catalogPath)
            ? JsonSerializer.Deserialize(File.ReadAllText(catalogPath), SampleJsonContext.Default.SampleModuleDefinition)
            : null;
        var bundle = PublishedSampleBundle.Modules.Contains(module.Name, StringComparer.OrdinalIgnoreCase)
            ? PublishedSampleBundle
            : null;
        var output = bundle is null ? null : Path.Combine(root, "build", "bin", configuration, "browser-wasm", "web", bundle.Slug, "wwwroot");
        var samples = definition?.Web == true
            ? definition.Samples.Where(sample => SupportsPlatform(sample, web: true))
                .OrderBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(sample => sample.Id, StringComparer.OrdinalIgnoreCase)
                .Select(sample => new WebSample(sample.Id, sample.Name, sample.ApplicationType)).ToArray()
            : [];
        var available = output is not null && File.Exists(Path.Combine(output, "index.html"));
        var publishedBundle = available ? new PublishedBundle(output!) : null;
        return new PublishedModule(module, samples, available, publishedBundle);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
    }
    private static void WriteCatalog(string path, IReadOnlyList<PublishedModule> modules)
    {
        var temporaryPath = path + ".tmp";
        using (var stream = File.Create(temporaryPath))
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartArray();
            foreach (var module in modules)
            {
                writer.WriteStartObject();
                writer.WriteString("slug", module.Slug);
                writer.WriteString("name", module.Name);
                writer.WriteString("category", module.Category);
                writer.WriteString("description", module.Description);
                writer.WriteString("repository", $"https://github.com/{GitHubOrganization}/{module.Name}");
                writer.WriteBoolean("available", module.Available);
                writer.WriteStartArray("samples");
                foreach (var sample in module.Samples)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", sample.Id);
                    writer.WriteString("name", sample.Name);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    private static async Task<int> RunGitCloneAsync(Repository repository, string destination)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                UseShellExecute = false,
            },
        };

        process.StartInfo.ArgumentList.Add("clone");
        process.StartInfo.ArgumentList.Add($"git@github.com:{GitHubOrganization}/{repository.Name}.git");
        process.StartInfo.ArgumentList.Add(destination);

        try
        {
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"Cannot run Git: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunGitPullAsync(string directory, string reference)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                UseShellExecute = false,
            },
        };

        process.StartInfo.ArgumentList.Add("-C");
        process.StartInfo.ArgumentList.Add(directory);
        process.StartInfo.ArgumentList.Add("pull");
        process.StartInfo.ArgumentList.Add("--rebase");
        process.StartInfo.ArgumentList.Add("origin");
        process.StartInfo.ArgumentList.Add(reference);

        try
        {
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"Cannot run Git: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunProcessAsync(string directory, string fileName, IReadOnlyCollection<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = directory,
                UseShellExecute = false,
            },
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        try
        {
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"Cannot run '{fileName}': {exception.Message}");
            return 1;
        }
    }

    private static string GetScriptDirectory([CallerFilePath] string sourcePath = "") =>
        Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;

    private static void PrintHelp()
    {
        Console.WriteLine("RedBolt monorepo helper");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ./monorepo.cs <subcommand> [options]");
        Console.WriteLine();
        Console.WriteLine("Subcommands:");
        Console.WriteLine("  init              Clone module repositories");
        Console.WriteLine("  pull              Pull every cloned repository with rebase");
        Console.WriteLine("  generate-samples  Generate web and Android sample hosts");
        Console.WriteLine("  publish-samples   Deploy built web samples to the website");
        Console.WriteLine();
        Console.WriteLine("Run './monorepo.cs <subcommand> --help' for command options.");
    }

    private static void PrintPullHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ./monorepo.cs pull [REF]");
        Console.WriteLine();
        Console.WriteLine("Pulls the specified ref from origin into every cloned repository with rebase.");
        Console.WriteLine("The default ref is master.");
    }

    private static void PrintInitHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ./monorepo.cs init [--with-modules NAME...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --with-modules NAME...  Enable optional repositories");
        Console.WriteLine();
        Console.WriteLine("Module names can be repeated or separated with commas.");
        Console.WriteLine("Use '--with-modules=all' to enable every module.");
        Console.WriteLine("Optional by default: RedBolt.Tests, SharpCpp, imgui");
    }

    private static void PrintPublishSamplesHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ./monorepo.cs publish-samples [--build] [--build-config Debug|Release]");
        Console.WriteLine();
        Console.WriteLine("Copies built WebAssembly sample catalogs into RedBoltFramework.github.io/samples.");
        Console.WriteLine("Pass --build to rebuild the unified WebAssembly bundle before publishing.");
        Console.WriteLine("The default build configuration is Release.");
    }

    private static void PrintGenerateSamplesHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ./monorepo.cs generate-samples [--modules NAME...] [--check]");
        Console.WriteLine("  ./monorepo.cs generate-samples --output PATH [--name NAME] [--modules NAME...] [--check]");
        Console.WriteLine();
        Console.WriteLine("Without '--output', regenerates each selected module's local web and Android hosts.");
        Console.WriteLine("With '--output', aggregates the selected modules into one host directory.");
        Console.WriteLine("Module names can be repeated or comma-separated; the default is all catalog modules.");
        Console.WriteLine("Use '--check' to report stale generated files without changing them.");
    }

    private sealed record Repository(string Name, string RelativePath, bool EnabledByDefault);
    private sealed record WebModule(string Name, string Slug, string Category, string Description);
    private sealed record SampleBundle(string Slug, IReadOnlyList<string> Modules);
    private sealed record WebSample(string Id, string Name, string Application);
    private sealed record PublishedBundle(string OutputDirectory);
    private sealed record SampleModule(string Root, string CatalogPath, SampleModuleDefinition Definition);
    private sealed record SampleTemplates(
        string WebProgram,
        string WebProject,
        string WebIndex,
        string AndroidProgram,
        string AndroidProject);
    private sealed record WebResourcePackage(string Name, string Directory);
    private enum GeneratedComment
    {
        CSharp,
        Xml,
    }
    private sealed class GenerateSamplesOptions
    {
        public List<string> Modules { get; } = [];
        public string? Output { get; set; }
        public string? Name { get; set; }
        public bool Check { get; set; }
    }
    internal sealed class SampleModuleDefinition
    {
        public string Name { get; set; } = "";
        public string ApplicationId { get; set; } = "";
        public bool Web { get; set; }
        public bool Android { get; set; }
        public bool Native { get; set; }
        public bool WebNative { get; set; }
        public List<string> NativeDependencies { get; set; } = [];
        public List<string> ProjectReferences { get; set; } = [];
        public List<string> PlatformIndependentProjectReferences { get; set; } = [];
        public List<string> AdditionalSources { get; set; } = [];
        public List<string> CoreData { get; set; } = [];
        public List<string> Data { get; set; } = [];
        public string? ResourcePackageName { get; set; }
        public List<string> WebPInvokeModules { get; set; } = [];
        public List<string> TrimmerRootAssemblies { get; set; } = [];
        public string? WebExtension { get; set; }
        public string? AndroidManifest { get; set; }
        public List<string> AndroidNativeLibraries { get; set; } = [];
        public List<SampleDefinition> Samples { get; set; } = [];
    }
    internal sealed class SampleDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ApplicationType { get; set; } = "";
        public string Source { get; set; } = "";
        public List<string> Platforms { get; set; } = [];
    }
    private sealed class PublishOptions
    {
        public bool Build { get; set; }
        public string Configuration { get; set; } = "Release";
    }
    private sealed record PublishedModule(
        string Name,
        string Slug,
        string Category,
        string Description,
        IReadOnlyList<WebSample> Samples,
        bool Available,
        PublishedBundle? Bundle)
    {
        public PublishedModule(WebModule module, IReadOnlyList<WebSample> samples, bool available, PublishedBundle? bundle)
            : this(module.Name, module.Slug, module.Category, module.Description, samples, available, bundle)
        {
        }
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MonorepoApp.SampleModuleDefinition))]
internal partial class SampleJsonContext : JsonSerializerContext;
