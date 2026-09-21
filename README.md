# RedBolt monorepo helper

This repository contains a file-based C# application that prepares a RedBolt
development workspace. The `init` command clones selected repositories beside
the script.

## Requirements

- .NET SDK 10 or newer.
- Git SSH access to the `RedBoltFramework` GitHub organization.

## Quick start

Run the executable script from the repository root:

```sh
./monorepo.cs init
```

Use `/path/to/monorepo.cs init` from another directory, or use
`dotnet run /path/to/monorepo.cs -- init` when executable script files are
unavailable. The default selection includes all RedBolt repositories except
`RedBolt.Tests`; `SharpCpp` and `imgui` are disabled by default.

Enable optional repositories with:

```sh
./monorepo.cs init --with-modules RedBolt.Tests SharpCpp imgui
./monorepo.cs init --with-modules=all
```

Use `--with-modules=all` to clone every repository. Existing Git clones are
preserved; non-Git directories are not overwritten.

Show command help with:

```sh
./monorepo.cs --help
./monorepo.cs init --help
```

See [AGENTS.md](AGENTS.md) for workspace layout, build and test commands,
repository maintenance, and sample generation rules.
