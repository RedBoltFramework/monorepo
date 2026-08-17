# RedBolt monorepo helper

This repository contains a file-based C# app that prepares a RedBolt development workspace. The `init` subcommand clones the selected repositories beside the script and writes `.vscode/RedBolt.code-workspace` with one folder for each available clone.

## Requirements

- .NET SDK 10 or newer
- Git with SSH access to the `RedBoltFramework` GitHub organization

## Usage

Run the executable script from any working directory:

```sh
/path/to/monorepo/monorepo.cs init
```

You can also use `dotnet run /path/to/monorepo/monorepo.cs -- init` on systems that do not use executable script files.

The default selection includes all RedBolt repositories except `RedBolt.Tests`. External repositories `SharpCpp` and `imgui` are also disabled by default. Enable optional modules with `--with-modules` and disable default modules with `--no-modules`:

```sh
./monorepo.cs init --with-modules RedBolt.Tests SharpCpp imgui
./monorepo.cs init --with-modules=all
./monorepo.cs init --no-modules RedBolt.Glow,RedBolt.CSG
```

Module options can occur more than once and accept space-separated or comma-separated names. Use `--with-modules=all` to clone every repository, including tests and external repositories. Existing Git clones are kept. The command reports an error instead of overwriting a non-Git directory.

Show command help with:

```sh
./monorepo.cs --help
./monorepo.cs init --help
```

Pull updates from `origin` for the monorepo, every cloned module, and every cloned developer external with rebase. The ref defaults to `master`:

```sh
./monorepo.cs pull
./monorepo.cs pull feature/my-change
```

Build and deploy the grouped Release WebAssembly sample catalog to the website repository:

```sh
./monorepo.cs publish-samples --build
```

The command builds one shared standard bundle plus dedicated bundles for Box2D, Jolt, and RmlUI. It copies each bundle to `RedBoltFramework.github.io/samples/`, pools identical resource packages by content hash, and rebuilds the landing-page catalog from the module selectors. Use `--module RedBolt.Network` for the logical bundle containing one module, or `--build-config Debug` for a local iteration build.

## Continuous integration

`ci.cs` removes generated build trees, bootstraps RedBolt's CPM dependencies, builds every module for all platforms and architectures in Debug and Release, then builds and runs both test configurations:

```sh
./ci.cs
```

Use `--incremental` to keep existing build outputs. Relocated CMake platform caches are still removed because CMake cannot reuse them from another checkout path. Build and test configurations can be selected independently with `--build-config` and `--test-config`; use `none` to skip either phase. Platform and architecture options accept repeated or comma-separated values.

```sh
./ci.cs --incremental --build-config Debug --test-config Debug
./ci.cs --test-config Release --tests 'FullyQualifiedName~PhysicsJolt'
./ci.cs --test-config none --platform linux,android --architectures x64
```

Run `./ci.cs --help` for all options and test argument forwarding.
Repositories that are not cloned are reported and skipped. Tests run only when all module repositories are available because the test project references the complete workspace. Builds use RedBolt's default CPM-managed SharpCpp and imgui checkouts; the top-level `extern/SharpCpp` and `extern/imgui` clones are only for developer use.
The runner uses CI tools from `~/.local/bin`, versioned tool packages under `~/.local/opt/*/bin`, or the system `PATH`. Explicit environment variables keep priority over detected Android and Java defaults.
