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

- **Monorepo mode:** from this repository root, run `dotnet build ./RedBolt.slnx`. The root solution builds the selected core and sibling leaf projects into this repository's shared `build/` directory. Select a platform variant with `-p:RedBoltBuildPlatform=linux-x64`, `win-x64`, `android-x64`, `android-arm64`, or `browser-wasm`.
- **Module mode:** from `RedBolt/` or one sibling module repository, run `dotnet build ./<module>.slnx`. The module solution uses the same module-local build directory and resolves a sibling core checkout through `../RedBolt` when required.

Each solution `Build` or `Rebuild` records its active build root in each module checkout's local `build/BuildRoot.props`. A later direct `.csproj` build uses the root selected by that module's last solution build. Delete `BuildRoot.props` to restore the module-local default. Do not run the root and module solutions concurrently because they can share the core checkout's generated dependencies.

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

## Update samples

Each module owns its sample list in `src/RedBolt/Sample/SampleCatalog.json`. To add, remove, or rename a sample, update its entry in that catalog and its source directory under `src/RedBolt/Sample/`. The important catalog fields are:

- `web` and `android` enable the module host for each platform.
- `samples` defines each sample `id`, display `name`, `applicationType`, and source directory. Generated sample lists are sorted alphabetically by display name.
- A sample `platforms` list limits that entry to `web` or `android`; an empty or missing list enables both supported platforms.
- `projectReferences`, `additionalSources`, and the platform-specific fields add build inputs.
- `coreData` and `data` select data files that the generated projects package and mount at runtime.

The core projects in `RedBolt/src/RedBolt/Sample/Web/` and `RedBolt/src/RedBolt/Sample/Android/` are both working core sample hosts and the templates for every generated host. Change shared host behavior there. Keep every `<redbolt-generated name="...">` marker intact and unique: the generator replaces only the marked regions, so hand-written code outside them survives a core self-update. Do not manually edit generated hosts in external modules or `Samples/`; generation replaces them from the core templates.

After a catalog, sample source, data file, or core template changes, regenerate all module-local hosts:

```sh
./monorepo.cs generate-samples
```

Regenerate only selected module hosts during focused work:

```sh
./monorepo.cs generate-samples --modules RedBolt.RmlUI
```

Use check mode in validation to fail when committed generated hosts are stale:

```sh
./monorepo.cs generate-samples --check
```

Generate a custom unified Web and Android host by giving an output, a host name, and a module list:

```sh
./monorepo.cs generate-samples \
  --output Samples --name all \
  --modules RedBolt RedBolt.2D RedBolt.Actions RedBolt.CSG
```

The output contains generated WebAssembly and Android projects plus their shared build infrastructure. Native modules can be combined when one selected module owns the other native modules as dependencies; incompatible native roots are rejected. Module data remains in distinct `.pak` files and is merged through the runtime virtual file system.

The only supported published bundle is `PublishedSampleBundle` in `monorepo.cs`. Change its module list to change which module catalogs appear in the unified website and Android applications. Then build and deploy the unified Release WebAssembly catalog to the website repository:

```sh
./monorepo.cs publish-samples --build
```

With `--build`, the command regenerates and builds the only supported monorepo host under `Samples/`. It copies that unified bundle directly to `RedBoltFramework.github.io/samples/` and rebuilds `catalog.json` from the selected module catalogs. Release web builds use AOT. Add `--build-config Debug` for a faster local web build without AOT. Android generation does not require web deployment.

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
