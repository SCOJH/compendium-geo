# Changelog

All notable changes to the Compendium geo domain (`Compendium.Abstractions.Geo`,
`Compendium.Adapters.GoogleMaps`) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **Both packages now target `net9.0` and `net10.0`.** .NET 9 (STS) leaves support
  on 2026-11-10; .NET 10 is the LTS consumers are moving to, and `net9.0` stays for
  the transition only. `global.json` moves to the .NET 10 SDK (10.0.100 or any later
  10.0 feature band), which builds both targets; the CI and release workflows install
  the .NET 9 SDK beside it for its runtime, so the tests run once per target. On
  `net10.0` the `Microsoft.Extensions.*` family resolves to 10.0.x; `net9.0` keeps the
  9.0.16 versions it already shipped with. `Compendium.Adapters.GoogleMaps` no longer
  references the `System.Text.Json` package, which the shared framework provides on
  both targets, so that id disappears from its nuspec dependencies; the assembly was
  already coming from the framework, so a `net9.0` consumer's graph loses that node
  with no effect at compile or run time. The Compendium core packages stay at
  `1.0.5-preview.1` (their `net9.0` assets serve `net10.0`). (NET10-01,
  agent-spot/backlog#363)
