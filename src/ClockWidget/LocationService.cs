using Windows.Devices.Geolocation;

namespace ClockWidget;

public interface ILocationService
{
    Task<(double Latitude, double Longitude)?> LocateAsync(CancellationToken token, bool requestPermission = false);
}
public sealed class LocationService : ILocationService
{
    public async Task<(double Latitude, double Longitude)?> LocateAsync(CancellationToken token, bool requestPermission = false)
    {
        // Only a visible, explicit settings action may initiate a permission prompt.
        // Startup/hourly/resume refreshes reuse Windows' existing grant and handle denial.
        if (requestPermission && await Geolocator.RequestAccessAsync() != GeolocationAccessStatus.Allowed) return null;
        var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
        var operation = locator.GetGeopositionAsync(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(15));
        using var registration = token.Register(operation.Cancel);
        var position = await operation;
        token.ThrowIfCancellationRequested();
        return (Math.Round(position.Coordinate.Point.Position.Latitude, 2), Math.Round(position.Coordinate.Point.Position.Longitude, 2));
    }
}
