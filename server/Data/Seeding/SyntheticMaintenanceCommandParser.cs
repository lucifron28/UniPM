namespace UniPM.Api.Data.Seeding;

internal enum SyntheticMaintenanceCommand
{
    None,
    Seed,
    Reset,
    Rebuild,
    RebuildEmbeddings,
    Migrate,
    SeedDevelopmentUsers,
    SeedReferenceDocuments,
    ResetReferenceDocuments,
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

        if (arguments.Contains("--rebuild-maintenance-embeddings"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.RebuildEmbeddings);
        }

        if (arguments.Contains("--migrate-database"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.Migrate);
        }

        if (arguments.Contains("--seed-development-users"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.SeedDevelopmentUsers);
        }

        if (arguments.Contains("--seed-reference-documents"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.SeedReferenceDocuments);
        }

        if (arguments.Contains("--reset-reference-documents"))
        {
            requestedCommands.Add(SyntheticMaintenanceCommand.ResetReferenceDocuments);
        }

        return requestedCommands.Count switch
        {
            0 => SyntheticMaintenanceCommand.None,
            1 => requestedCommands[0],
            _ => SyntheticMaintenanceCommand.Ambiguous
        };
    }
}
