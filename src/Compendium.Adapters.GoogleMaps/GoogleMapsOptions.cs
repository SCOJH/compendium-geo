// -----------------------------------------------------------------------
// <copyright file="GoogleMapsOptions.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Compendium.Adapters.GoogleMaps;

/// <summary>Options for the Google Maps Platform adapter.</summary>
public sealed class GoogleMapsOptions
{
    /// <summary>The configuration section name (<c>GoogleMaps</c>).</summary>
    public const string SectionName = "GoogleMaps";

    /// <summary>The Google Maps Platform API key (Geocoding + Distance Matrix APIs enabled).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The Geocoding API base URL. Overridable for testing.</summary>
    public string BaseUrl { get; set; } = "https://maps.googleapis.com";

    /// <summary>The Routes API base URL (used for the route matrix). Overridable for testing.</summary>
    public string RoutesBaseUrl { get; set; } = "https://routes.googleapis.com";
}
