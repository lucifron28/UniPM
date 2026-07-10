using System.Text.Json;

namespace UniPM.Api.Data.Seeding;

public sealed class SyntheticMaintenanceDatasetLoader(
    SyntheticMaintenanceSeedOptions options,
    SyntheticMaintenanceDatasetValidator validator)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SyntheticMaintenanceDataset> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.DatasetPath))
        {
            throw new FileNotFoundException(
                "The synthetic maintenance fixture was not found in the application resources.",
                options.DatasetPath);
        }

        await using var stream = File.OpenRead(options.DatasetPath);
        var dataset = await JsonSerializer.DeserializeAsync<SyntheticMaintenanceDataset>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (dataset is null)
        {
            throw new SyntheticMaintenanceFixtureException("The synthetic maintenance fixture is empty.");
        }

        validator.Validate(dataset);
        return dataset;
    }
}
