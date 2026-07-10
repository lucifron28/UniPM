namespace UniPM.Api.Tests.Seeding;

internal static class SyntheticFixturePaths
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    public static string OperationalFixture => Path.Combine(
        RepositoryRoot,
        "server",
        "Data",
        "Seeding",
        "Resources",
        "synthetic-maintenance-v1.json");

    public static string OperationalSchema => Path.Combine(
        RepositoryRoot,
        "server",
        "Data",
        "Seeding",
        "Resources",
        "synthetic-maintenance-v1.schema.json");

    public static string EvaluationFixture => Path.Combine(
        RepositoryRoot,
        "tests",
        "UniPM.Api.Tests",
        "Retrieval",
        "Fixtures",
        "retrieval-evaluation-v1.json");

    public static string EvaluationSchema => Path.Combine(
        RepositoryRoot,
        "tests",
        "UniPM.Api.Tests",
        "Retrieval",
        "Fixtures",
        "retrieval-evaluation-v1.schema.json");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniPM.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the UniPM repository root for fixture tests.");
    }
}
