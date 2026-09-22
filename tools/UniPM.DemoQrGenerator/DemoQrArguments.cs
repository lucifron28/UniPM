namespace UniPM.DemoQrGenerator;

internal static class DemoQrArguments
{
    internal static string ParseOutputDirectory(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return Path.Combine("reference", "demo", "qr");
        }

        if (arguments.Count == 2
            && string.Equals(arguments[0], "--output", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(arguments[1]))
        {
            return arguments[1];
        }

        throw new ArgumentException("Usage: UniPM.DemoQrGenerator [--output <directory>]");
    }
}
