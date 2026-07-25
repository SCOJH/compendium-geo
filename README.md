# Compendium — geo domain

Geospatial domain repo for the [Compendium](https://github.com/sassy-solutions/compendium)
framework (repo-per-domain topology, ADR-0007): the provider-agnostic geo abstraction and
its adapters live together, so a port change and the adapters that implement it ship in
one atomic PR.

## Packages

| Package | Project | Description |
|---------|---------|-------------|
| `Compendium.Abstractions.Geo` | `src/Compendium.Abstractions.Geo` | Ports: `IGeocoder` (address ↔ coordinates) and `IRouteMatrixProvider` (real road distance + duration for every origin × destination pair), plus `GeoErrors` and the geo models (`GeoCoordinate`, `GeocodeResult`, `RouteMatrix`, `RouteMatrixOptions`, `TravelMode`). |
| `Compendium.Adapters.GoogleMaps` | `src/Compendium.Adapters.GoogleMaps` | Google Maps Platform adapter: `IGeocoder` → Geocoding API, `IRouteMatrixProvider` → Routes API (`computeRouteMatrix`). |

Adapters reference the abstraction via `ProjectReference` — the abstraction no longer
comes from a local NuGet feed. Both projects pack and publish from the same tag.

## Usage

```csharp
services.AddGoogleMaps(o => o.ApiKey = config["GoogleMaps:ApiKey"]!);

// geocode
var loc = await geocoder.GeocodeAsync("Grand-Place, Brussels");

// real road distances for a route optimizer
var matrix = await routes.GetMatrixAsync(origins, destinations,
    new RouteMatrixOptions(TravelMode.Driving));
```

All operations return `Result<T>` (Compendium Result pattern — no exceptions for control flow).

## Build

```bash
dotnet build -c Release
dotnet test -c Release --filter "FullyQualifiedName!~IntegrationTests"
```

- .NET 9 / C# 13, `TreatWarningsAsErrors`, central package management.
- Versioning: MinVer from git tags (`v*`). Release tags publish to GitHub Packages
  (primary) and nuget.org (when `NUGET_API_KEY` is configured).
- Version train: `1.1.x` (above the framework's `1.0.x` so domain-repo packages win resolution).

## Provenance

Assembled from the framework repo and the standalone adapter repos — see
[MIGRATION.md](MIGRATION.md) for source SHAs and the changes made during assembly.
