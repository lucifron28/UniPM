using UniPM.Api.Data.Seeding;

namespace UniPM.Api.Tests.Seeding;

public sealed class SyntheticMaintenanceCommandParserTests
{
    [Fact]
    public void Parse_returns_none_without_a_seed_flag()
    {
        Assert.Equal(
            SyntheticMaintenanceCommand.None,
            SyntheticMaintenanceCommandParser.Parse([]));
    }

    [Fact]
    public void Parse_returns_the_requested_single_command()
    {
        Assert.Equal(
            SyntheticMaintenanceCommand.Seed,
            SyntheticMaintenanceCommandParser.Parse(["--seed-synthetic"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Reset,
            SyntheticMaintenanceCommandParser.Parse(["--reset-synthetic-seed"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Rebuild,
            SyntheticMaintenanceCommandParser.Parse(["--rebuild-maintenance-search-documents"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.RebuildEmbeddings,
            SyntheticMaintenanceCommandParser.Parse(["--rebuild-maintenance-embeddings"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Migrate,
            SyntheticMaintenanceCommandParser.Parse(["--migrate-database"]));
    }

    [Fact]
    public void Parse_rejects_multiple_maintenance_commands()
    {
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--seed-synthetic", "--reset-synthetic-seed"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--seed-synthetic", "--rebuild-maintenance-search-documents"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--reset-synthetic-seed", "--rebuild-maintenance-search-documents"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--rebuild-maintenance-search-documents", "--rebuild-maintenance-embeddings"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse([
                "--seed-synthetic",
                "--reset-synthetic-seed",
                "--rebuild-maintenance-search-documents",
                "--rebuild-maintenance-embeddings"]));
    }
}
