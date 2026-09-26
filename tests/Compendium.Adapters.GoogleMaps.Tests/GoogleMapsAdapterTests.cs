// -----------------------------------------------------------------------
// <copyright file="GoogleMapsAdapterTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

using System.Net;
using System.Text;
using Compendium.Abstractions.Geo.Models;
using Compendium.Adapters.GoogleMaps;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Compendium.Adapters.GoogleMaps.Tests;

public sealed class GoogleMapsAdapterTests
{
    private static IOptions<GoogleMapsOptions> Opts(string key = "test-key") =>
        Options.Create(new GoogleMapsOptions { ApiKey = key });

    [Fact]
    public async Task GeocodeAsync_ParsesLocation()
    {
        const string json =
            """
            {"status":"OK","results":[{"formatted_address":"Grand-Place, 1000 Bruxelles",
             "place_id":"ChIJ123","geometry":{"location":{"lat":50.8466,"lng":4.3525}}}]}
            """;
        var geocoder = new GoogleGeocoder(Factory(json), Opts(), NullLogger<GoogleGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("Grand-Place Brussels");

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : null);
        result.Value.Coordinate.Should().Be(new GeoCoordinate(50.8466, 4.3525));
        result.Value.FormattedAddress.Should().Contain("Grand-Place");
        result.Value.PlaceId.Should().Be("ChIJ123");
    }

    [Fact]
    public async Task GeocodeAsync_ZeroResults_ReturnsAddressNotFound()
    {
        var geocoder = new GoogleGeocoder(
            Factory("{\"status\":\"ZERO_RESULTS\",\"results\":[]}"), Opts(), NullLogger<GoogleGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("nowhere at all");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geo.AddressNotFound");
    }

    [Fact]
    public async Task GeocodeAsync_NoApiKey_ReturnsMissingApiKey()
    {
        var geocoder = new GoogleGeocoder(Factory("{}"), Opts(key: ""), NullLogger<GoogleGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("Grand-Place");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geo.MissingApiKey");
    }

    [Fact]
    public async Task GetMatrixAsync_ParsesDistancesAndDurations()
    {
        // Google Routes API computeRouteMatrix shape (JSON array; proto3 omits zero values).
        const string json =
            """
            [
              {"condition":"ROUTE_EXISTS"},
              {"originIndex":0,"destinationIndex":1,"distanceMeters":2170,"duration":"300s","condition":"ROUTE_EXISTS"}
            ]
            """;
        var provider = new GoogleRouteMatrixProvider(Factory(json), Opts(), NullLogger<GoogleRouteMatrixProvider>.Instance);

        var origins = new[] { new GeoCoordinate(50.8466, 4.3528) };
        var destinations = new[] { new GeoCoordinate(50.8466, 4.3525), new GeoCoordinate(50.8275, 4.3590) };

        var result = await provider.GetMatrixAsync(origins, destinations);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : null);
        result.Value.OriginCount.Should().Be(1);
        result.Value.DestinationCount.Should().Be(2);
        result.Value.Get(0, 1)!.DistanceMeters.Should().Be(2170);
        result.Value.Get(0, 1)!.DurationSeconds.Should().Be(300);
        result.Value.Get(0, 1)!.Reachable.Should().BeTrue();
    }

    [Fact]
    public async Task GetMatrixAsync_EmptyInput_ReturnsInvalidInput()
    {
        var provider = new GoogleRouteMatrixProvider(Factory("{}"), Opts(), NullLogger<GoogleRouteMatrixProvider>.Instance);

        var result = await provider.GetMatrixAsync(Array.Empty<GeoCoordinate>(), Array.Empty<GeoCoordinate>());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geo.InvalidInput");
    }

    private static IHttpClientFactory Factory(string responseJson) =>
        new StubHttpClientFactory(new StubHandler(HttpStatusCode.OK, responseJson));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
