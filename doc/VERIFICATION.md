# RedBolt verification

Commands below run from the monorepo root unless a different working directory
is stated. Resolve it from the checkout containing `monorepo.cs`; do not assume
an absolute path. Finish one build before starting another or launching tests.
The workspace concurrency and host-cache constraints apply to all commands.
For ordinary builds, pass `-maxcpucount` to `dotnet build` and `--parallel` to
`cmake --build` without a numeric limit so each tool uses its default
available-core parallelism. Use a single job only while debugging the build
system.

## Select checks by impact

- Documentation or instruction edits: inspect diffs, links, and referenced CLI
  help/preset names. No runtime builds unless an executable example changed in
  a way that requires execution to validate it.
- Behavior changes: build affected projects and run focused behavioral tests.
  Add or update meaningful regression coverage where needed.
- Shared build, generator, or cross-module changes: build the affected aggregate
  and exercise its dependent modules; include affected native/platform cells.
- Rendering or interaction changes: run the relevant sample and inspect the
  screenshot or interaction. Use MultipleViewports only when it exercises the
  changed behavior. XR runtime changes require an appropriate OpenXR runtime;
  report unavailable runtime verification without claiming it passed.
- Packaging changes: create and validate affected packages. Release readiness:
  run the supported platform/configuration/package matrix.
- Coverage tasks and release coverage validation: run the coverage harness below
  with its default 95% line and branch gate. Coverage is not an automatic gate
  for every edit. A zero minimum is diagnostic only.

Fix regressions caused by the change, rerun affected checks, and finish when the
requested behavior and applicable gates pass. Broaden or repeat checks only
when changes, failures, or unresolved risks justify it. Report unrelated failures
and unavailable checks without silently changing their expectations.

## Build entry points

```sh
# Aggregate build using the saved selection.
dotnet build RedBolt.slnx --configuration Debug -maxcpucount
# Core-only build using its saved selection.
dotnet build RedBolt/RedBolt.slnx --configuration Debug -maxcpucount
```

For a deliberately selected Linux x64 aggregate build, use the
`RedBoltEnable` property, for example `-p:RedBoltEnable=linux-x64`. Consult core build requirements for
selection persistence. Build test prerequisites before using
`BuildProjectReferences=false`; existing assemblies must reflect the change.
From the test repository:

```sh
dotnet build RedBolt.Tests.csproj --configuration Debug --no-restore -maxcpucount \
  -p:RedBoltKeepParentRuntimeAssemblies=true -p:BuildProjectReferences=false
```

Use `dotnet run --project <sample-project> --configuration Debug --no-build -- <sample-arguments>` when the project supports running. If a tool needs an output
path, query MSBuild (`dotnet msbuild <project> -getProperty:TargetPath` with the
same configuration/platform properties) or use build-reported output. Do not
reconstruct binary directories. Runtime resources are staged by build targets;
tests use the parent output resources. The engine changes its working directory
to the executable directory. Samples using staged resources use
`CoreData;SampleData` and resource prefixes `.;..`. Preserve that staging and
shared assembly probing instead of copying guessed output trees. For native-only diagnostics, use the owning
repository's existing configure and build presets, for example:

```sh
cmake --list-presets
cmake --build --list-presets
# From the core repository, after its matching configure:
cmake --build --preset linux-x64-debug --parallel
```

The preset's binary tree may differ from MSBuild's selected build root. Normal
engine verification stays with `dotnet`; configuring another tree is not a
required preliminary step.

## Test harnesses

From `RedBolt.Tests/`, set `XDG_DATA_HOME=/home/rk/.local/share` and use:

```sh
./scripts/with_software_renderer.sh   dotnet test RedBolt.Tests.csproj --configuration Debug --no-build   --filter 'FullyQualifiedName~SomeTestClass' --logger 'console;verbosity=minimal'
```

Replace the filter with the affected class. Native, graphics, and visual tests
need this Xvfb/Mesa software-rendering harness. Managed-only tests that do not
initialize native engine state can run `dotnet test` directly.

Native engine state is process-global. Keep tests sequential within each host;
for a full suite use `./scripts/run_tests_parallel.sh`, which launches isolated
hosts with `--no-build`. `REDBOLT_TEST_SHARD_COUNT` controls host count; use one
when an emulator/QEMU is consuming the machine's resources.

For coverage, the collector already invokes the software-rendering harness:

```sh
./scripts/collect_coverage.sh --no-build --logger 'console;verbosity=minimal'
```

Use the report path and rates printed by the collector. It selects the newest
Cobertura report by modification time. `REDBOLT_COVERAGE_RESULTS_DIR` optionally
selects a results directory.

## SharpCpp changes

For implementation changes affecting RedBolt's SharpCpp integration, run the
changed generator's focused checks and the generated-binding E2E project in
SharpCpp's `tests/SharpCpp.Tests.E2E/`. Before completion, also run the rbfx wrapper
E2E project in `rbfx/SharpCpp.Rbfx.Tests.E2E/`. Resolve the SharpCpp checkout used
by the build, rather than assuming the optional developer clone is active.
Do not add these test projects to the engine solution. Instruction-only edits
do not require these runtime suites.
