namespace UniPM.Api.Features.Schedules;

public sealed class PreventiveMaintenanceScheduleGenerationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PreventiveMaintenanceScheduleGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateOnly? lastSuccessfulCheck = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = timeProvider.GetUtcNow();
            var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
            var institutionalDate = DateOnly.FromDateTime(institutionalNow.DateTime);
            var succeeded = false;

            if (lastSuccessfulCheck != institutionalDate)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var generator = scope.ServiceProvider
                        .GetRequiredService<PreventiveMaintenanceScheduleGenerationService>();
                    var result = await generator.EnsureYearAsync(
                        institutionalNow.Year,
                        now,
                        stoppingToken);
                    logger.LogInformation(
                        "Preventive-maintenance schedule recovery checked {Year}: {CreatedSchedules} schedules added.",
                        result.Year,
                        result.CreatedSchedules);
                    if (result.CyclesRequiringGsdCoverageReview > 0)
                    {
                        logger.LogWarning(
                            "Skipped {CyclesRequiringGsdCoverageReview} missing current-year cycles before the current month because ScheduleGeneration:EffectiveDate is not configured. GSD must confirm the approved coverage date before catch-up generation.",
                            result.CyclesRequiringGsdCoverageReview);
                    }

                    lastSuccessfulCheck = institutionalDate;
                    succeeded = true;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        "Preventive-maintenance schedule recovery failed ({ExceptionType}).",
                        exception.GetType().Name);
                }
            }

            var delay = succeeded || lastSuccessfulCheck == institutionalDate
                ? TimeSpan.FromHours(24)
                : TimeSpan.FromHours(1);
            await Task.Delay(delay, timeProvider, stoppingToken);
        }
    }
}
