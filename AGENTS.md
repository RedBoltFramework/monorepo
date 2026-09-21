# RedBolt workspace

## Repository layout

- `monorepo.cs` and `RedBolt.slnx` define the aggregate workspace.
- `RedBolt/` contains the core engine.
- Sibling directories contain modules and `RedBolt.Tests`.
- External modules require a sibling core checkout. Resolve it through project
  references and existing root properties; do not embed absolute paths.

## Workspace setup

- Install .NET SDK 10 or newer.
- Configure Git SSH access to the `RedBoltFramework` GitHub organization.
- Run the executable script from any directory:

  ```sh
  /path/to/monorepo/monorepo.cs init
  ```

- Use `dotnet run /path/to/monorepo/monorepo.cs -- init` when executable script
  files are unavailable.
- The default selection clones all RedBolt repositories except `RedBolt.Tests`.
- External repositories `SharpCpp` and `imgui` are disabled by default.
- Enable optional repositories with `--with-modules`:

  ```sh
  ./monorepo.cs init --with-modules RedBolt.Tests SharpCpp imgui
  ./monorepo.cs init --with-modules=all
  ```

- Accept module names as repeated, space-separated, or comma-separated options.
- Use `--with-modules=all` to include tests and external repositories.
- Keep existing Git clones. Report an error instead of overwriting a non-Git
  directory.

## Ownership

- Keep shared MSBuild, CMake, Android, WebAssembly, script, and NuGet
  infrastructure in `RedBolt/src/RedBolt/ExternalModule` or its existing build
  directory.
- Keep modules limited to descriptors, sources, references, resources, sample
  catalogs, native targets, and package manifests.
- Use generated SharpCpp bindings for native dependencies. Do not add manual
  native bindings or P/Invoke wrappers.
- Use CPM for third-party source dependencies. Do not add git submodules.
- Follow nested `AGENTS.md` files for module-specific rules.

## Build and verify

- Build the aggregate workspace with `dotnet build RedBolt.slnx`.
- Build a module with its solution; use an affected `.csproj` for narrow
  iteration.
- Use `--no-restore` only after a matching restore.
- Finish each build before starting another build or launching tests.
- Preserve the saved build selection and active build root. Do not delete or
  rewrite build configuration files to infer defaults.
- Pass `-maxcpucount` to `dotnet build` and `--parallel` to `cmake --build`
  without numeric limits for ordinary builds. Use a single job only for build
  system debugging.
- Select a platform variant with `-p:RedBoltBuildPlatform=linux-x64`,
  `win-x64`, `android-x64`, `android-arm64`, or `browser-wasm`.
- Each solution `Build` or `Rebuild` records its active build root in
  `build/BuildRoot.props`. Direct `.csproj` builds use the root selected by the
  module's last solution build. Delete `BuildRoot.props` to restore the
  module-local default.
- Do not run root and module solutions concurrently; they can share generated
  dependencies.
- Follow [verification guidance](doc/VERIFICATION.md). Expand checks for shared
  infrastructure, native bindings, packaging, and affected platforms.
- For impact-based check selection, focused test harnesses, staged resources,
  native diagnostics, or SharpCpp E2E checks, read
  [`.agent/skills/redbolt-verification/SKILL.md`](.agent/skills/redbolt-verification/SKILL.md).
- Run the full platform, configuration, and package matrix for release
  validation or an explicit task requirement. For documentation-only changes,
  run link, command, and consistency checks instead of engine builds.
- For native-only work, list supported presets with `cmake --list-presets` and
  `cmake --build --list-presets`, then build with
  `cmake --build --preset <name>`.
- Let MSBuild invoke native dependencies for managed builds. Do not run
  `ninja -C` against a guessed binary directory or assume a preset uses the
  active MSBuild output root; use the owning build entry point.
- Use the aggregate solution as the canonical build and test entry point:

  ```sh
  dotnet build RedBolt.slnx --configuration Debug -maxcpucount
  dotnet test RedBolt.slnx --configuration Debug --no-build --no-restore
  ```

- Keep standalone module `.slnx` files usable for independent builds.
- Use RedBolt's default CPM-managed SharpCpp and imgui checkouts for builds.
  Treat top-level `extern/SharpCpp` and `extern/imgui` clones as developer-only
  checkouts.

## Repository commands

- Show command help with:

  ```sh
  ./monorepo.cs --help
  ./monorepo.cs init --help
  ```

- Rebase the monorepo, cloned modules, and cloned developer externals onto their
  `origin` branches with `./monorepo.cs pull [ref]`. The default ref is
  `master`.
- Push the current branch to each configured upstream with
  `./monorepo.cs push`.

- For sample catalogs, host templates, generation, and publishing, read
  [`.agent/skills/redbolt-samples/SKILL.md`](.agent/skills/redbolt-samples/SKILL.md).

## References

- [README.md](README.md): public overview and quick-start commands.
- [Local toolchains](doc/LOCAL_TOOLCHAINS.md): pinned host tools and recovery.
- [Core build requirements](RedBolt/doc/BUILD_REQUIREMENTS.md): platform setup
  and build selection.
- For direct C++ to C# ports, read
  [`.agent/skills/redbolt-porting/SKILL.md`](.agent/skills/redbolt-porting/SKILL.md).
