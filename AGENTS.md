# RedBolt monorepo workflow

Use `RedBolt.slnx` as the entry point for aggregate builds and tests. Use `RedBolt/RedBolt.slnx` for core-only iteration instead of building the default full graph.

## Fast iteration

- Keep existing outputs by using `--no-restore` after the solution has been restored.
- Select one configuration, platform, and architecture with MSBuild properties when the change does not require the full graph.
- Run the aggregate solution for final validation.

Examples:

```sh
# Core only, local desktop target.
dotnet build RedBolt/RedBolt.slnx --configuration Debug --no-restore \
	-p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=linux \
	-p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64 \
	-p:RedBoltTestsEnabled=false

# One full external-module prerequisite check.
dotnet build RedBolt.slnx --configuration Debug --no-restore \
	-p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=linux \
	-p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64 \
	-p:RedBoltTestsEnabled=false

# Reuse existing build outputs for a focused test pass.
dotnet test RedBolt.slnx --configuration Debug --no-build --no-restore \
	--filter 'FullyQualifiedName~PhysicsJolt'
```

Use `dotnet build RedBolt.slnx --configuration Debug` for the aggregate build.

## Web samples

Run `./monorepo.cs publish-samples --build --build-config Debug` for a local build-and-publish pass. Use `Release` for the complete deployed catalog. The command refreshes `RedBoltFramework.github.io/samples/` and `catalog.json`.

Repository-specific instructions in nested `AGENTS.md` files still apply when editing those repositories.
