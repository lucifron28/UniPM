namespace UniPM.Api.Features.Inspections;

internal sealed record InspectionLocationClassification(double? DistanceMeters, string Outcome);

internal static class InspectionLocationClassifier
{
    private const double MeanEarthRadiusMeters = 6_371_000;

    public static InspectionLocationClassification Classify(
        double? expectedLatitude,
        double? expectedLongitude,
        double? expectedRadiusMeters,
        double measuredLatitude,
        double measuredLongitude,
        double accuracyMeters)
    {
        var configuredCount = (expectedLatitude.HasValue ? 1 : 0)
            + (expectedLongitude.HasValue ? 1 : 0)
            + (expectedRadiusMeters.HasValue ? 1 : 0);
        if (configuredCount == 0)
        {
            return new InspectionLocationClassification(null, "NotConfigured");
        }

        if (configuredCount != 3)
        {
            throw new ArgumentException("Verification location values must be configured together.");
        }

        var distance = HaversineDistanceMeters(
            expectedLatitude!.Value,
            expectedLongitude!.Value,
            measuredLatitude,
            measuredLongitude);
        var radius = expectedRadiusMeters!.Value;
        var outcome = distance + accuracyMeters <= radius
            ? "Inside"
            : distance - accuracyMeters > radius
                ? "Outside"
                : "Uncertain";

        return new InspectionLocationClassification(distance, outcome);
    }

    public static double HaversineDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var latitude1Radians = DegreesToRadians(latitude1);
        var latitude2Radians = DegreesToRadians(latitude2);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(latitude1Radians)
            * Math.Cos(latitude2Radians)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        var boundedHaversine = Math.Clamp(haversine, 0, 1);

        return MeanEarthRadiusMeters
            * 2
            * Math.Atan2(Math.Sqrt(boundedHaversine), Math.Sqrt(1 - boundedHaversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180);
}
