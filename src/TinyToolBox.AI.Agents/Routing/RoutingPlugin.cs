using System.ComponentModel;
using Azure.Core.GeoJson;
using Azure.Maps.Routing;
using Microsoft.SemanticKernel;

namespace TinyToolBox.AI.Agents.Routing;

internal sealed class RoutingPlugin(MapsRoutingClient mapsRoutingClient)
{
    [KernelFunction(nameof(GetRouteDirection))]
    [Description("Get GPS route directions for a given GPS origin and a GPS destination in Australia")]
    public async Task<IReadOnlyCollection<GeoPosition>> GetRouteDirection(
        [Description("The original GPS latitude and longitude to route from")]
        GeoPosition origin, 
        [Description("The destination GPS latitude and longitude to route to")]
        GeoPosition destination,
        CancellationToken cancellationToken = default)
    {
        var query = new RouteDirectionQuery([ origin, destination ]);
        var response = await mapsRoutingClient.GetDirectionsAsync(query, cancellationToken);
        
        var routePoints = new List<GeoPosition>();
        foreach (var leg in response.Value.Routes[0].Legs)
        {
            routePoints.AddRange(leg.Points);
        }

        return routePoints;
    }
}