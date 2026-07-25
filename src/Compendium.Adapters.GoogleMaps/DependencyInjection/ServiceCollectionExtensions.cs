// -----------------------------------------------------------------------
// <copyright file="ServiceCollectionExtensions.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace Compendium.Adapters.GoogleMaps.DependencyInjection;

/// <summary>DI extensions for the Google Maps geospatial adapter.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GoogleGeocoder"/> as <see cref="IGeocoder"/> and
    /// <see cref="GoogleRouteMatrixProvider"/> as <see cref="IRouteMatrixProvider"/>, wiring the
    /// shared named HttpClient and (optionally) <see cref="GoogleMapsOptions"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options configurator (at minimum, the API key).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGoogleMaps(
        this IServiceCollection services,
        Action<GoogleMapsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(GoogleGeocoder.HttpClientName);

        var optionsBuilder = services.AddOptions<GoogleMapsOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.AddSingleton<IGeocoder, GoogleGeocoder>();
        services.AddSingleton<IRouteMatrixProvider, GoogleRouteMatrixProvider>();
        return services;
    }
}
