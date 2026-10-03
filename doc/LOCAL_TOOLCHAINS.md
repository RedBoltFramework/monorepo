# Local toolchains

Use this reference only for platform setup, dependency recovery, or missing tools.
Resolve `redbolt_engine_root` from the checked-out core project before using the
commands below. Paths to pinned host installations are intentional: a sandbox
HOME may not expose the host's tools or caches. Prefer existing environment
variables and `command -v` when they resolve the required version; these host
locations are fallbacks, not duplicate installation destinations.


- Resolve system-installed CMake, Ninja, and other build tools through `PATH`.
- Test dependencies and reusable caches are under `/home/rk/.local/share`. Set `XDG_DATA_HOME=/home/rk/.local/share` for tests. NuGet uses `NuGet/`; RedBolt stores compiled test and sample shaders under `Urho3D Rebel Fork/`.
- Use the existing Android JDK and SDK at `/home/rk/.local/share/redbolt-android/jdk` and `/home/rk/.local/share/redbolt-android/sdk`; do not install duplicates.
- A sandbox may replace `/home/rk` with an isolated home. Confirm that the host `.local` tree is exposed before reporting a missing dependency.

If an isolated home hides the system .NET SDK's user-local workload metadata or packs, restore them without updating manifests:

```sh
dotnet workload install wasm-tools android --skip-manifest-update
```

Use these Android build roots:

```sh
export XDG_DATA_HOME=/home/rk/.local/share
export JAVA_HOME=/home/rk/.local/share/redbolt-android/jdk
export ANDROID_HOME=/home/rk/.local/share/redbolt-android/sdk
export ANDROID_SDK_ROOT="$ANDROID_HOME"
export ANDROID_NDK_ROOT="$ANDROID_HOME/ndk/27.2.12479018"
```

For Windows x64 cross-builds, place Wine 11 and LLVM-MinGW before the system Wine on `PATH`, followed by the GCC-based MinGW scanner:

```sh
export PATH="/home/rk/.local/opt/wine-11.14-amd64-wow64/bin:/home/rk/.local/opt/llvm-mingw-20260616-msvcrt-ubuntu-22.04-x86_64/bin:/home/rk/.local/mingw-w64/usr/bin:$PATH"
```

For a new `win-x64` native build directory, configure the binding compilers and Wine executable before the first build. Follow the engine’s `doc/BUILD_REQUIREMENTS.md`. Do not use system Wine 9: its server socket path is on the read-only `/run` mount.

If the Android roots are missing, use the Android project to restore its pinned JDK and SDK, then install the pinned NDK:

```sh
cd "$redbolt_engine_root"
dotnet build src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj \
  --target InstallAndroidDependencies --framework net10.0-android \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=true
"$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" \
  'ndk;27.2.12479018'
```

To fetch the Android toolchain without the Android project, `dotnet run --file monorepo.cs -- install-android [--root DIR] [--accept-licenses]` downloads the pinned JDK, command-line tools, and SDK/NDK packages into `DIR/jdk` and `DIR/sdk` (default `build/android`); point the build at it with `RedBoltAndroidRoot`. It is a manual helper and is not part of any build.

Initialize missing dependencies or submodules before building:

```sh
cd "$redbolt_engine_root"
git submodule update --init --recursive
```
