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
    }

    [Fact]
    public void Parse_rejects_both_seed_flags()
    {
        Assert.Equal(
            SyntheticMaintenanceCommand.Ambiguous,
            SyntheticMaintenanceCommandParser.Parse(["--seed-synthetic", "--reset-synthetic-seed"]));
    }
}
