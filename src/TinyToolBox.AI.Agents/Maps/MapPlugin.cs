using System.ComponentModel;
using Azure.Core.GeoJson;
using Azure.Maps.Search;
using Azure.Maps.Search.Models;
using Microsoft.SemanticKernel;

namespace TinyToolBox.AI.Agents.Maps;

public sealed class MapPlugin(MapsSearchClient mapsSearchClient)
{
    private const int DefaultResultSize = 5;

    [KernelFunction(nameof(GetPosition))]
    [Description("Get GPS latitude and longitude for a given postal address, postcode, suburbs in Australia")]
    public async Task<GeoPosition?> GetPosition(
        [Description("Postal address, postcode, suburbs in Australia to search for")]
        string location,
        CancellationToken cancellationToken = default)
    {
        var response = await mapsSearchClient.GetGeocodingAsync(
            location,
            new GeocodingQuery
            {
                Top = DefaultResultSize
            },
            cancellationToken);

        return response.Value.Features.Count > 0
            ? response.Value.Features[0].Geometry.Coordinates
            : default;
    }

    [KernelFunction(nameof(GetAddress))]
    [Description("Get the address for a given GPS latitude and longitude in Australia")]
    public async Task<string?> GetAddress(
        [Description("GPS latitude")] double latitude,
        [Description("GPS longitude")] double longitude,
        CancellationToken cancellationToken = default)
    {
        var coordinates = new GeoPosition(longitude, latitude);
        var response = await mapsSearchClient.GetReverseGeocodingAsync(
            coordinates,
            new ReverseGeocodingQuery
            {
                ResultTypes = [ReverseGeocodingResultTypeEnum.Address]
            },
            cancellationToken);

        return response.Value.Features.Count > 0
            ? response.Value.Features[0].Properties.Address.FormattedAddress
            : default;
    }
}