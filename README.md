# RedBolt monorepo helper

This repository contains a file-based C# app that prepares a RedBolt development workspace. The `init` subcommand clones the selected repositories beside the script.

## Requirements

- .NET SDK 10 or newer
- Git with SSH access to the `RedBoltFramework` GitHub organization

## Usage

Run the executable script from any working directory:

```sh
/path/to/monorepo/monorepo.cs init
```

You can also use `dotnet run /path/to/monorepo/monorepo.cs -- init` on systems that do not use executable script files.

The default selection includes all RedBolt repositories except `RedBolt.Tests`. External repositories `SharpCpp` and `imgui` are also disabled by default. Enable optional repositories with `--with-modules`:

```sh
./monorepo.cs init --with-modules RedBolt.Tests SharpCpp imgui
./monorepo.cs init --with-modules=all
```

Module options can occur more than once and accept space-separated or comma-separated names. Use `--with-modules=all` to clone every repository, including tests and external repositories. Existing Git clones are kept. The command reports an error instead of overwriting a non-Git directory.

There are two build modes:

- **Monorepo mode:** from this repository root, run `dotnet build ./RedBolt.slnx`. The root solution builds the selected core and sibling leaf projects. Each project writes only to its own `<module>/build` directory. Select a platform variant with `-p:RedBoltBuildPlatform=linux-x64`, `win-x64`, `android-x64`, `android-arm64`, or `browser-wasm`.
- **Module mode:** from `RedBolt/` or one sibling module repository, run `dotnet build ./<module>.slnx`. The module solution uses the same module-local build directory and resolves a sibling core checkout through `../RedBolt` when required.

The build system marks these modes explicitly. Do not run the root and module solutions concurrently because they can share the core checkout's generated dependencies.

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

Each module declares its web and Android catalog in `src/RedBolt/Sample/SampleCatalog.json`. Regenerate all module-local hosts, or verify that committed hosts are current, with:

```sh
./monorepo.cs generate-samples
./monorepo.cs generate-samples --check
```

Generate one compatible aggregate host by giving an output, a host name, and a module list:

```sh
./monorepo.cs generate-samples \
  --output Samples --name all \
  --modules RedBolt RedBolt.2D RedBolt.Actions RedBolt.CSG
```

The output contains generated WebAssembly and Android projects plus their shared build infrastructure. Native modules can be combined when one selected module owns the other native modules as dependencies; incompatible native roots are rejected.

Build and deploy the grouped Release WebAssembly sample catalog to the website repository:

```sh
./monorepo.cs publish-samples --build
```

With `--build`, the command regenerates and builds the only supported monorepo host under `Samples/`. It copies that unified bundle directly to `RedBoltFramework.github.io/samples/` and rebuilds the landing-page catalog from the module catalogs. Add `--build-config Debug` for a local iteration build.

## Build and test

The aggregate `RedBolt.slnx` is the canonical build and test entry point. Build the complete workspace with:

```sh
dotnet build RedBolt.slnx --configuration Debug
dotnet test RedBolt.slnx --configuration Debug --no-build --no-restore
```

For fast core-only iteration, use `RedBolt/RedBolt.slnx`. A focused Linux/x64 build can select the platform and architecture with MSBuild properties:

```sh
dotnet build RedBolt/RedBolt.slnx --configuration Debug --no-restore \
	-p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=linux \
	-p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64 \
	-p:RedBoltTestsEnabled=false
```

Module repositories retain their standalone `.slnx` files for independent builds. Builds use RedBolt's default CPM-managed SharpCpp and imgui checkouts; the top-level `extern/SharpCpp` and `extern/imgui` clones are only for developer use.
