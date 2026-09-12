# RedBolt workspace

## Layout and ownership

This directory contains `monorepo.cs` and the aggregate `RedBolt.slnx`. The core
engine is in `RedBolt/`; modules and `RedBolt.Tests/` are its siblings. External
modules require the core checkout; module solution builds are supported, but
builds without the core are not. Use project references and existing root
properties to locate dependencies instead of embedding absolute checkout paths.

Keep shared MSBuild, CMake, Android, WebAssembly, script, and NuGet infrastructure
in the core's `src/RedBolt/ExternalModule` or matching existing build directory.
Modules retain only descriptors, sources, references, resources, sample catalogs,
native targets, and package manifests. Preserve module-specific constraints in
nested `AGENTS.md` files.

## Build and verification

Use `dotnet build RedBolt.slnx` for aggregate work, the affected module solution
for module work, or an affected `.csproj` for narrow iteration. The core solution
is `RedBolt/RedBolt.slnx`. Use `--no-restore` only after a matching restore.
Preserve the saved build selection and active build root during ordinary
iteration; do not delete or rewrite build configuration files to guess defaults.

Follow [verification guidance](doc/VERIFICATION.md) when building or testing.
Expand checks for shared infrastructure, native bindings, packaging, and affected
platforms. A full platform/configuration/package matrix is a release gate or an
explicit task requirement, not a gate for every edit. Documentation-only changes
need link, command, and consistency checks rather than engine builds.

For native-only work, discover supported presets with `cmake --list-presets`
and `cmake --build --list-presets`, then use `cmake --build --preset <name>`.
Normal managed builds invoke native dependencies through MSBuild. Do not run
`ninja -C` on a guessed binary directory or assume a preset uses the active
MSBuild output root; use the entry point that owns that configuration.

## Conditional references

- [README.md](README.md): monorepo commands, sample catalogs and host generation.
- [Local toolchains](doc/LOCAL_TOOLCHAINS.md): pinned host tools or recovery.
- [Core build requirements](RedBolt/doc/BUILD_REQUIREMENTS.md): platform setup
  and build selection semantics.

For sample catalog or shared host changes, use `./monorepo.cs generate-samples`
with the relevant `--modules` selection, then its `--check` mode. For requested
local Web catalog staging, `./monorepo.cs publish-samples --build --build-config Debug` prepares the website files; use Release for release preparation. Local
staging is distinct from a Git push or remote deployment. Use `./monorepo.cs --help` to discover commands and options rather than reconstructing paths.
