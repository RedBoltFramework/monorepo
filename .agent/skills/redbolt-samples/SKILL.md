---
name: redbolt-samples
description: Maintain RedBolt sample catalogs, host templates, generated hosts, and published WebAssembly or Android sample bundles across the monorepo.
---

# RedBolt samples

Use this skill for changes to sample catalogs, sample source directories, core
sample hosts, generated module hosts, or the unified WebAssembly and Android
sample bundle.

## Catalogs

- Store each module's catalog in `src/RedBolt/Sample/SampleCatalog.json`.
- Update the catalog entry and matching directory under `src/RedBolt/Sample/`
  when adding, removing, or renaming a sample.
- Use catalog fields as follows:
  - `web` and `android`: enable the module host for each platform.
  - `samples`: define `id`, display `name`, `applicationType`, and source
    directory. Generated lists sort by display name.
  - `platforms`: restrict an entry to `web` or `android`; an empty or missing
    list enables both supported platforms.
  - `projectReferences`, `additionalSources`, and platform-specific fields: add
    build inputs.
  - `coreData` and `data`: select files for packaging and runtime mounting.

## Hosts and generation

- Treat `RedBolt/src/RedBolt/Sample/Web/` and
  `RedBolt/src/RedBolt/Sample/Android/` as working core hosts and templates for
  generated hosts.
- Preserve every unique `<redbolt-generated name="...">` marker. The generator
  replaces only marked regions; hand-written code outside them survives a core
  self-update.
- Do not manually edit generated hosts in external modules or `Samples/`.
- Regenerate all module-local hosts after catalog, sample source, data, or core
  template changes:

  ```sh
  ./monorepo.cs generate-samples
  ```

- Regenerate selected module hosts with:

  ```sh
  ./monorepo.cs generate-samples --modules RedBolt.RmlUI
  ```

- Check committed generated hosts with:

  ```sh
  ./monorepo.cs generate-samples --check
  ```

- Generate a custom unified Web and Android host with an output directory, host
  name, and module list:

  ```sh
  ./monorepo.cs generate-samples \
    --output Samples --name all \
    --modules RedBolt RedBolt.2D RedBolt.Actions RedBolt.CSG
  ```

- Combine native modules only when a selected module owns the other native
  modules as dependencies. Reject incompatible native roots.
- Keep module data in separate `.pak` files and merge it through the runtime
  virtual file system.

## Publishing

- Use `PublishedSampleBundle` in `monorepo.cs` as the only supported published
  bundle. Change its module list to change the unified website and Android
  catalogs.
- Build and stage the unified Release WebAssembly catalog with:

  ```sh
  ./monorepo.cs publish-samples --build
  ```

- Use `--build-config Debug` for local build-and-stage work. Use `Release` for
  release preparation.
- Local staging refreshes the website checkout but does not push or deploy it.
- `--build` regenerates and builds the supported monorepo host under `Samples/`,
  copies the bundle to `RedBoltFramework.github.io/samples/`, and rebuilds
  `catalog.json` from the selected module catalogs.
- Release WebAssembly builds use AOT. Use `--build-config Debug` for faster
  local WebAssembly builds without AOT.
- Android generation does not deploy the Web catalog.
