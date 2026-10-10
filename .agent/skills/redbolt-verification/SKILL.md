---
name: redbolt-verification
description: Select and run RedBolt builds, tests, samples, native diagnostics, packaging checks, and SharpCpp integration checks by change impact.
---

# RedBolt verification

Read [`doc/VERIFICATION.md`](../../../doc/VERIFICATION.md) and
[`RedBolt/doc/BUILD_REQUIREMENTS.md`](../../../RedBolt/doc/BUILD_REQUIREMENTS.md)
for the current build selection and platform setup.

## Select checks by impact

- Documentation or instruction edits: inspect diffs, links, and referenced CLI
  help or preset names. Do not run runtime builds unless an executable example
  requires execution to validate it.
- Behavior changes: build affected projects, run focused behavioral tests, and
  add meaningful regression coverage where needed.
- Direct ports and unmodified-feature fixes: use a focused test filter during
  iteration; do not make unrelated solution-wide suites the iteration gate.
- Shared build, generator, or cross-module changes: build the affected aggregate
  and exercise dependent modules and affected native/platform cells.
- Rendering or interaction changes: run the relevant sample and inspect the
  screenshot or interaction; process exit alone is not visual verification. Use
  MultipleViewports when no more relevant sample exercises the changed behavior.
- XR runtime changes: use an appropriate OpenXR runtime and report unavailable
  runtime verification without claiming it passed.
- Packaging changes: create and validate affected packages.
- Release validation: run the supported platform, configuration, and package
  matrix. Do not use that matrix as the default check for every edit.

## Build and test safely

- Finish one build before starting another or launching tests.
- Use the aggregate or owning module solution entry point. Use existing CMake
  presets for native-only work; do not guess binary directories.
- Query MSBuild properties such as `TargetPath` when an output path is needed.
  Do not reconstruct output trees or copy guessed resource directories.
- Build prerequisites before using `BuildProjectReferences=false`; existing
  assemblies must contain the change.
- Preserve `ResourceRoot.ini` in the monorepo root and shared assembly probing.
  Desktop executables find the resource directories through that file; the build
  makes no link or copy for them.
- Use the software-rendering harness for native, graphics, and visual tests:

  ```sh
  cd ../RedBolt.Tests
  export XDG_DATA_HOME=/home/rk/.local/share
  ./scripts/with_software_renderer.sh \
    dotnet test RedBolt.Tests.csproj --configuration Debug --no-build \
    --filter 'FullyQualifiedName~SomeTestClass' \
    --logger 'console;verbosity=minimal'
  ```

- Managed-only tests that do not initialize native engine state may run directly.
- Run a supported sample with `dotnet run --project <sample-project> --no-build`
  when the changed behavior is sample-visible and the project supports running.
- Keep tests sequential within a host. Use the isolated-host parallel runner for
  full-suite sharding; reduce `REDBOLT_TEST_SHARD_COUNT` to one when an
  emulator or QEMU uses the machine.
- For native diagnostics, run `cmake --list-presets`,
  `cmake --build --list-presets`, then the owning `cmake --build --preset ...`.
  The preset binary tree may differ from MSBuild's build root.

## Samples and publishing

- For sample generation, catalogs, hosts, and publishing, read
  `.agent/skills/redbolt-samples/SKILL.md`.
- Use `--build-config Debug` for local sample staging. Use `Release` for release
  preparation. Local staging does not imply a Git push or remote deployment.

## SharpCpp changes

- For RedBolt SharpCpp integration changes, run changed-generator focused checks,
  the generated-binding E2E project in `SharpCpp/tests/SharpCpp.Tests.E2E/`, and
  the rbfx wrapper E2E project in `rbfx/SharpCpp.Rbfx.Tests.E2E/` when those
  checkouts are part of the workspace.
- Resolve the SharpCpp checkout used by the build; do not assume the optional
  developer clone is active.
- Do not add SharpCpp test projects to the engine solution.
- Instruction-only changes do not require runtime suites.

## Completion

- Fix regressions caused by the change, rerun affected checks, and report
  unrelated failures or unavailable checks without changing their expectations.
