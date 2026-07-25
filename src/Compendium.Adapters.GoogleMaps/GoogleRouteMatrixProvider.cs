// -----------------------------------------------------------------------
// <copyright file="GoogleRouteMatrixProvider.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Compendium.Adapters.GoogleMaps;

/// <summary>
/// <see cref="IRouteMatrixProvider"/> backed by the Google <b>Routes API</b>
/// (<c>distanceMatrix/v2:computeRouteMatrix</c>) — the current, non-legacy distance-matrix
/// endpoint.
/// </summary>
public sealed class GoogleRouteMatrixProvider : IRouteMatrixProvider
{
    private static readonly JsonSerializerOptions RequestJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleRouteMatrixProvider> _logger;

    /// <summary>Initialises a new instance.</summary>
    public GoogleRouteMatrixProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleMapsOptions> options,
        ILogger<GoogleRouteMatrixProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<RouteMatrix>> GetMatrixAsync(
        IReadOnlyList<GeoCoordinate> origins,
        IReadOnlyList<GeoCoordinate> destinations,
        RouteMatrixOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(destinations);

        if (origins.Count == 0 || destinations.Count == 0)
        {
            return Result.Failure<RouteMatrix>(GeoErrors.InvalidInput("Origins and destinations must be non-empty."));
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Result.Failure<RouteMatrix>(GeoErrors.MissingApiKey());
        }

        options ??= RouteMatrixOptions.Default;

        var travelMode = MapTravelMode(options.Mode);
        var payload = new RouteMatrixRequest
        {
            Origins = origins.Select(ToWaypoint).ToArray(),
            Destinations = destinations.Select(ToWaypoint).ToArray(),
            TravelMode = travelMode,
            RoutingPreference = travelMode == "DRIVE" ? "TRAFFIC_UNAWARE" : null,
        };

        var url = $"{_options.RoutesBaseUrl.TrimEnd('/')}/distanceMatrix/v2:computeRouteMatrix";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: RequestJson),
        };
        request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "originIndex,destinationIndex,distanceMeters,duration,condition");

        var client = _httpClientFactory.CreateClient(GoogleGeocoder.HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Google Routes API transport failure");
            return Result.Failure<RouteMatrix>(GeoErrors.ProviderError("transport failure"));
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Result.Failure<RouteMatrix>(GeoErrors.Throttled());
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Routes API returned {StatusCode}: {Body}", (int)response.StatusCode, Truncate(body));
                return Result.Failure<RouteMatrix>(GeoErrors.ProviderError($"HTTP {(int)response.StatusCode}: {Truncate(body)}"));
            }

            return Parse(body, origins.Count, destinations.Count);
        }
    }

    private Result<RouteMatrix> Parse(string body, int originCount, int destinationCount)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Result.Failure<RouteMatrix>(GeoErrors.ProviderError("unexpected route-matrix response shape"));
            }

            var elements = new List<RouteMatrixElement>(originCount * destinationCount);
            foreach (var cell in document.RootElement.EnumerateArray())
            {
                // proto3 JSON omits zero/default values, so missing == 0.
                var originIndex = cell.TryGetProperty("originIndex", out var oi) ? oi.GetInt32() : 0;
                var destinationIndex = cell.TryGetProperty("destinationIndex", out var di) ? di.GetInt32() : 0;
                var reachable = !cell.TryGetProperty("condition", out var c) || c.GetString() == "ROUTE_EXISTS";
                var distance = cell.TryGetProperty("distanceMeters", out var dm) ? dm.GetDouble() : 0;
                var duration = cell.TryGetProperty("duration", out var du) ? ParseDuration(du.GetString()) : 0;

                elements.Add(new RouteMatrixElement(originIndex, destinationIndex, distance, duration, reachable));
            }

            return Result.Success(new RouteMatrix(originCount, destinationCount, elements));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Google Routes API returned malformed JSON");
            return Result.Failure<RouteMatrix>(GeoErrors.ProviderError("malformed response"));
        }
    }

    private static double ParseDuration(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var trimmed = value.EndsWith('s') ? value[..^1] : value;
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : 0;
    }

    private static Waypoint ToWaypoint(GeoCoordinate c) =>
        new() { WaypointValue = new WaypointLocation { Location = new LatLngLocation { LatLng = new LatLng { Latitude = c.Latitude, Longitude = c.Longitude } } } };

    private static string MapTravelMode(TravelMode mode) => mode switch
    {
        TravelMode.Walking => "WALK",
        TravelMode.Bicycling => "BICYCLE",
        TravelMode.Transit => "TRANSIT",
        _ => "DRIVE",
    };

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];

    private sealed class RouteMatrixRequest
    {
        [JsonPropertyName("origins")] public Waypoint[] Origins { get; init; } = [];

        [JsonPropertyName("destinations")] public Waypoint[] Destinations { get; init; } = [];

        [JsonPropertyName("travelMode")] public string TravelMode { get; init; } = "DRIVE";

        [JsonPropertyName("routingPreference")] public string? RoutingPreference { get; init; }
    }

    private sealed class Waypoint
    {
        [JsonPropertyName("waypoint")] public WaypointLocation WaypointValue { get; init; } = new();
    }

    private sealed class WaypointLocation
    {
        [JsonPropertyName("location")] public LatLngLocation Location { get; init; } = new();
    }

    private sealed class LatLngLocation
    {
        [JsonPropertyName("latLng")] public LatLng LatLng { get; init; } = new();
    }

    private sealed class LatLng
    {
        [JsonPropertyName("latitude")] public double Latitude { get; init; }

        [JsonPropertyName("longitude")] public double Longitude { get; init; }
    }
}
