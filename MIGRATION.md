# Migration — geo domain repo assembly

This repository was assembled from the following sources on 2026-07-25
(repo-per-domain topology, ADR-0007). Source repos were extracted read-only via
`git archive`; nothing was modified upstream.

| Component | Source repo | Source ref / HEAD SHA | Notes |
|-----------|-------------|-----------------------|-------|
| `src/Compendium.Abstractions.Geo` | `sassy-solutions/compendium` (framework) | `ce2b7634230f82dba421e89c8b73e1aeb5a826bf` (local branch `feat/messaging-channels`, commit "feat(geo): add Compendium.Abstractions.Geo") | **Not on `origin/main`** (`792dd626…`): the Geo abstraction was authored on a local branch and never pushed. Extracted from that committed tree. In-framework `ProjectReference` to `Compendium.Abstractions` replaced by the nuget.org `PackageReference` (1.0.5-preview.1); explicit `IsPackable=true` added (the framework's root props provided it before). PackageId unchanged. |
| `src/Compendium.Adapters.GoogleMaps` | `sassy-solutions/compendium-adapter-google-maps` | `298d012c1ef55e9354f2a6b34537093bbce8d560` (`main`) | `PackageReference Compendium.Abstractions.Geo` (1.0.4, local `../.local-nuget` feed) replaced by an in-repo `ProjectReference` — this removes the local-feed bootstrap documented in the old README. PackageId unchanged. |
| `tests/Compendium.Adapters.GoogleMaps.Tests` | `sassy-solutions/compendium-adapter-google-maps` | `298d012c1ef55e9354f2a6b34537093bbce8d560` (`main`) | Unchanged (5 unit tests). |
| Scaffold (`Directory.Build.props`, `global.json`, `.config/dotnet-tools.json`, workflows) | `sassy-solutions/compendium-adapter-supabase` | working tree (freshest release.yml scaffold) | Repo URLs re-pointed to `SCOJH/geo`; release.yml packs the abstraction + adapters (nupkg assert `-ge 2`); ci.yml coverage gate set to 75% (assembly baseline: 76.9%). |

## Version pins

- Compendium base packages (`Core`, `Abstractions`, `Multitenancy`, `Testing`):
  nuget.org `1.0.5-preview.1` (bumped from the adapter's `1.0.4` pin; compiles clean, no API drift).
- `Directory.Packages.props` is the union of the source repos' pins; conflicts resolved by
  taking the highest (`Microsoft.NET.Test.Sdk` 18.4.0, `xunit.runner.visualstudio` 3.1.5,
  `coverlet.collector` 6.0.4).

## Version train

This repo tags from `v1.1.0-preview.1` — deliberately ABOVE the framework's `1.0.x`
train so domain-repo packages win NuGet resolution over any historical framework-built
or local-feed versions of the same package IDs.

## Later steps (not part of this assembly)

- Remove `src/Abstractions/Compendium.Abstractions.Geo` from the framework repo.
- Archive `compendium-adapter-google-maps`.
