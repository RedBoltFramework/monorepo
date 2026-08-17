#!/bin/sh
#if false
exec dotnet run --file "$0" -- "$@"
#endif

using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

    private static readonly WebBundle[] WebBundles =
    [
        new("standard", "WebSamples/RedBolt.WebSamples.csproj",
            ["RedBolt", "RedBolt.2D", "RedBolt.Actions", "RedBolt.CSG", "RedBolt.IK", "RedBolt.Navigation", "RedBolt.Network", "RedBolt.Particles"]),
        new("physics-box2d", "WebSamples/RedBolt.WebSamples.csproj", ["RedBolt.PhysicsBox2D"]),
        new("physics-jolt", "WebSamples/RedBolt.WebSamples.csproj", ["RedBolt.PhysicsJolt"]),
        new("rmlui", "WebSamples/RedBolt.WebSamples.csproj", ["RedBolt.RmlUI"]),
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        if (string.Equals(args[0], "pull", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("The pull subcommand does not accept options.");
                return 1;
            }

            return await PullAsync();
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

            if (option is not ("--with-modules" or "--no-modules"))
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
                if (option == "--with-modules" && string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
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

                if (option == "--with-modules")
                    enabled.Add(repository.Name);
                else
                    enabled.Remove(repository.Name);
            }
        }

        selected = Repositories.Where(repository => enabled.Contains(repository.Name)).ToArray();
        error = null;
        return true;
    }

    private static IEnumerable<string> SplitModuleNames(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

        WriteWorkspace(root, ready);
        Console.WriteLine($"Workspace: {Path.Combine(root, ".vscode", "RedBolt.code-workspace")}");
        return failed ? 1 : 0;
    }

    private static async Task<int> PullAsync()
    {
        var root = GetScriptDirectory();
        var failed = false;
        var found = 0;

        foreach (var repository in Repositories)
        {
            var directory = Path.Combine(root, repository.RelativePath);
            if (!Directory.Exists(Path.Combine(directory, ".git")) && !File.Exists(Path.Combine(directory, ".git")))
                continue;

            found++;
            Console.WriteLine($"Pulling {repository.RelativePath}...");
            if (await RunGitPullAsync(directory) != 0)
                failed = true;
        }

        if (found == 0)
            Console.WriteLine("No cloned repositories found.");

        return failed ? 1 : 0;
    }

    private static bool TryParsePublishOptions(string[] args, out PublishOptions options, out string? error)
    {
        options = new PublishOptions();
        error = null;
        var modulesSpecified = false;
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
            if (name is not ("--build-config" or "--module" or "--modules"))
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

            if (!modulesSpecified)
            {
                options.Modules.Clear();
                modulesSpecified = true;
            }
            foreach (var moduleName in SplitModuleNames(value))
            {
                var module = WebModules.FirstOrDefault(candidate => candidate.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                if (module is null)
                {
                    error = $"Unknown web module '{moduleName}'.";
                    return false;
                }
                if (!options.Modules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
                    options.Modules.Add(module.Name);
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

        if (options.Build && await BuildWebSamplesAsync(root, options) != 0)
            return 1;

        var catalog = WebModules.Select(module => ReadWebModule(root, module, options.Configuration)).ToArray();
        var available = catalog.Where(module => module.Available).ToArray();
        if (available.Length == 0)
        {
            Console.Error.WriteLine($"No {options.Configuration} web sample bundles were found. Build the web platform first or pass --build.");
            return 1;
        }

        var stage = Path.Combine(website, $".samples-stage-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stage);
            var availableBundles = available.Select(module => module.Bundle!).DistinctBy(bundle => bundle.Slug).ToArray();
            foreach (var bundle in availableBundles)
            {
                var destination = Path.Combine(stage, bundle.Slug);
                CopyDirectory(bundle.OutputDirectory, destination);
                var bundleModules = available.Where(module => module.Bundle?.Slug == bundle.Slug).ToArray();
                WriteBundleSelector(Path.Combine(destination, "index.html"), bundleModules);
                Console.WriteLine($"Published {string.Join(", ", bundleModules.Select(module => module.Name))}: samples/{bundle.Slug}/");
            }
            PoolResources(stage, availableBundles.Select(bundle => bundle.Slug));

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
        var selected = options.Modules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bundles = WebBundles.Where(bundle => bundle.Modules.Any(selected.Contains)).ToArray();
        foreach (var bundle in bundles)
        {
            Console.WriteLine($"Building {options.Configuration} WebAssembly bundle '{bundle.Slug}'...");
            var arguments = new[]
            {
                "build", Path.Combine(root, bundle.Project),
                "--configuration", options.Configuration,
                "-m:1",
                "-p:RedBoltParallelization=1",
                "-p:RedBoltPlatform=browser-wasm",
                "-p:RedBoltEnablePlatforms=web",
                "-p:RedBoltEnableModules=all",
                $"-p:RedBoltWebBundle={bundle.Slug}",
            };
            if (await RunProcessAsync(root, "dotnet", arguments) != 0)
                return 1;
        }
        return 0;
    }

    private static PublishedModule ReadWebModule(string root, WebModule module, string configuration)
    {
        var repository = Path.Combine(root, module.Name);
        var selector = Path.Combine(repository, "src", "RedBolt", "Sample", "Web", "wwwroot", "index.html");
        var bundle = WebBundles.FirstOrDefault(candidate => candidate.Modules.Contains(module.Name, StringComparer.OrdinalIgnoreCase));
        var output = bundle is null ? null : Path.Combine(root, "build", "bin", configuration, "browser-wasm", "web", bundle.Slug, "wwwroot");
        var samples = File.Exists(selector) ? ReadSamples(selector) : [];
        if (module.Name == "RedBolt.Network")
            samples = samples.Where(sample => !sample.Id.Equals("webrtc-stun", StringComparison.OrdinalIgnoreCase)).ToArray();
        var available = output is not null && File.Exists(Path.Combine(output, "index.html"));
        var publishedBundle = available ? new PublishedBundle(bundle!.Slug, output!) : null;
        return new PublishedModule(module, samples, available, publishedBundle);
    }

    private static IReadOnlyList<WebSample> ReadSamples(string selector)
    {
        var html = File.ReadAllText(selector);
        html = Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline);
        var pattern = "<option\\b(?=[^>]*\\bvalue=\"(?<id>[^\"]+)\")(?=[^>]*\\bdata-application=\"(?<application>[^\"]+)\")[^>]*>(?<name>.*?)</option>";
        var samples = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(match => new WebSample(
                WebUtility.HtmlDecode(match.Groups["id"].Value.Trim()),
                WebUtility.HtmlDecode(Regex.Replace(match.Groups["name"].Value, "<[^>]+>", "").Trim()),
                WebUtility.HtmlDecode(match.Groups["application"].Value.Trim())))
            .ToArray();

        if (samples.Select(sample => sample.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != samples.Length)
            throw new InvalidDataException($"Duplicate sample identifiers in {selector}.");
        return samples;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
    }

    private static void WriteBundleSelector(string path, IReadOnlyList<PublishedModule> modules)
    {
        var html = File.ReadAllText(path);
        var options = string.Join(Environment.NewLine, modules.SelectMany(module => module.Samples.Select(sample =>
            $"        <option value=\"{WebUtility.HtmlEncode(module.Slug + ":" + sample.Id)}\" data-application=\"{WebUtility.HtmlEncode(sample.Application)}\">{WebUtility.HtmlEncode(sample.Name)}</option>")));
        var replacement = $"<select id=\"sample\">{Environment.NewLine}{options}{Environment.NewLine}      </select>";
        html = Regex.Replace(html, "<select\\s+id=\"sample\"[^>]*>.*?</select>", replacement,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        File.WriteAllText(path, html);
    }

    private static void PoolResources(string stage, IEnumerable<string> bundleSlugs)
    {
        var shared = Path.Combine(stage, "shared", "resources");
        foreach (var slug in bundleSlugs)
        {
            var bundle = Path.Combine(stage, slug);
            var manifestPath = Path.Combine(bundle, "resources.manifest.json");
            if (!File.Exists(manifestPath))
                continue;

            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsArray()
                ?? throw new InvalidDataException($"Invalid resource manifest: {manifestPath}");
            foreach (var item in manifest)
            {
                var entry = item?.AsObject() ?? throw new InvalidDataException($"Invalid resource entry: {manifestPath}");
                var name = entry["name"]?.GetValue<string>()
                    ?? throw new InvalidDataException($"Resource entry has no name: {manifestPath}");
                var source = Path.GetFullPath(Path.Combine(bundle, "_framework", name));
                if (!File.Exists(source))
                    throw new InvalidDataException($"Resource does not exist: {source}");
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
                var fileName = hash + Path.GetExtension(source);
                Directory.CreateDirectory(shared);
                var destination = Path.Combine(shared, fileName);
                if (!File.Exists(destination))
                    File.Copy(source, destination);
                entry["name"] = $"../shared/resources/{fileName}";
            }
            File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            var resources = Path.Combine(bundle, "resources");
            if (Directory.Exists(resources))
                Directory.Delete(resources, recursive: true);
        }
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
                if (module.Bundle is not null)
                    writer.WriteString("bundle", module.Bundle.Slug);
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

    private static async Task<int> RunGitPullAsync(string directory)
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

    private static void WriteWorkspace(string root, IReadOnlyList<Repository> repositories)
    {
        var vscodeDirectory = Path.Combine(root, ".vscode");
        Directory.CreateDirectory(vscodeDirectory);

        var workspacePath = Path.Combine(vscodeDirectory, "RedBolt.code-workspace");
        using var stream = File.Create(workspacePath);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("folders");
            foreach (var repository in repositories)
            {
                writer.WriteStartObject();
                writer.WriteString("name", repository.Name);
                writer.WriteString("path", "../" + repository.RelativePath.Replace(Path.DirectorySeparatorChar, '/'));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        stream.WriteByte((byte)'\n');
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
        Console.WriteLine("  init    Clone modules and generate the VS Code workspace");
        Console.WriteLine("  pull    Pull every cloned repository with rebase");
        Console.WriteLine("  publish-samples  Deploy built web samples to the website");
        Console.WriteLine();
        Console.WriteLine("Run './monorepo.cs init --help' for init options.");
    }

    private static void PrintInitHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ./monorepo.cs init [--with-modules NAME...] [--no-modules NAME...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --with-modules NAME...  Enable optional modules");
        Console.WriteLine("  --no-modules NAME...    Disable default modules");
        Console.WriteLine();
        Console.WriteLine("Module names can be repeated or separated with commas.");
        Console.WriteLine("Use '--with-modules=all' to enable every module.");
        Console.WriteLine("Optional by default: RedBolt.Tests, SharpCpp, imgui");
    }

    private static void PrintPublishSamplesHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ./monorepo.cs publish-samples [--build] [--module NAME] [--build-config Debug|Release]");
        Console.WriteLine();
        Console.WriteLine("Copies built WebAssembly sample catalogs into RedBoltFramework.github.io/samples.");
        Console.WriteLine("Pass --build to incrementally build the selected modules before publishing.");
        Console.WriteLine("Module names can be repeated or comma-separated; the default is all modules.");
        Console.WriteLine("The default build configuration is Release.");
    }

    private sealed record Repository(string Name, string RelativePath, bool EnabledByDefault);
    private sealed record WebModule(string Name, string Slug, string Category, string Description);
    private sealed record WebBundle(string Slug, string Project, IReadOnlyList<string> Modules);
    private sealed record WebSample(string Id, string Name, string Application);
    private sealed record PublishedBundle(string Slug, string OutputDirectory);
    private sealed class PublishOptions
    {
        public bool Build { get; set; }
        public string Configuration { get; set; } = "Release";
        public List<string> Modules { get; } = WebModules.Select(module => module.Name).ToList();
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
