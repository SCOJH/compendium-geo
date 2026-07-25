// -----------------------------------------------------------------------
// <copyright file="GoogleGeocoder.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Compendium.Adapters.GoogleMaps;

/// <summary><see cref="IGeocoder"/> backed by the Google Geocoding API.</summary>
public sealed class GoogleGeocoder : IGeocoder
{
    /// <summary>The named <see cref="HttpClient"/> shared by the Google Maps adapters.</summary>
    public const string HttpClientName = "compendium-google-maps";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleGeocoder> _logger;

    /// <summary>Initialises a new instance.</summary>
    public GoogleGeocoder(
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleMapsOptions> options,
        ILogger<GoogleGeocoder> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<GeocodeResult>> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Task.FromResult(Result.Failure<GeocodeResult>(GeoErrors.InvalidInput("Address is required.")));
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Task.FromResult(Result.Failure<GeocodeResult>(GeoErrors.MissingApiKey()));
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/maps/api/geocode/json"
                  + $"?address={Uri.EscapeDataString(address)}&key={_options.ApiKey}";
        return GetGeocodeAsync(url, address, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<GeocodeResult>> ReverseGeocodeAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Task.FromResult(Result.Failure<GeocodeResult>(GeoErrors.MissingApiKey()));
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/maps/api/geocode/json"
                  + $"?latlng={Uri.EscapeDataString(coordinate.ToString())}&key={_options.ApiKey}";
        return GetGeocodeAsync(url, coordinate.ToString(), cancellationToken);
    }

    private async Task<Result<GeocodeResult>> GetGeocodeAsync(string url, string label, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Google geocoding transport failure");
            return Result.Failure<GeocodeResult>(GeoErrors.ProviderError("transport failure"));
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Result.Failure<GeocodeResult>(GeoErrors.Throttled());
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<GeocodeResult>(GeoErrors.ProviderError($"HTTP {(int)response.StatusCode}"));
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;

                if (status == "OVER_QUERY_LIMIT")
                {
                    return Result.Failure<GeocodeResult>(GeoErrors.Throttled());
                }

                if (status != "OK"
                    || !root.TryGetProperty("results", out var results)
                    || results.ValueKind != JsonValueKind.Array
                    || results.GetArrayLength() == 0)
                {
                    return Result.Failure<GeocodeResult>(GeoErrors.AddressNotFound(label));
                }

                var first = results[0];
                var location = first.GetProperty("geometry").GetProperty("location");
                var coordinate = new GeoCoordinate(
                    location.GetProperty("lat").GetDouble(),
                    location.GetProperty("lng").GetDouble());

                var formatted = first.TryGetProperty("formatted_address", out var fa)
                    ? fa.GetString() ?? string.Empty
                    : string.Empty;
                var placeId = first.TryGetProperty("place_id", out var pid) ? pid.GetString() : null;

                return Result.Success(new GeocodeResult(coordinate, formatted, placeId));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Google geocoding returned malformed JSON");
                return Result.Failure<GeocodeResult>(GeoErrors.ProviderError("malformed response"));
            }
        }
    }
}
