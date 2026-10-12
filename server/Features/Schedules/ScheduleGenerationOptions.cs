namespace UniPM.Api.Features.Schedules;

public sealed class ScheduleGenerationOptions
{
    public const string SectionName = "ScheduleGeneration";

    /// <summary>
    /// Approved local calendar date from which UniPM may create PM obligations.
    /// Keep unset until GSD approves the scheduling coverage boundary.
    /// </summary>
    public string? EffectiveDate { get; set; }
}
