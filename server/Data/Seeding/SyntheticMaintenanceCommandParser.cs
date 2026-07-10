namespace UniPM.Api.Data.Seeding;

internal enum SyntheticMaintenanceCommand
{
    None,
    Seed,
    Reset,
    Rebuild,
    Ambiguous
}

internal static class SyntheticMaintenanceCommandParser
{
    public static SyntheticMaintenanceCommand Parse(IEnumerable<string> commandLineArguments)
    {
        var arguments = commandLineArguments.ToHashSet(StringComparer.Ordinal);
        var requestedCommands = new List<SyntheticMaintenanceCommand>();

        if (arguments.Contains("--seed-synthetic"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.Seed);
        }

        if (arguments.Contains("--reset-synthetic-seed"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.Reset);
        }

        if (arguments.Contains("--rebuild-maintenance-search-documents"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.Rebuild);
        }

        return requestedCommands.Count switch
        {
            0 => SyntheticMaintenanceCommand.None,
            1 => requestedCommands[0],
            _ => SyntheticMaintenanceCommand.Ambiguous
        };
    }
}
