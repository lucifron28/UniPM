namespace UniPM.Api.Data.Seeding;

internal enum SyntheticMaintenanceCommand
{
    None,
    Seed,
    Reset,
    Ambiguous
}

internal static class SyntheticMaintenanceCommandParser
{
    public static SyntheticMaintenanceCommand Parse(IEnumerable<string> commandLineArguments)
    {
        var seedRequested = commandLineArguments.Contains("--seed-synthetic", StringComparer.Ordinal);
        var resetRequested = commandLineArguments.Contains("--reset-synthetic-seed", StringComparer.Ordinal);

        if (seedRequested && resetRequested)
        {
            return SyntheticMaintenanceCommand.Ambiguous;
        }

        if (seedRequested)
        {
            return SyntheticMaintenanceCommand.Seed;
        }

        return resetRequested ? SyntheticMaintenanceCommand.Reset : SyntheticMaintenanceCommand.None;
    }
}
