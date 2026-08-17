# RedBolt monorepo workflow

Use `./ci.cs` as the entry point for builds and tests. During iteration, select the smallest useful graph instead of running the default full matrix.

## Fast iteration

- Keep existing outputs with `--incremental`.
- Select one or more repositories with `--module`; selecting an external module automatically builds `RedBolt` first and reuses those core outputs.
- Select one configuration, platform, and architecture. Disable tests when the change does not require them.
- Run the full default matrix only for final CI validation.

Examples:

```sh
# Core only, local desktop target.
./ci.cs --incremental --module RedBolt --build-config Debug --test-config none --platform linux --architectures x64

# One external module plus its core prerequisite.
./ci.cs --incremental --module RedBolt.PhysicsJolt --build-config Debug --test-config none --platform linux --architectures x64

# Reuse existing build outputs for a focused test pass.
./ci.cs --incremental --build-config none --test-config Debug --tests 'FullyQualifiedName~PhysicsJolt'
```

Pass extra `dotnet test` arguments after `--`. Use `./ci.cs --help` for all selectors.

## Web samples

Run `./monorepo.cs publish-samples --build --module RedBolt --build-config Debug` for a focused build-and-publish pass. Omit `--module` and use `Release` for the complete deployed catalog. The command refreshes `RedBoltFramework.github.io/samples/` and `catalog.json`.

Repository-specific instructions in nested `AGENTS.md` files still apply when editing those repositories.
