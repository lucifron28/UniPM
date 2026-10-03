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
            SyntheticMaintenanceCommand.RebuildInstitutionalReferenceEmbeddings,
            SyntheticMaintenanceCommandParser.Parse(["--rebuild-institutional-reference-embeddings"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Migrate,
            SyntheticMaintenanceCommandParser.Parse(["--migrate-database"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.SeedDevelopmentUsers,
            SyntheticMaintenanceCommandParser.Parse(["--seed-development-users"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.SeedDemo,
            SyntheticMaintenanceCommandParser.Parse(["--seed-demo"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.ResetDemo,
            SyntheticMaintenanceCommandParser.Parse(["--reset-demo"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.SeedReferenceDocuments,
            SyntheticMaintenanceCommandParser.Parse(["--seed-reference-documents"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.ResetReferenceDocuments,
            SyntheticMaintenanceCommandParser.Parse(["--reset-reference-documents"]));
    }

    [Fact]
    public void Parse_ignores_retired_maintenance_rebuild_commands()
    {
        Assert.Equal(
            SyntheticMaintenanceCommand.None,
            SyntheticMaintenanceCommandParser.Parse(["--rebuild-maintenance-search-documents"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.None,
            SyntheticMaintenanceCommandParser.Parse(["--rebuild-maintenance-embeddings"]));
    }

    [Fact]
    public void Parse_rejects_multiple_commands()
    {
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--seed-synthetic", "--reset-synthetic-seed"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--seed-development-users", "--seed-synthetic"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse([
                "--seed-synthetic",
                "--reset-synthetic-seed",
                "--seed-reference-documents"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--seed-reference-documents", "--seed-synthetic"]));
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse([
                "--rebuild-institutional-reference-embeddings",
                "--seed-reference-documents"]));
    }
}
