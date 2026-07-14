using System.Text.Json;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.MaintenanceReview;

internal interface ISummaryExperimentCapture
{
    void Capture(string generatedText);
}

internal sealed class NullSummaryExperimentCapture : ISummaryExperimentCapture
{
    public void Capture(string generatedText)
    {
    }
}

internal sealed class FileSummaryExperimentCapture(
    IHostEnvironment environment,
    IOptions<SummaryExperimentOptions> optionsAccessor)
    : ISummaryExperimentCapture
{
    private readonly SummaryExperimentOptions options = optionsAccessor.Value;
    private readonly object writeLock = new();

    public void Capture(string generatedText)
    {
        if (!environment.IsDevelopment() || !options.CaptureGeneratedText || string.IsNullOrWhiteSpace(options.CapturePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(options.CapturePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Summary experiment capture path must include a directory.");
        }

        lock (writeLock)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                options.CapturePath,
                JsonSerializer.Serialize(new { generatedText }) + Environment.NewLine);
        }
    }
}

public sealed class SummaryExperimentOptions
{
    public const string SectionName = "SummaryExperiment";

    public bool CaptureGeneratedText { get; set; }
    public string? CapturePath { get; set; }
}
