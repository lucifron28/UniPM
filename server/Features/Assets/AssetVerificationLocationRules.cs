namespace UniPM.Api.Features.Assets;

internal static class AssetVerificationLocationRules
{
    public static void AddConfigurationErrors(
        double? latitude,
        double? longitude,
        double? radiusMeters,
        IDictionary<string, string[]> errors)
    {
        var suppliedCount = (latitude.HasValue ? 1 : 0)
            + (longitude.HasValue ? 1 : 0)
            + (radiusMeters.HasValue ? 1 : 0);
        if (suppliedCount is > 0 and < 3)
        {
            const string message = "Latitude, longitude, and radius must be provided together.";
            if (!latitude.HasValue) errors["VerificationLatitude"] = [message];
            if (!longitude.HasValue) errors["VerificationLongitude"] = [message];
            if (!radiusMeters.HasValue) errors["VerificationRadiusMeters"] = [message];
        }

        if (latitude is { } latitudeValue && !IsValidLatitude(latitudeValue))
        {
            errors["VerificationLatitude"] = ["Verification latitude must be between -90 and 90."];
        }

        if (longitude is { } longitudeValue && !IsValidLongitude(longitudeValue))
        {
            errors["VerificationLongitude"] = ["Verification longitude must be between -180 and 180."];
        }

        if (radiusMeters is { } radiusValue && (!double.IsFinite(radiusValue) || radiusValue <= 0))
        {
            errors["VerificationRadiusMeters"] = ["Verification radius must be greater than zero."];
        }
    }

    public static bool IsValidLatitude(double value)
        => double.IsFinite(value) && value is >= -90 and <= 90;

    public static bool IsValidLongitude(double value)
        => double.IsFinite(value) && value is >= -180 and <= 180;
}
