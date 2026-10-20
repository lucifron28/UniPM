using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Models;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Schedules;

public static class SchedulesEndpoints
{
    private const int AssignmentOptionLimit = 10;
    private const int MaximumAssigneeSearchLength = 100;
    private const int MaximumAssignmentOptionPageSize = 100;

    public static IEndpointRouteBuilder MapSchedulesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/schedules").WithTags("Schedules");

        group.MapPost("/generate", async (
            GenerateScheduleCyclesDto dto,
            PreventiveMaintenanceScheduleGenerationService scheduleGenerator,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var now = timeProvider.GetUtcNow();
            var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
            var validationErrors = dto.Validate(institutionalNow.Year);
            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            ScheduleGenerationResult result;
            try
            {
                result = await scheduleGenerator.EnsureYearAsync(dto.Year, now, cancellationToken);
            }
            catch (ScheduleBatchMutationConflictException)
            {
                return ApiErrors.Conflict("A PM batch changed during schedule recovery. Retry generation.");
            }
            return Results.Ok(result);
        })
        .WithName("GeneratePreventiveMaintenanceSchedules")
        .WithSummary("Ensures missing CPMP schedules for one calendar year")
        .Produces<ScheduleGenerationResult>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanGenerateSchedules);

        group.MapGet("/coverage-review", async (
            int? page,
            int? pageSize,
            PreventiveMaintenanceScheduleGenerationService scheduleGenerator,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var coverage = await scheduleGenerator.GetCoverageReviewAsync(
                timeProvider.GetUtcNow(),
                cancellationToken);
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var total = coverage.Items.Count;
            var pageCount = Math.Max(1, (int)Math.Ceiling((double)total / size));
            var pageNumber = Math.Clamp(page ?? 1, 1, pageCount);
            var items = coverage.Items
                .Skip((pageNumber - 1) * size)
                .Take(size)
                .ToArray();
            return Results.Ok(new ScheduleCoverageReviewPage(
                coverage.Year,
                pageNumber,
                size,
                total,
                items));
        })
        .WithName("ListScheduleCoverageReview")
        .WithSummary("Lists past CPMP cycles missing both a schedule and a deferral for GSD review")
        .Produces<ScheduleCoverageReviewPage>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanGenerateSchedules);

        group.MapGet("/enrollment-deferrals", async (
            int? page,
            int? pageSize,
            string? status,
            string? pmCycle,
            string? assetCategory,
            string? department,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            string? normalizedStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!ScheduleEnrollmentDeferralStatusCatalog.TryNormalize(status, out var parsedStatus))
                {
                    errors[nameof(status)] = ["Status must be NeedsReview or Reviewed."];
                }
                else
                {
                    normalizedStatus = parsedStatus;
                }
            }

            string? normalizedCycle = null;
            if (!string.IsNullOrWhiteSpace(pmCycle))
            {
                if (!PreventiveMaintenanceCycle.TryParse(pmCycle, out _, out _))
                {
                    errors[nameof(pmCycle)] = ["PM cycle must use the yyyy-MM format."];
                }
                else
                {
                    normalizedCycle = pmCycle.Trim();
                }
            }

            string? normalizedCategory = null;
            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                if (!AssetCategoryCatalog.TryNormalize(assetCategory, out var parsedCategory))
                {
                    errors[nameof(assetCategory)] = ["Asset category must be one of the selected UniPM study scope categories."];
                }
                else
                {
                    normalizedCategory = parsedCategory;
                }
            }

            var normalizedDepartment = string.IsNullOrWhiteSpace(department)
                ? null
                : department.Trim().ToUpperInvariant();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var allStatusQuery = context.ScheduleEnrollmentDeferrals
                .AsNoTracking()
                .Where(deferral => normalizedCycle == null || deferral.PmCycle == normalizedCycle)
                .Where(deferral => normalizedCategory == null || deferral.AssetCategoryAtDeferral == normalizedCategory)
                .Where(deferral => normalizedDepartment == null || deferral.DepartmentAtDeferral == normalizedDepartment);
            var pendingCount = await allStatusQuery.CountAsync(
                deferral => deferral.ReviewedAt == null,
                cancellationToken);
            var reviewedCount = await allStatusQuery.CountAsync(
                deferral => deferral.ReviewedAt != null,
                cancellationToken);
            var query = normalizedStatus switch
            {
                ScheduleEnrollmentDeferralStatusCatalog.NeedsReview => allStatusQuery.Where(deferral => deferral.ReviewedAt == null),
                ScheduleEnrollmentDeferralStatusCatalog.Reviewed => allStatusQuery.Where(deferral => deferral.ReviewedAt != null),
                _ => allStatusQuery
            };
            var total = await query.CountAsync(cancellationToken);
            var pageCount = Math.Max(1, (int)Math.Ceiling((double)total / size));
            var pageNumber = Math.Clamp(page ?? 1, 1, pageCount);
            var items = await query
                .OrderByDescending(item => item.DeferredAt)
                .ThenBy(item => item.DepartmentAtDeferral)
                .ThenBy(item => item.PmCycle)
                .Skip((pageNumber - 1) * size)
                .Take(size)
                .Join(context.Assets.AsNoTracking(),
                    deferral => deferral.AssetId,
                    asset => asset.Id,
                    (deferral, asset) => new { Deferral = deferral, Asset = asset })
                .GroupJoin(context.Users.AsNoTracking(),
                    item => item.Deferral.ReviewedByUserId,
                    reviewer => (Guid?)reviewer.Id,
                    (item, reviewers) => new { item.Deferral, item.Asset, Reviewers = reviewers })
                .SelectMany(item => item.Reviewers.DefaultIfEmpty(),
                    (item, reviewer) => new ScheduleEnrollmentDeferralResponse(
                        item.Deferral.AssetId,
                        item.Asset.AssetCode,
                        item.Deferral.DepartmentAtDeferral,
                        item.Deferral.AssetCategoryAtDeferral,
                        item.Deferral.PmCycle,
                        item.Deferral.NextEligiblePmCycle,
                        item.Deferral.ReasonCode,
                        ScheduleEnrollmentDeferralReason.GetDescription(item.Deferral.ReasonCode),
                        ScheduleEnrollmentDeferralStatusCatalog.ToLabel(item.Deferral),
                        item.Deferral.DeferredAt,
                        item.Deferral.ReviewedAt,
                        item.Deferral.ReviewedByUserId,
                        reviewer == null ? null : reviewer.DisplayName,
                        item.Deferral.ReviewNote))
                .ToListAsync(cancellationToken);
            return Results.Ok(new ScheduleEnrollmentDeferralPage(
                pageNumber,
                size,
                total,
                pendingCount,
                reviewedCount,
                items));
        })
        .WithName("ListScheduleEnrollmentDeferrals")
        .WithSummary("Lists deferred asset cycles and their GSD review status")
        .Produces<ScheduleEnrollmentDeferralPage>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanGenerateSchedules);

        group.MapPost("/enrollment-deferrals/{assetId:guid}/{pmCycle}/review", async (
            Guid assetId,
            string pmCycle,
            ScheduleEnrollmentDeferralReviewRequest request,
            HttpContext httpContext,
            IDbContextFactory<ApplicationDbContext> factory,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (!PreventiveMaintenanceCycle.TryParse(pmCycle, out _, out _))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(pmCycle)] = ["PM cycle must use the yyyy-MM format."]
                });
            }

            if (request.Note?.Length > ScheduleEnrollmentDeferralReviewRequest.MaximumNoteLength)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.Note)] = [$"Review note must not exceed {ScheduleEnrollmentDeferralReviewRequest.MaximumNoteLength} characters."]
                });
            }

            var reviewerId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(reviewerId, out var reviewerGuid))
            {
                return Results.Unauthorized();
            }

            var normalizedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var deferralQuery = context.ScheduleEnrollmentDeferrals
                .Where(deferral => deferral.AssetId == assetId && deferral.PmCycle == pmCycle);
            var reviewedAt = timeProvider.GetUtcNow();

            if (context.Database.IsRelational())
            {
                var updated = await deferralQuery
                    .Where(deferral => deferral.ReviewedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(deferral => deferral.ReviewedAt, reviewedAt)
                        .SetProperty(deferral => deferral.ReviewedByUserId, (Guid?)reviewerGuid)
                        .SetProperty(deferral => deferral.ReviewNote, normalizedNote),
                        cancellationToken);
                if (updated == 0)
                {
                    var exists = await deferralQuery.AnyAsync(cancellationToken);
                    return exists
                        ? ApiErrors.Conflict("This deferred cycle has already been reviewed.")
                        : Results.NotFound();
                }
            }
            else
            {
                var deferral = await deferralQuery.SingleOrDefaultAsync(cancellationToken);
                if (deferral is null)
                {
                    return Results.NotFound();
                }

                if (deferral.ReviewedAt is not null)
                {
                    return ApiErrors.Conflict("This deferred cycle has already been reviewed.");
                }

                deferral.ReviewedAt = reviewedAt;
                deferral.ReviewedByUserId = reviewerGuid;
                deferral.ReviewNote = normalizedNote;
                await context.SaveChangesAsync(cancellationToken);
            }

            return Results.NoContent();
        })
        .WithName("ReviewScheduleEnrollmentDeferral")
        .WithSummary("Marks one deferred asset cycle as reviewed by GSD")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanGenerateSchedules);

        group.MapGet("/supervisor-assignment-options", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? selectedUserId,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            if (!TryNormalizeAssignmentOptionFilters(
                    search,
                    page,
                    pageSize,
                    out var normalizedSearch,
                    out var pageNumber,
                    out var size,
                    out var validationErrors))
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var options = await GetAssignmentOptionsAsync(
                context,
                AuthRoleCatalog.Supervisor,
                normalizedSearch,
                pageNumber,
                size,
                selectedUserId,
                cancellationToken);
            return Results.Ok(new ScheduleSupervisorAssignmentOptionsResponse(
                options.Items,
                options.Page,
                options.PageSize,
                options.TotalCount,
                options.Selected));
        })
        .WithName("ListScheduleSupervisorAssignmentOptions")
        .WithSummary("Searches a bounded page of active Supervisors for GSD batch assignment")
        .Produces<ScheduleSupervisorAssignmentOptionsResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleSupervisors);

        group.MapGet("/assignment-options", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? selectedUserId,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            if (!TryNormalizeAssignmentOptionFilters(
                    search,
                    page,
                    pageSize,
                    out var normalizedSearch,
                    out var pageNumber,
                    out var size,
                    out var validationErrors))
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var options = await GetAssignmentOptionsAsync(
                context,
                AuthRoleCatalog.Inspector,
                normalizedSearch,
                pageNumber,
                size,
                selectedUserId,
                cancellationToken);
            return Results.Ok(new ScheduleWorkerAssignmentOptionsResponse(
                options.Items,
                options.Page,
                options.PageSize,
                options.TotalCount,
                options.Selected));
        })
        .WithName("ListScheduleWorkerAssignmentOptions")
        .WithSummary("Searches a bounded page of active Inspectors for Supervisor batch assignment")
        .Produces<ScheduleWorkerAssignmentOptionsResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleWorkers);

        group.MapPost("/", async (
            CreateScheduleDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = dto.Validate(
                PreventiveMaintenanceCycle.ToInstitutionalTime(timeProvider.GetUtcNow()).Year);
            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets.FirstOrDefaultAsync(asset => asset.Id == dto.AssetId, cancellationToken);
            if (asset is null)
            {
                return ApiErrors.NotFound("Asset not found.");
            }

            var pmCycle = dto.ResolvePmCycle();
            PreventiveMaintenanceCycle.TryParse(pmCycle, out _, out var cycleMonth);
            var scheduledMonths = CpmpScheduleFrequency.GetMonths(asset.AssetCategory);
            if (!CpmpScheduleFrequency.IsValid(asset.AssetCategory, cycleMonth))
            {
                var message = scheduledMonths.Count == 0
                    ? "A CPMP schedule frequency is not defined for this asset category."
                    : $"PM cycle month must be one of {string.Join(", ", scheduledMonths.Select(month => month.ToString("00")))} for this asset category.";
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.PmCycle)] = [message]
                });
            }

            if (asset.Status != AssetStatusCatalog.Active)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.AssetId)] = ["Choose an active asset for preventive maintenance."]
                });
            }

            if (string.IsNullOrWhiteSpace(asset.Department))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.AssetId)] = ["The asset needs a department before it can be scheduled."]
                });
            }

            var now = timeProvider.GetUtcNow();
            PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out var month);
            if (year != PreventiveMaintenanceCycle.ToInstitutionalTime(now).Year)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.Year)] = ["Ordinary schedule creation is limited to the current institutional calendar year."]
                });
            }

            var quarter = $"Q{((month - 1) / 3) + 1}";
            var periodType = SchedulePeriodTypeCatalog.TryNormalize(
                dto.PeriodType,
                out var normalizedPeriodType)
                ? normalizedPeriodType
                : throw new InvalidOperationException("Validated schedule period type was not canonicalizable.");
            var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle);

            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = dto.AssetId,
                Asset = asset,
                ScheduleDate = deadline,
                PmCycle = pmCycle,
                PeriodType = periodType,
                Quarter = periodType == SchedulePeriodTypeCatalog.Quarter ? quarter : null,
                Year = year,
                Status = deadline < PreventiveMaintenanceCycle.ToInstitutionalTime(now)
                    ? ScheduleStatusCatalog.Overdue
                    : ScheduleStatusCatalog.Due,
                CreatedAt = now,
                UpdatedAt = now
            };

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            }

            await using var transactionScope = transaction;
            try
            {
                if (!ScheduleBatchIdentity.TryCreate(asset.Department, asset.AssetCategory, pmCycle, out var identity))
                {
                    return ApiErrors.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(dto.AssetId)] = ["The asset needs a department and supported category before it can be scheduled."]
                    });
                }

                await using var mutationLock = await ScheduleBatchMutationLockLease.AcquireAsync(
                    context, identity, cancellationToken, transaction);
                if (!mutationLock.Acquired)
                {
                    return ApiErrors.Conflict("The PM batch changed while the schedule was being created. Retry the request.");
                }

                var duplicateCycle = await context.PreventiveMaintenanceSchedules
                    .AnyAsync(candidate => candidate.AssetId == dto.AssetId
                        && candidate.PmCycle == pmCycle,
                        cancellationToken);
                if (duplicateCycle)
                {
                    return ApiErrors.Conflict("A schedule already exists for this asset and PM cycle.");
                }

                if (await PreventiveMaintenanceScheduleGenerationService.GetBatchLockReasonAsync(
                        context, identity, cancellationToken) is not null)
                {
                    return ApiErrors.Conflict("This PM batch is already assigned or has inspection evidence, so it cannot accept another schedule.");
                }

                context.PreventiveMaintenanceSchedules.Add(schedule);
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (DbUpdateException exception)
                when (DatabaseConstraintViolation.IsUniqueConstraint(
                    exception,
                    PreventiveMaintenanceScheduleGenerationService.UniqueIndexName))
            {
                return ApiErrors.Conflict("A schedule already exists for this asset and PM cycle.");
            }
            catch (ScheduleBatchMutationConflictException)
            {
                return ApiErrors.Conflict("The PM batch changed while the schedule was being created. Retry the request.");
            }

            return Results.Created($"/api/v1/schedules/{schedule.Id}", ScheduleResponse.FromSchedule(schedule));
        })
        .WithName("CreateSchedule")
        .WithSummary("Creates a preventive maintenance schedule for an existing asset")
        .Produces<ScheduleResponse>(StatusCodes.Status201Created)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanManageSchedules);

        group.MapPut("/{id:guid}/supervisor-assignment", async (
            Guid id,
            AssignScheduleSupervisorDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            var supervisor = await userManager.FindByIdAsync(dto.SupervisorUserId.ToString());
            if (supervisor is null || !supervisor.IsActive
                || !await userManager.IsInRoleAsync(supervisor, AuthRoleCatalog.Supervisor))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.SupervisorUserId)] = ["Choose an active Supervisor account."]
                });
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var preview = await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Include(schedule => schedule.Asset)
                .SingleOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);
            if (preview?.Asset is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            if (!ScheduleBatchIdentity.TryCreate(preview, out var identity))
            {
                return ApiErrors.Conflict("Schedules without a department and valid PM cycle cannot be assigned as a batch.");
            }

            await using var mutationLock = await ScheduleBatchMutationLockLease.AcquireAsync(
                context,
                identity,
                cancellationToken);
            if (!mutationLock.Acquired)
            {
                return ApiErrors.Conflict("This PM batch is being updated. Try again shortly.");
            }

            var selected = await context.PreventiveMaintenanceSchedules
                .Include(schedule => schedule.Asset)
                .SingleOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);
            if (selected?.Asset is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            if (!ScheduleBatchIdentity.TryCreate(selected, out var currentIdentity)
                || currentIdentity != identity)
            {
                return ApiErrors.Conflict("The PM batch changed while assignment was being prepared. Refresh and try again.");
            }

            var batch = await LoadBatchAsync(context, identity, cancellationToken);
            if (batch.Count == 0)
            {
                return ApiErrors.Conflict("No schedules were found in this PM batch.");
            }

            if (await HasAssignmentWorkEvidenceAsync(context, batch, cancellationToken))
            {
                return ApiErrors.Conflict("A PM batch cannot be assigned after inspection work has started or a schedule is completed or cancelled.");
            }

            if (batch.Any(schedule => schedule.AssignedSupervisorUserId is not null
                || schedule.AssignedToUserId is not null))
            {
                if (!batch.All(schedule => schedule.AssignedSupervisorUserId == supervisor.Id))
                {
                    return ApiErrors.Conflict("This batch is already assigned. Use Schedule History to reassign unfinished work.");
                }

                await mutationLock.CommitAsync(cancellationToken);
                var existingWorkerIds = batch.Select(schedule => schedule.AssignedToUserId).Distinct().ToArray();
                var existingWorkerId = existingWorkerIds.Length == 1 ? existingWorkerIds[0] : null;
                var existingWorker = existingWorkerId is { } existingAssignedWorkerId
                    ? await userManager.FindByIdAsync(existingAssignedWorkerId.ToString())
                    : null;
                return Results.Ok(new ScheduleAssignmentBatchResponse(
                    identity.Department,
                    selected.Asset.AssetCategory,
                    identity.PmCycle,
                    existingWorkerId,
                    existingWorker?.DisplayName,
                    supervisor.Id,
                    supervisor.DisplayName,
                    batch.Select(schedule => schedule.Id).ToArray()));
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var schedule in batch)
            {
                schedule.AssignedSupervisorUserId = supervisor.Id;
                schedule.UpdatedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            await mutationLock.CommitAsync(cancellationToken);

            var workerIds = batch.Select(schedule => schedule.AssignedToUserId).Distinct().ToArray();
            var workerId = workerIds.Length == 1 ? workerIds[0] : null;
            var worker = workerId is { } assignedWorkerId
                ? await userManager.FindByIdAsync(assignedWorkerId.ToString())
                : null;
            return Results.Ok(new ScheduleAssignmentBatchResponse(
                identity.Department,
                selected.Asset.AssetCategory,
                identity.PmCycle,
                workerId,
                worker?.DisplayName,
                supervisor.Id,
                supervisor.DisplayName,
                batch.Select(schedule => schedule.Id).ToArray()));
        })
        .WithName("AssignScheduleBatchSupervisor")
        .WithSummary("Assigns a Supervisor to all schedules in one PM batch")
        .Produces<ScheduleAssignmentBatchResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleSupervisors);

        group.MapPut("/{id:guid}/assignment", async (
            Guid id,
            AssignScheduleWorkerDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var supervisorId))
            {
                return Results.Forbid();
            }

            var supervisor = await userManager.FindByIdAsync(supervisorId.ToString());
            if (supervisor is null || !supervisor.IsActive
                || !await userManager.IsInRoleAsync(supervisor, AuthRoleCatalog.Supervisor))
            {
                return Results.Forbid();
            }

            var worker = await userManager.FindByIdAsync(dto.WorkerUserId.ToString());
            if (worker is null || !worker.IsActive
                || !await userManager.IsInRoleAsync(worker, AuthRoleCatalog.Inspector))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.WorkerUserId)] = ["Choose an active Inspector account."]
                });
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var preview = await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Include(schedule => schedule.Asset)
                .SingleOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);
            if (preview?.Asset is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            if (!ScheduleBatchIdentity.TryCreate(preview, out var identity))
            {
                return ApiErrors.Conflict("Schedules without a department and valid PM cycle cannot be assigned as a batch.");
            }

            await using var mutationLock = await ScheduleBatchMutationLockLease.AcquireAsync(
                context,
                identity,
                cancellationToken);
            if (!mutationLock.Acquired)
            {
                return ApiErrors.Conflict("This PM batch is being updated. Try again shortly.");
            }

            var selected = await context.PreventiveMaintenanceSchedules
                .Include(schedule => schedule.Asset)
                .SingleOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);
            if (selected?.Asset is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            if (!ScheduleBatchIdentity.TryCreate(selected, out var currentIdentity)
                || currentIdentity != identity)
            {
                return ApiErrors.Conflict("The PM batch changed while assignment was being prepared. Refresh and try again.");
            }

            var batch = await LoadBatchAsync(context, identity, cancellationToken);
            if (batch.Count == 0)
            {
                return ApiErrors.Conflict("No schedules were found in this PM batch.");
            }

            var assignableSchedules = batch.Where(IsUnfinishedSchedule).ToArray();
            if (assignableSchedules.Length == 0)
            {
                return ApiErrors.Conflict("This PM batch has no unfinished schedules eligible for Inspector assignment.");
            }

            if (assignableSchedules.Any(schedule => schedule.AssignedSupervisorUserId != supervisor.Id))
            {
                return Results.Forbid();
            }

            if (await HasAssignmentWorkEvidenceAsync(context, assignableSchedules, cancellationToken))
            {
                return ApiErrors.Conflict("A PM batch cannot be assigned after inspection work has started or a schedule is completed or cancelled.");
            }

            if (assignableSchedules.Any(schedule => schedule.AssignedToUserId is not null))
            {
                if (!assignableSchedules.All(schedule => schedule.AssignedToUserId == worker.Id))
                {
                    return ApiErrors.Conflict("This batch is already assigned. Use Schedule History to reassign unfinished work.");
                }

                await mutationLock.CommitAsync(cancellationToken);
                return Results.Ok(new ScheduleAssignmentBatchResponse(
                    identity.Department,
                    selected.Asset.AssetCategory,
                    identity.PmCycle,
                    worker.Id,
                    worker.DisplayName,
                    supervisor.Id,
                    supervisor.DisplayName,
                    assignableSchedules.Select(schedule => schedule.Id).ToArray()));
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var schedule in assignableSchedules)
            {
                schedule.AssignedToUserId = worker.Id;
                schedule.UpdatedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            await mutationLock.CommitAsync(cancellationToken);

            var scheduleIds = batch.Select(schedule => schedule.Id).ToArray();
            return Results.Ok(new ScheduleAssignmentBatchResponse(
                identity.Department,
                selected.Asset.AssetCategory,
                identity.PmCycle,
                worker.Id,
                worker.DisplayName,
                supervisor.Id,
                supervisor.DisplayName,
                assignableSchedules.Select(schedule => schedule.Id).ToArray()));
        })
        .WithName("AssignScheduleBatchWorker")
        .WithSummary("Assigns an Inspector to a PM batch owned by the authenticated Supervisor")
        .Produces<ScheduleAssignmentBatchResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleWorkers);

        group.MapPut("/{id:guid}/supervisor-reassignment", async (
            Guid id,
            ReassignScheduleBatchDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
            await ReassignScheduleBatchAsync(
                id,
                dto,
                "Supervisor",
                principal,
                factory,
                userManager,
                cancellationToken))
        .WithName("ReassignScheduleBatchSupervisor")
        .WithSummary("Reassigns a Supervisor for unfinished schedules in a PM batch")
        .Produces<ScheduleAssignmentBatchResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleSupervisors);

        group.MapPut("/{id:guid}/inspector-reassignment", async (
            Guid id,
            ReassignScheduleBatchDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
            await ReassignScheduleBatchAsync(
                id,
                dto,
                "Inspector",
                principal,
                factory,
                userManager,
                cancellationToken))
        .WithName("ReassignScheduleBatchInspector")
        .WithSummary("Reassigns an Inspector for unfinished schedules in a PM batch")
        .Produces<ScheduleAssignmentBatchResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleWorkers);

        group.MapGet("/assignment-batches", async (
            int year,
            string? assetCategory,
            string? department,
            string? pmCycle,
            ClaimsPrincipal user,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = new Dictionary<string, string[]>();
            string? normalizedCategory = null;
            var normalizedDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            string? normalizedCycle = null;

            if (year is < 1 or > 9999)
            {
                validationErrors[nameof(year)] = ["Year must be between 1 and 9999."];
            }

            if (normalizedDepartment?.Length > 256)
            {
                validationErrors[nameof(department)] = ["Department must be 256 characters or fewer."];
            }

            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                if (AssetCategoryCatalog.TryNormalize(assetCategory, out var parsedCategory))
                {
                    normalizedCategory = parsedCategory;
                }
                else
                {
                    validationErrors[nameof(assetCategory)] =
                        ["Asset category must be one of the selected UniPM study scope categories."];
                }
            }

            if (!string.IsNullOrWhiteSpace(pmCycle))
            {
                var cycle = pmCycle.Trim();
                if (PreventiveMaintenanceCycle.TryParse(cycle, out var cycleYear, out _)
                    && cycleYear == year)
                {
                    normalizedCycle = cycle;
                }
                else
                {
                    validationErrors[nameof(pmCycle)] =
                        ["PM cycle must use the yyyy-MM format and match the selected year."];
                }
            }

            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            var isGsd = user.IsInRole(AuthRoleCatalog.Gsd);
            if (!isGsd && !user.IsInRole(AuthRoleCatalog.Supervisor))
            {
                return Results.Forbid();
            }

            Guid? supervisorId = null;
            if (!isGsd)
            {
                if (!Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var authenticatedUserId))
                {
                    return Results.Forbid();
                }

                supervisorId = authenticatedUserId;
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var query = context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Where(schedule => schedule.Year == year && schedule.Asset != null);

            if (normalizedDepartment is not null)
            {
                var departmentKey = normalizedDepartment.ToUpperInvariant();
                query = query.Where(schedule => schedule.Asset!.Department != null
                    && schedule.Asset.Department.Trim().ToUpper() == departmentKey);
            }

            if (normalizedCategory is not null)
            {
                query = query.Where(schedule => schedule.Asset!.AssetCategory == normalizedCategory);
            }

            if (normalizedCycle is not null)
            {
                query = query.Where(schedule => schedule.PmCycle == normalizedCycle);
            }

            var rows = await query
                .OrderBy(schedule => schedule.ScheduleDate)
                .ThenBy(schedule => schedule.Id)
                .Select(schedule => new ScheduleAssignmentQueueSourceRow(
                    new ScheduleResponse(
                        schedule.Id,
                        schedule.AssetId,
                        schedule.ScheduleDate,
                        schedule.PmCycle,
                        schedule.PeriodType,
                        schedule.Status,
                        schedule.Quarter,
                        schedule.Semester,
                        schedule.Year,
                        schedule.AcademicYear,
                        schedule.AssignedToUserId,
                        schedule.AssignedSupervisorUserId,
                        schedule.CompletedAt,
                        schedule.CreatedAt,
                        schedule.UpdatedAt,
                        new ScheduleAssetResponse(
                            schedule.Asset!.Id,
                            schedule.Asset.AssetCode,
                            schedule.Asset.AssetCategory,
                            schedule.Asset.Building,
                            schedule.Asset.Department,
                            schedule.Asset.Location,
                            schedule.Asset.Status,
                            schedule.Asset.QrCodeValue)),
                    context.InspectionRecords.Any(inspection => inspection.ScheduleId == schedule.Id)))
                .ToListAsync(cancellationToken);

            var assignedUserIds = rows
                .SelectMany(row => new Guid?[]
                {
                    row.Schedule.AssignedSupervisorUserId,
                    row.Schedule.AssignedToUserId
                })
                .Where(userId => userId.HasValue)
                .Select(userId => userId!.Value)
                .Distinct()
                .ToArray();
            var assigneeDisplayNames = await context.Users
                .AsNoTracking()
                .Where(user => assignedUserIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

            var groupedRows = new Dictionary<ScheduleBatchIdentity, List<ScheduleAssignmentQueueSourceRow>>();
            foreach (var row in rows)
            {
                if (!ScheduleBatchIdentity.TryCreate(
                        row.Schedule.Asset?.Department,
                        row.Schedule.Asset?.AssetCategory,
                        row.Schedule.PmCycle,
                        out var identity))
                {
                    continue;
                }

                if (!groupedRows.TryGetValue(identity, out var batchRows))
                {
                    batchRows = [];
                    groupedRows.Add(identity, batchRows);
                }

                batchRows.Add(row);
            }

            var batches = groupedRows
                .Where(pair => supervisorId is null
                    || pair.Value.Any(row => row.Schedule.AssignedSupervisorUserId == supervisorId))
                .Select(pair => CreateAssignmentQueueBatch(pair.Key, pair.Value, assigneeDisplayNames))
                .OrderBy(batch => batch.PmCycle, StringComparer.Ordinal)
                .ThenBy(batch => batch.Department, StringComparer.OrdinalIgnoreCase)
                .ThenBy(batch => batch.AssetCategory, StringComparer.Ordinal)
                .ToArray();

            return Results.Ok(new ScheduleAssignmentQueueResponse(
                batches.Sum(batch => batch.ScheduleCount),
                batches));
        })
        .WithName("ListScheduleAssignmentBatches")
        .WithSummary("Lists grouped PM schedule batches for assignment")
        .Produces<ScheduleAssignmentQueueResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanReadSchedules);

        group.MapGet("/registry", async (
            Guid? assetId,
            string? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? quarter,
            int? year,
            string? pmCycle,
            string? department,
            string? assetCategory,
            string? search,
            int? page,
            int? pageSize,
            ClaimsPrincipal user,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var pageNumber = page ?? 1;
            var size = pageSize ?? 25;
            var validationErrors = ValidateScheduleRegistryFilters(
                from,
                to,
                quarter,
                year,
                pmCycle,
                status,
                department,
                assetCategory,
                search,
                pageNumber,
                size,
                out var normalizedQuarter,
                out var normalizedPmCycle,
                out var normalizedStatus,
                out var normalizedDepartment,
                out var normalizedCategory,
                out var normalizedSearch);
            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var scopedQuery = CreateReadableScheduleQuery(context, user);
            if (scopedQuery is null)
            {
                return Results.Forbid();
            }

            var overallCount = await scopedQuery.CountAsync(cancellationToken);
            var query = ApplyScheduleRegistryFilters(
                scopedQuery,
                assetId,
                normalizedStatus,
                from,
                to,
                normalizedQuarter,
                year,
                normalizedPmCycle,
                normalizedDepartment,
                normalizedCategory,
                normalizedSearch);
            var totalCount = await query.CountAsync(cancellationToken);
            var skip = (long)(pageNumber - 1) * size;
            List<ScheduleRegistryItemResponse> items = skip > int.MaxValue
                ? []
                : await query
                    .OrderBy(schedule => schedule.ScheduleDate)
                    .ThenBy(schedule => schedule.Id)
                    .Skip((int)skip)
                    .Take(size)
                    .Select(schedule => new ScheduleRegistryItemResponse(
                        schedule.Id,
                        schedule.AssetId,
                        schedule.ScheduleDate,
                        schedule.PmCycle,
                        schedule.PeriodType,
                        schedule.Status,
                        schedule.Quarter,
                        schedule.Semester,
                        schedule.Year,
                        schedule.AcademicYear,
                        schedule.CompletedAt,
                        schedule.CreatedAt,
                        schedule.UpdatedAt,
                        schedule.Asset == null
                            ? null
                            : new ScheduleRegistryAssetContext(
                                schedule.Asset.Id,
                                schedule.Asset.AssetCode,
                                schedule.Asset.AssetCategory,
                                schedule.Asset.Building,
                                schedule.Asset.Department,
                                schedule.Asset.Location)))
                    .ToListAsync(cancellationToken);

            return Results.Ok(new ScheduleRegistryPageResponse(
                items,
                pageNumber,
                size,
                totalCount,
                overallCount));
        })
        .WithName("ListScheduleRegistry")
        .WithSummary("Lists a bounded page of schedules for the web registry")
        .Produces<ScheduleRegistryPageResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanReadSchedules);

        group.MapGet("/registry/options", async (
            string? assetCategory,
            string? department,
            ClaimsPrincipal user,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var normalizedDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            var validationErrors = new Dictionary<string, string[]>();
            string? normalizedCategory = null;
            if (normalizedDepartment?.Length > 256)
            {
                validationErrors[nameof(department)] = ["Department must be 256 characters or fewer."];
            }

            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                if (AssetCategoryCatalog.TryNormalize(assetCategory, out var category))
                {
                    normalizedCategory = category;
                }
                else
                {
                    validationErrors[nameof(assetCategory)] =
                        ["Asset category must be one of the selected UniPM study scope categories."];
                }
            }

            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var scopedQuery = CreateReadableScheduleQuery(context, user);
            if (scopedQuery is null)
            {
                return Results.Forbid();
            }

            var query = scopedQuery.Where(schedule => schedule.Asset != null);
            if (normalizedCategory is not null)
            {
                query = query.Where(schedule => schedule.Asset!.AssetCategory == normalizedCategory);
            }

            var departmentOptionsQuery = query;
            if (normalizedDepartment is not null)
            {
                var departmentKey = normalizedDepartment.ToUpperInvariant();
                query = query.Where(schedule => schedule.Asset!.Department != null
                    && schedule.Asset.Department.ToUpper() == departmentKey);
            }

            var departments = await departmentOptionsQuery
                .Where(schedule => schedule.Asset!.Department != null)
                .Select(schedule => schedule.Asset!.Department!)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync(cancellationToken);
            var years = await query
                .Where(schedule => schedule.PmCycle.Length == PreventiveMaintenanceCycle.Length)
                .Select(schedule => schedule.PmCycle.Substring(0, 4))
                .Distinct()
                .OrderByDescending(value => value)
                .ToListAsync(cancellationToken);

            return Results.Ok(new ScheduleRegistryOptionsResponse(
                AssetCategoryCatalog.All,
                ScheduleQuarterCatalog.PersistedValues,
                departments,
                years));
        })
        .WithName("GetScheduleRegistryOptions")
        .WithSummary("Lists scoped schedule registry filter options")
        .Produces<ScheduleRegistryOptionsResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanReadSchedules);

        group.MapGet("/registry/summary", async (
            ClaimsPrincipal user,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var scopedQuery = CreateReadableScheduleQuery(context, user);
            if (scopedQuery is null)
            {
                return Results.Forbid();
            }

            var groupedCounts = await scopedQuery
                .GroupBy(schedule => schedule.Status)
                .Select(grouping => new ScheduleRegistryStatusCounts(grouping.Key, grouping.Count()))
                .ToListAsync(cancellationToken);
            var counts = groupedCounts.ToDictionary(value => value.Status, value => value.Count, StringComparer.Ordinal);
            int Count(string status) => counts.TryGetValue(status, out var count) ? count : 0;
            var totalCount = groupedCounts.Sum(value => value.Count);

            return Results.Ok(new ScheduleRegistrySummaryResponse(
                totalCount,
                Count(ScheduleStatusCatalog.Due),
                Count(ScheduleStatusCatalog.Ongoing),
                Count(ScheduleStatusCatalog.Overdue),
                Count(ScheduleStatusCatalog.Completed),
                Count(ScheduleStatusCatalog.Cancelled)));
        })
        .WithName("GetScheduleRegistrySummary")
        .WithSummary("Returns status totals for the authenticated schedule read scope")
        .Produces<ScheduleRegistrySummaryResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanReadSchedules);

        group.MapGet("/", async (
            Guid? assetId,
            string? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? quarter,
            int? year,
            string? department,
            string? assetCategory,
            string? search,
            ClaimsPrincipal user,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            Guid? assignedUserId = null;
            if (!user.IsInRole(AuthRoleCatalog.Gsd)
                && !user.IsInRole(AuthRoleCatalog.Supervisor))
            {
                if (!Guid.TryParse(
                        user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                        out var authenticatedUserId))
                {
                    return Results.Forbid();
                }

                assignedUserId = authenticatedUserId;
            }

            if (from is not null && to is not null && from > to)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(from)] = ["From date must be earlier than or equal to to date."]
                });
            }

            var normalizedDepartment = string.IsNullOrWhiteSpace(department)
                ? null
                : department.Trim();
            var normalizedSearch = string.IsNullOrWhiteSpace(search)
                ? null
                : search.Trim();
            var normalizedCategory = string.Empty;
            var validationErrors = new Dictionary<string, string[]>();
            if (normalizedDepartment?.Length > 256)
            {
                validationErrors[nameof(department)] = ["Department must be 256 characters or fewer."];
            }

            if (normalizedSearch?.Length > 256)
            {
                validationErrors[nameof(search)] = ["Search must be 256 characters or fewer."];
            }

            if (!string.IsNullOrWhiteSpace(assetCategory)
                && !AssetCategoryCatalog.TryNormalize(assetCategory, out normalizedCategory))
            {
                validationErrors[nameof(assetCategory)] = ["Asset category must be one of the selected UniPM study scope categories."];
            }

            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var query = context.PreventiveMaintenanceSchedules.AsNoTracking();

            if (assignedUserId is not null)
            {
                query = query.Where(schedule => schedule.AssignedToUserId == assignedUserId.Value);
            }

            if (assetId is not null)
            {
                query = query.Where(schedule => schedule.AssetId == assetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!ScheduleStatusCatalog.TryNormalize(status, out var normalizedStatus))
                {
                    return ApiErrors.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(status)] = ["Status must be a supported schedule status."]
                    });
                }

                query = query.Where(schedule => schedule.Status == normalizedStatus);
            }

            if (from is not null)
            {
                query = query.Where(schedule => schedule.ScheduleDate >= from.Value);
            }

            if (to is not null)
            {
                query = query.Where(schedule => schedule.ScheduleDate <= to.Value);
            }

            if (!string.IsNullOrWhiteSpace(quarter))
            {
                if (!ScheduleQuarterCatalog.TryNormalizeNullable(quarter, out var normalizedQuarter))
                {
                    return ApiErrors.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(quarter)] = ["Quarter must be one of Q1, Q2, Q3, or Q4."]
                    });
                }

                var quarterMonths = normalizedQuarter switch
                {
                    ScheduleQuarterCatalog.Q1 => new[] { "01", "02", "03" },
                    ScheduleQuarterCatalog.Q2 => new[] { "04", "05", "06" },
                    ScheduleQuarterCatalog.Q3 => new[] { "07", "08", "09" },
                    ScheduleQuarterCatalog.Q4 => new[] { "10", "11", "12" },
                    _ => Array.Empty<string>()
                };
                query = query.Where(schedule =>
                    schedule.PmCycle != null
                    && schedule.PmCycle.Length == PreventiveMaintenanceCycle.Length
                    && quarterMonths.Contains(schedule.PmCycle.Substring(5, 2)));
            }

            if (year is not null)
            {
                query = query.Where(schedule => schedule.Year == year.Value);
            }

            if (normalizedDepartment is not null)
            {
                var departmentKey = normalizedDepartment.ToUpperInvariant();
                query = query.Where(schedule =>
                    schedule.Asset != null
                    && schedule.Asset.Department != null
                    && schedule.Asset.Department.ToUpper() == departmentKey);
            }

            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                query = query.Where(schedule =>
                    schedule.Asset != null
                    && schedule.Asset.AssetCategory == normalizedCategory);
            }

            if (normalizedSearch is not null)
            {
                var searchKey = normalizedSearch.ToUpperInvariant();
                query = query.Where(schedule =>
                    schedule.PmCycle != null && schedule.PmCycle.ToUpper().Contains(searchKey)
                    || (schedule.Asset != null && (
                        schedule.Asset.AssetCode.Contains(searchKey)
                        || schedule.Asset.AssetCategory.ToUpper().Contains(searchKey)
                        || (schedule.Asset.Building != null && schedule.Asset.Building.ToUpper().Contains(searchKey))
                        || (schedule.Asset.Department != null && schedule.Asset.Department.ToUpper().Contains(searchKey))
                        || (schedule.Asset.Location != null && schedule.Asset.Location.ToUpper().Contains(searchKey)))));
            }

            var schedules = await query
                .OrderBy(schedule => schedule.ScheduleDate)
                .ThenBy(schedule => schedule.Id)
                .Select(schedule => new ScheduleResponse(
                    schedule.Id,
                    schedule.AssetId,
                    schedule.ScheduleDate,
                    schedule.PmCycle,
                    schedule.PeriodType,
                    schedule.Status,
                    schedule.Quarter,
                    schedule.Semester,
                    schedule.Year,
                    schedule.AcademicYear,
                    schedule.AssignedToUserId,
                    schedule.AssignedSupervisorUserId,
                    schedule.CompletedAt,
                    schedule.CreatedAt,
                    schedule.UpdatedAt,
                    schedule.Asset == null
                        ? null
                        : new ScheduleAssetResponse(
                            schedule.Asset.Id,
                            schedule.Asset.AssetCode,
                            schedule.Asset.AssetCategory,
                            schedule.Asset.Building,
                            schedule.Asset.Department,
                            schedule.Asset.Location,
                            schedule.Asset.Status,
                            schedule.Asset.QrCodeValue)))
                .ToListAsync(cancellationToken);

            return Results.Ok(schedules);
        })
        .WithName("ListSchedules")
        .WithSummary("Lists preventive maintenance schedules using supported filters")
        .Produces<IReadOnlyList<ScheduleResponse>>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanReadSchedules);

        group.MapGet("/{id}", async (
            Guid id,
            ClaimsPrincipal user,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var query = context.PreventiveMaintenanceSchedules.AsNoTracking();
            if (!user.IsInRole(AuthRoleCatalog.Gsd)
                && !user.IsInRole(AuthRoleCatalog.Supervisor))
            {
                if (!Guid.TryParse(
                        user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                        out var authenticatedUserId))
                {
                    return Results.Forbid();
                }

                query = query.Where(schedule => schedule.AssignedToUserId == authenticatedUserId);
            }

            var schedule = await query
                .Include(schedule => schedule.Asset)
                .FirstOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);

            return schedule is not null
                ? Results.Ok(ScheduleResponse.FromSchedule(schedule))
                : ApiErrors.NotFound("Schedule not found.");
        })
        .WithName("GetSchedule")
        .WithSummary("Gets a preventive maintenance schedule by its identifier")
        .Produces<ScheduleResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization(AuthPolicyCatalog.CanReadSchedules);

        return endpoints;
    }

    private static ScheduleAssignmentQueueBatchResponse CreateAssignmentQueueBatch(
        ScheduleBatchIdentity identity,
        IReadOnlyCollection<ScheduleAssignmentQueueSourceRow> rows,
        IReadOnlyDictionary<Guid, string> assigneeDisplayNames)
    {
        var schedules = rows.Select(row => row.Schedule).ToArray();
        var unfinishedRows = rows
            .Where(row => IsUnfinishedSchedule(row.Schedule) && !row.HasInspectionEvidence)
            .ToArray();
        var assignmentRows = unfinishedRows.Length > 0 ? unfinishedRows : rows.ToArray();
        var supervisor = SummarizeAssignment(assignmentRows.Select(row => row.Schedule.AssignedSupervisorUserId));
        var inspector = SummarizeAssignment(assignmentRows.Select(row => row.Schedule.AssignedToUserId));
        var representative = schedules
            .OrderBy(schedule => schedule.Asset?.AssetCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(schedule => schedule.Id)
            .First();
        var buildings = schedules
            .Select(schedule => schedule.Asset?.Building?.Trim())
            .OfType<string>()
            .Where(building => !string.IsNullOrWhiteSpace(building))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(building => building, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var supervisorDisplayName = supervisor.UserId is Guid supervisorId
            && assigneeDisplayNames.TryGetValue(supervisorId, out var resolvedSupervisorName)
                ? resolvedSupervisorName
                : null;
        var inspectorDisplayName = inspector.UserId is Guid inspectorId
            && assigneeDisplayNames.TryGetValue(inspectorId, out var resolvedInspectorName)
                ? resolvedInspectorName
                : null;

        return new ScheduleAssignmentQueueBatchResponse(
            identity.CreateLockResource(),
            representative.Asset?.Department?.Trim() ?? identity.Department,
            representative.Asset?.AssetCategory ?? identity.AssetCategory,
            identity.PmCycle,
            schedules.Length,
            unfinishedRows.Length,
            schedules.Count(schedule => string.Equals(
                schedule.Status, ScheduleStatusCatalog.Completed, StringComparison.OrdinalIgnoreCase)),
            schedules.Count(schedule => string.Equals(
                schedule.Status, ScheduleStatusCatalog.Ongoing, StringComparison.OrdinalIgnoreCase)),
            schedules.Count(schedule => string.Equals(
                schedule.Status, ScheduleStatusCatalog.Overdue, StringComparison.OrdinalIgnoreCase)),
            supervisor.State,
            supervisor.UserId,
            supervisorDisplayName,
            inspector.State,
            inspector.UserId,
            inspectorDisplayName,
            rows.Any(row => row.HasInspectionEvidence
                || row.Schedule.CompletedAt is not null
                || string.Equals(
                    row.Schedule.Status,
                    ScheduleStatusCatalog.Ongoing,
                    StringComparison.OrdinalIgnoreCase)
                || !IsAssignableScheduleStatus(row.Schedule.Status)),
            assignmentRows.All(row => row.Schedule.AssignedSupervisorUserId == supervisor.UserId)
                && supervisor.UserId is not null,
            buildings,
            representative);
    }

    private static bool TryNormalizeAssignmentOptionFilters(
        string? search,
        int? page,
        int? pageSize,
        out string? normalizedSearch,
        out int pageNumber,
        out int size,
        out Dictionary<string, string[]> validationErrors)
    {
        normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        pageNumber = page ?? 1;
        size = pageSize ?? AssignmentOptionLimit;
        validationErrors = [];

        if (pageNumber < 1)
        {
            validationErrors[nameof(page)] = ["Page must be greater than zero."];
        }

        if (size is < 1 or > MaximumAssignmentOptionPageSize)
        {
            validationErrors[nameof(pageSize)] = [
                $"Page size must be between 1 and {MaximumAssignmentOptionPageSize}."];
        }

        if (normalizedSearch?.Length > MaximumAssigneeSearchLength)
        {
            validationErrors[nameof(search)] = [
                $"Search must not exceed {MaximumAssigneeSearchLength} characters."];
        }

        return validationErrors.Count == 0;
    }

    private static async Task<ScheduleAssignmentOptionsPage> GetAssignmentOptionsAsync(
        ApplicationDbContext context,
        string roleName,
        string? search,
        int page,
        int pageSize,
        Guid? selectedUserId,
        CancellationToken cancellationToken)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();
        var eligibleUsers =
            from user in context.Users.AsNoTracking()
            join userRole in context.UserRoles.AsNoTracking()
                on user.Id equals userRole.UserId
            join role in context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where user.IsActive && role.NormalizedName == normalizedRoleName
            select user;
        var query = eligibleUsers;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(user => user.DisplayName.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (long)(page - 1) * pageSize;
        List<ScheduleAssigneeOption> items = skip > int.MaxValue
            ? []
            : await query
                .OrderBy(user => user.DisplayName)
                .ThenBy(user => user.Id)
                .Skip((int)skip)
                .Take(pageSize)
            .Select(user => new ScheduleAssigneeOption(user.Id, user.DisplayName))
            .ToListAsync(cancellationToken);
        var selected = selectedUserId is Guid selectedId
            ? await eligibleUsers
                .Where(user => user.Id == selectedId)
                .Select(user => new ScheduleAssigneeOption(user.Id, user.DisplayName))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new ScheduleAssignmentOptionsPage(items, page, pageSize, totalCount, selected);
    }

    private static ScheduleAssignmentState SummarizeAssignment(IEnumerable<Guid?> userIds)
    {
        var distinctIds = userIds.Distinct().ToArray();
        if (distinctIds.Length == 1)
        {
            var userId = distinctIds[0];
            return userId is not null
                ? new ScheduleAssignmentState("Assigned", userId)
                : new ScheduleAssignmentState("Unassigned", null);
        }

        return distinctIds.Contains(null)
            ? new ScheduleAssignmentState("Partially assigned", null)
            : new ScheduleAssignmentState("Mixed", null);
    }

    private static bool IsAssignableScheduleStatus(string status)
    {
        return string.Equals(status, ScheduleStatusCatalog.Due, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ScheduleStatusCatalog.Ongoing, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ScheduleStatusCatalog.Overdue, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ScheduleAssignmentQueueSourceRow(
        ScheduleResponse Schedule,
        bool HasInspectionEvidence);

    private sealed record ScheduleAssignmentState(string State, Guid? UserId);

    private static Task<List<PreventiveMaintenanceSchedule>> LoadBatchAsync(
        ApplicationDbContext context,
        ScheduleBatchIdentity identity,
        CancellationToken cancellationToken)
    {
        return context.PreventiveMaintenanceSchedules
            .Include(schedule => schedule.Asset)
            .Where(schedule => schedule.PmCycle == identity.PmCycle
                && schedule.Asset != null
                && schedule.Asset.AssetCategory.Trim().ToUpper() == identity.AssetCategory
                && schedule.Asset.Department != null
                && schedule.Asset.Department.Trim().ToUpper() == identity.Department)
            .ToListAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateScheduleRegistryFilters(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? quarter,
        int? year,
        string? pmCycle,
        string? status,
        string? department,
        string? assetCategory,
        string? search,
        int page,
        int pageSize,
        out string? normalizedQuarter,
        out string? normalizedPmCycle,
        out string? normalizedStatus,
        out string? normalizedDepartment,
        out string? normalizedCategory,
        out string? normalizedSearch)
    {
        normalizedQuarter = null;
        normalizedPmCycle = null;
        normalizedStatus = null;
        normalizedDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        normalizedCategory = null;
        normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors[nameof(page)] = ["Page must be greater than zero."];
        }

        if (pageSize is < 1 or > 100)
        {
            errors[nameof(pageSize)] = ["Page size must be between 1 and 100."];
        }

        if (from is not null && to is not null && from > to)
        {
            errors[nameof(from)] = ["From date must be earlier than or equal to to date."];
        }

        if (!string.IsNullOrWhiteSpace(quarter))
        {
            if (ScheduleQuarterCatalog.TryNormalizeNullable(quarter, out var normalizedQuarterValue))
            {
                normalizedQuarter = normalizedQuarterValue;
            }
            else
            {
                errors[nameof(quarter)] = ["Quarter must be one of Q1, Q2, Q3, or Q4."];
            }
        }

        if (year is < 1 or > 9999)
        {
            errors[nameof(year)] = ["Year must be between 1 and 9999."];
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (ScheduleStatusCatalog.TryNormalize(status, out var normalizedStatusValue))
            {
                normalizedStatus = normalizedStatusValue;
            }
            else
            {
                errors[nameof(status)] = ["Status must be a supported schedule status."];
            }
        }

        if (!string.IsNullOrWhiteSpace(pmCycle))
        {
            var value = pmCycle.Trim();
            if (PreventiveMaintenanceCycle.TryParse(value, out _, out _))
            {
                normalizedPmCycle = value;
            }
            else
            {
                errors[nameof(pmCycle)] = ["PM cycle must use the yyyy-MM format."];
            }
        }

        if (normalizedDepartment?.Length > 256)
        {
            errors[nameof(department)] = ["Department must be 256 characters or fewer."];
        }

        if (normalizedSearch?.Length > 256)
        {
            errors[nameof(search)] = ["Search must be 256 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(assetCategory))
        {
            if (AssetCategoryCatalog.TryNormalize(assetCategory, out var normalizedCategoryValue))
            {
                normalizedCategory = normalizedCategoryValue;
            }
            else
            {
                errors[nameof(assetCategory)] =
                    ["Asset category must be one of the selected UniPM study scope categories."];
            }
        }

        return errors;
    }

    private static IQueryable<PreventiveMaintenanceSchedule>? CreateReadableScheduleQuery(
        ApplicationDbContext context,
        ClaimsPrincipal user)
    {
        var query = context.PreventiveMaintenanceSchedules.AsNoTracking();
        if (user.IsInRole(AuthRoleCatalog.Gsd) || user.IsInRole(AuthRoleCatalog.Supervisor))
        {
            return query;
        }

        if (!Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var authenticatedUserId))
        {
            return null;
        }

        return query.Where(schedule => schedule.AssignedToUserId == authenticatedUserId);
    }

    private static IQueryable<PreventiveMaintenanceSchedule> ApplyScheduleRegistryFilters(
        IQueryable<PreventiveMaintenanceSchedule> query,
        Guid? assetId,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? quarter,
        int? year,
        string? pmCycle,
        string? department,
        string? assetCategory,
        string? search)
    {
        if (assetId is not null)
        {
            query = query.Where(schedule => schedule.AssetId == assetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status)
            && ScheduleStatusCatalog.TryNormalize(status, out var normalizedStatus))
        {
            query = query.Where(schedule => schedule.Status == normalizedStatus);
        }

        if (from is not null)
        {
            query = query.Where(schedule => schedule.ScheduleDate >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(schedule => schedule.ScheduleDate <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(quarter))
        {
            var quarterMonths = quarter switch
            {
                ScheduleQuarterCatalog.Q1 => new[] { "01", "02", "03" },
                ScheduleQuarterCatalog.Q2 => new[] { "04", "05", "06" },
                ScheduleQuarterCatalog.Q3 => new[] { "07", "08", "09" },
                ScheduleQuarterCatalog.Q4 => new[] { "10", "11", "12" },
                _ => Array.Empty<string>()
            };
            query = query.Where(schedule =>
                schedule.PmCycle.Length == PreventiveMaintenanceCycle.Length
                && quarterMonths.Contains(schedule.PmCycle.Substring(5, 2)));
        }

        if (year is not null)
        {
            var yearKey = year.Value.ToString("D4", CultureInfo.InvariantCulture);
            query = query.Where(schedule =>
                schedule.PmCycle.Length == PreventiveMaintenanceCycle.Length
                && schedule.PmCycle.Substring(0, 4) == yearKey);
        }

        if (pmCycle is not null)
        {
            query = query.Where(schedule => schedule.PmCycle == pmCycle);
        }

        if (department is not null)
        {
            var departmentKey = department.ToUpperInvariant();
            query = query.Where(schedule => schedule.Asset != null
                && schedule.Asset.Department != null
                && schedule.Asset.Department.ToUpper() == departmentKey);
        }

        if (assetCategory is not null)
        {
            query = query.Where(schedule => schedule.Asset != null
                && schedule.Asset.AssetCategory == assetCategory);
        }

        if (search is not null)
        {
            var searchKey = search.ToUpperInvariant();
            query = query.Where(schedule =>
                schedule.PmCycle.ToUpper().Contains(searchKey)
                || (schedule.Asset != null && (
                    schedule.Asset.AssetCode.Contains(searchKey)
                    || schedule.Asset.AssetCategory.ToUpper().Contains(searchKey)
                    || (schedule.Asset.Building != null && schedule.Asset.Building.ToUpper().Contains(searchKey))
                    || (schedule.Asset.Department != null && schedule.Asset.Department.ToUpper().Contains(searchKey))
                    || (schedule.Asset.Location != null && schedule.Asset.Location.ToUpper().Contains(searchKey)))));
        }

        return query;
    }

    private static async Task<bool> HasAssignmentWorkEvidenceAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<PreventiveMaintenanceSchedule> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Any(schedule => schedule.CompletedAt is not null
            || !string.Equals(schedule.Status, ScheduleStatusCatalog.Due, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(schedule.Status, ScheduleStatusCatalog.Overdue, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var scheduleIds = batch.Select(schedule => schedule.Id).ToArray();
        return await context.InspectionRecords.AnyAsync(
            inspection => scheduleIds.Contains(inspection.ScheduleId),
            cancellationToken);
    }

    private static bool IsUnfinishedSchedule(PreventiveMaintenanceSchedule schedule)
    {
        return schedule.CompletedAt is null && IsAssignableScheduleStatus(schedule.Status);
    }

    private static bool IsUnfinishedSchedule(ScheduleResponse schedule)
    {
        return schedule.CompletedAt is null && IsAssignableScheduleStatus(schedule.Status);
    }

    private static async Task<IResult> ReassignScheduleBatchAsync(
        Guid scheduleId,
        ReassignScheduleBatchDto dto,
        string assignmentRole,
        ClaimsPrincipal principal,
        IDbContextFactory<ApplicationDbContext> factory,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var errors = dto.Validate();
        if (errors.Count > 0)
        {
            return ApiErrors.Validation(errors);
        }

        var isSupervisorAssignment = string.Equals(
            assignmentRole,
            "Supervisor",
            StringComparison.Ordinal);
        var actorRole = isSupervisorAssignment
            ? AuthRoleCatalog.Gsd
            : AuthRoleCatalog.Supervisor;
        var targetRole = isSupervisorAssignment
            ? AuthRoleCatalog.Supervisor
            : AuthRoleCatalog.Inspector;
        if (!principal.IsInRole(actorRole)
            || !Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId))
        {
            return Results.Forbid();
        }

        var actor = await userManager.FindByIdAsync(actorId.ToString());
        if (actor is null || !actor.IsActive
            || !await userManager.IsInRoleAsync(actor, actorRole))
        {
            return Results.Forbid();
        }

        var target = await userManager.FindByIdAsync(dto.AssigneeUserId.ToString());
        if (target is null || !target.IsActive
            || !await userManager.IsInRoleAsync(target, targetRole))
        {
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(dto.AssigneeUserId)] = [$"Choose an active {targetRole} account."]
            });
        }

        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var preview = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .Include(schedule => schedule.Asset)
            .SingleOrDefaultAsync(schedule => schedule.Id == scheduleId, cancellationToken);
        if (preview?.Asset is null)
        {
            return ApiErrors.NotFound("Schedule not found.");
        }

        if (!ScheduleBatchIdentity.TryCreate(preview, out var identity))
        {
            return ApiErrors.Conflict("Schedules without a department and valid PM cycle cannot be reassigned as a batch.");
        }

        await using var mutationLock = await ScheduleBatchMutationLockLease.AcquireAsync(
            context,
            identity,
            cancellationToken);
        if (!mutationLock.Acquired)
        {
            return ApiErrors.Conflict("This PM batch is being updated. Try again shortly.");
        }

        var selected = await context.PreventiveMaintenanceSchedules
            .Include(schedule => schedule.Asset)
            .SingleOrDefaultAsync(schedule => schedule.Id == scheduleId, cancellationToken);
        if (selected?.Asset is null)
        {
            return ApiErrors.NotFound("Schedule not found.");
        }

        if (!ScheduleBatchIdentity.TryCreate(selected, out var currentIdentity)
            || currentIdentity != identity)
        {
            return ApiErrors.Conflict("The PM batch changed while reassignment was being prepared. Refresh and try again.");
        }

        var batch = await LoadBatchAsync(context, identity, cancellationToken);
        if (batch.Count == 0)
        {
            return ApiErrors.Conflict("No schedules were found in this PM batch.");
        }

        var batchScheduleIds = batch.Select(schedule => schedule.Id).ToArray();
        var schedulesWithInspection = (await context.InspectionRecords
            .AsNoTracking()
            .Where(inspection => batchScheduleIds.Contains(inspection.ScheduleId))
            .Select(inspection => inspection.ScheduleId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var unfinished = batch
            .Where(schedule => IsUnfinishedSchedule(schedule)
                && !schedulesWithInspection.Contains(schedule.Id))
            .ToArray();
        if (unfinished.Length == 0)
        {
            return ApiErrors.Conflict("This PM batch has no unfinished schedules eligible for reassignment.");
        }

        if (!isSupervisorAssignment
            && unfinished.Any(schedule => schedule.AssignedSupervisorUserId != actorId))
        {
            return Results.Forbid();
        }

        Guid? CurrentAssignee(PreventiveMaintenanceSchedule schedule) =>
            isSupervisorAssignment ? schedule.AssignedSupervisorUserId : schedule.AssignedToUserId;

        if (unfinished.All(schedule => CurrentAssignee(schedule) is null))
        {
            return ApiErrors.Conflict("This batch has no existing assignment to reassign. Use Planning & Assignment first.");
        }

        var changed = unfinished
            .Where(schedule => CurrentAssignee(schedule) != target.Id)
            .ToArray();
        if (changed.Length == 0)
        {
            return ApiErrors.Conflict("The selected person is already assigned to all unfinished schedules.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var schedule in changed)
        {
            context.ScheduleAssignmentReassignmentAudits.Add(new ScheduleAssignmentReassignmentAudit
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                AssignmentRole = assignmentRole,
                PreviousAssigneeUserId = CurrentAssignee(schedule),
                NewAssigneeUserId = target.Id,
                ChangedByUserId = actorId,
                ChangedAt = now,
                Reason = dto.Reason.Trim()
            });

            if (isSupervisorAssignment)
            {
                schedule.AssignedSupervisorUserId = target.Id;
            }
            else
            {
                schedule.AssignedToUserId = target.Id;
            }

            schedule.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await mutationLock.CommitAsync(cancellationToken);

        var workerId = isSupervisorAssignment
            ? GetSingleAssignment(unfinished.Select(schedule => schedule.AssignedToUserId))
            : target.Id;
        var worker = workerId is { } assignedWorkerId
            ? await userManager.FindByIdAsync(assignedWorkerId.ToString())
            : null;
        var supervisorId = isSupervisorAssignment ? target.Id : actorId;
        var supervisorName = isSupervisorAssignment ? target.DisplayName : actor.DisplayName;

        return Results.Ok(new ScheduleAssignmentBatchResponse(
            identity.Department,
            selected.Asset.AssetCategory,
            identity.PmCycle,
            workerId,
            worker?.DisplayName,
            supervisorId,
            supervisorName,
            changed.Select(schedule => schedule.Id).ToArray()));
    }

    private static Guid? GetSingleAssignment(IEnumerable<Guid?> userIds)
    {
        var distinctIds = userIds.Distinct().ToArray();
        return distinctIds.Length == 1 ? distinctIds[0] : null;
    }
}

public sealed record ScheduleResponse(
    Guid Id,
    Guid AssetId,
    DateTimeOffset ScheduleDate,
    string PmCycle,
    string PeriodType,
    string Status,
    string? Quarter,
    string? Semester,
    int? Year,
    string? AcademicYear,
    Guid? AssignedToUserId,
    Guid? AssignedSupervisorUserId,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ScheduleAssetResponse? Asset)
{
    internal static ScheduleResponse FromSchedule(PreventiveMaintenanceSchedule schedule)
    {
        return new ScheduleResponse(
            schedule.Id,
            schedule.AssetId,
            schedule.ScheduleDate,
            PreventiveMaintenanceCycle.ForSchedule(schedule),
            schedule.PeriodType,
            schedule.Status,
            schedule.Quarter,
            schedule.Semester,
            schedule.Year,
            schedule.AcademicYear,
            schedule.AssignedToUserId,
            schedule.AssignedSupervisorUserId,
            schedule.CompletedAt,
            schedule.CreatedAt,
            schedule.UpdatedAt,
            schedule.Asset is null
                ? null
                : new ScheduleAssetResponse(
                    schedule.Asset.Id,
                    schedule.Asset.AssetCode,
                    schedule.Asset.AssetCategory,
                    schedule.Asset.Building,
                    schedule.Asset.Department,
                    schedule.Asset.Location,
                    schedule.Asset.Status,
                    schedule.Asset.QrCodeValue));
    }
}

public sealed record ScheduleRegistryPageResponse(
    IReadOnlyList<ScheduleRegistryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int OverallCount);

public sealed record ScheduleRegistryItemResponse(
    Guid Id,
    Guid AssetId,
    DateTimeOffset ScheduleDate,
    string PmCycle,
    string PeriodType,
    string Status,
    string? Quarter,
    string? Semester,
    int? Year,
    string? AcademicYear,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ScheduleRegistryAssetContext? Asset);

public sealed record ScheduleRegistryAssetContext(
    Guid Id,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Department,
    string? Location);

public sealed record ScheduleRegistryOptionsResponse(
    IReadOnlyList<AssetCategoryResponse> Categories,
    IReadOnlyList<string> Quarters,
    IReadOnlyList<string> Departments,
    IReadOnlyList<string> Years);

public sealed record ScheduleRegistrySummaryResponse(
    int TotalCount,
    int DueCount,
    int OngoingCount,
    int OverdueCount,
    int CompletedCount,
    int CancelledCount);

public sealed record ScheduleAssignmentQueueResponse(
    int TotalScheduleCount,
    IReadOnlyList<ScheduleAssignmentQueueBatchResponse> Batches);

public sealed record ScheduleAssignmentQueueBatchResponse(
    string BatchKey,
    string Department,
    string AssetCategory,
    string PmCycle,
    int ScheduleCount,
    int UnfinishedScheduleCount,
    int CompletedCount,
    int OngoingCount,
    int OverdueCount,
    string SupervisorState,
    Guid? SupervisorUserId,
    string? SupervisorDisplayName,
    string InspectorState,
    Guid? InspectorUserId,
    string? InspectorDisplayName,
    bool AssignmentLocked,
    bool SupervisorOwnershipIsConsistent,
    IReadOnlyList<string> Buildings,
    ScheduleResponse RepresentativeSchedule);

internal sealed record ScheduleRegistryStatusCounts(string Status, int Count);

public sealed record ScheduleAssigneeOption(Guid Id, string DisplayName);

public sealed record ScheduleWorkerAssignmentOptionsResponse(
    IReadOnlyList<ScheduleAssigneeOption> Workers,
    int Page,
    int PageSize,
    int TotalCount,
    ScheduleAssigneeOption? Selected);

public sealed record ScheduleSupervisorAssignmentOptionsResponse(
    IReadOnlyList<ScheduleAssigneeOption> Supervisors,
    int Page,
    int PageSize,
    int TotalCount,
    ScheduleAssigneeOption? Selected);

internal sealed record ScheduleAssignmentOptionsPage(
    IReadOnlyList<ScheduleAssigneeOption> Items,
    int Page,
    int PageSize,
    int TotalCount,
    ScheduleAssigneeOption? Selected);

public sealed record ScheduleCoverageReviewPage(
    int Year,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<ScheduleCoverageReviewItem> Items);

public sealed record ScheduleAssignmentBatchResponse(
    string Department,
    string AssetCategory,
    string PmCycle,
    Guid? WorkerUserId,
    string? WorkerDisplayName,
    Guid SupervisorUserId,
    string SupervisorDisplayName,
    IReadOnlyList<Guid> ScheduleIds);

public sealed class AssignScheduleWorkerDto
{
    public Guid WorkerUserId { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (WorkerUserId == Guid.Empty)
        {
            errors[nameof(WorkerUserId)] = ["An Inspector must be assigned."];
        }

        return errors;
    }
}

public sealed class AssignScheduleSupervisorDto
{
    public Guid SupervisorUserId { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        return SupervisorUserId == Guid.Empty
            ? new Dictionary<string, string[]> { [nameof(SupervisorUserId)] = ["A Supervisor must be assigned."] }
            : [];
    }
}

public sealed class ReassignScheduleBatchDto
{
    public Guid AssigneeUserId { get; set; }
    public string Reason { get; set; } = string.Empty;

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (AssigneeUserId == Guid.Empty)
        {
            errors[nameof(AssigneeUserId)] = ["Choose an active assignee."];
        }

        if (string.IsNullOrWhiteSpace(Reason) || Reason.Trim().Length > 500)
        {
            errors[nameof(Reason)] = ["A reassignment reason is required and must be 500 characters or fewer."];
        }

        return errors;
    }
}

public sealed record ScheduleAssetResponse(
    Guid Id,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Department,
    string? Location,
    string Status,
    string? QrCodeValue);

public class CreateScheduleDto
{
    public Guid AssetId { get; set; }
    public DateTimeOffset? ScheduleDate { get; set; }
    public string? PmCycle { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string? Quarter { get; set; }
    public int? Year { get; set; }

    internal Dictionary<string, string[]> Validate(int currentInstitutionalYear)
    {
        var errors = new Dictionary<string, string[]>();

        if (AssetId == Guid.Empty)
        {
            errors.Add(nameof(AssetId), ["Asset ID is required."]);
        }

        var hasPmCycle = !string.IsNullOrWhiteSpace(PmCycle);
        var hasScheduleDate = ScheduleDate.HasValue && ScheduleDate.Value != default;
        if (ScheduleDate.HasValue && ScheduleDate.Value == default)
        {
            errors[nameof(ScheduleDate)] = ["Schedule date must be valid."];
        }

        if (!hasPmCycle && !hasScheduleDate)
        {
            errors[nameof(ScheduleDate)] = ["Provide a schedule date or PM cycle."];
        }

        var hasSupportedPeriodType = SchedulePeriodTypeCatalog.TryNormalize(
            PeriodType,
            out var normalizedPeriodType);
        if (string.IsNullOrWhiteSpace(PeriodType))
        {
            errors.Add(nameof(PeriodType), ["Period type is required."]);
        }
        else if (!hasSupportedPeriodType)
        {
            errors.Add(nameof(PeriodType), ["Period type must be a supported maintenance period."]);
        }

        if (!ScheduleQuarterCatalog.TryNormalizeNullable(Quarter, out var normalizedQuarter))
        {
            errors.Add(nameof(Quarter), ["Quarter must be one of Q1, Q2, Q3, or Q4."]);
        }

        string? cycleForValidation = null;
        if (hasPmCycle)
        {
            if (!PreventiveMaintenanceCycle.TryParse(PmCycle, out _, out _))
            {
                errors[nameof(PmCycle)] = ["PM cycle must use the yyyy-MM format."];
            }
            else
            {
                cycleForValidation = PmCycle!.Trim();
            }
        }
        else if (hasScheduleDate)
        {
            cycleForValidation = PreventiveMaintenanceCycle.FromScheduleDate(ScheduleDate!.Value);
        }

        if (cycleForValidation is not null
            && PreventiveMaintenanceCycle.TryParse(cycleForValidation, out var cycleYear, out var cycleMonth))
        {
            var expectedQuarter = $"Q{((cycleMonth - 1) / 3) + 1}";
            var yearErrors = new List<string>();
            if (Year is not null && Year != cycleYear)
            {
                yearErrors.Add("Year must match the PM cycle.");
            }

            if (cycleYear != currentInstitutionalYear)
            {
                yearErrors.Add($"Ordinary schedule creation is limited to the current institutional calendar year ({currentInstitutionalYear}).");
            }

            if (yearErrors.Count > 0)
            {
                errors[nameof(Year)] = yearErrors.ToArray();
            }

            if (hasSupportedPeriodType
                && normalizedPeriodType == SchedulePeriodTypeCatalog.Quarter
                && normalizedQuarter is not null
                && normalizedQuarter != expectedQuarter)
            {
                errors[nameof(Quarter)] = ["Quarter must match the PM cycle."];
            }

            if (hasPmCycle && hasScheduleDate
                && !PreventiveMaintenanceCycle.Matches(
                    cycleForValidation,
                    PreventiveMaintenanceCycle.FromScheduleDate(ScheduleDate!.Value)))
            {
                errors[nameof(PmCycle)] = ["PM cycle must match the schedule date's PM cycle in Asia/Manila time."];
            }
        }

        if (!string.IsNullOrWhiteSpace(PeriodType) && PeriodType.Trim().Length > 32)
        {
            errors.Add(nameof(PeriodType), ["Period type must not exceed 32 characters."]);
        }

        return errors;
    }

    internal string ResolvePmCycle()
    {
        if (!string.IsNullOrWhiteSpace(PmCycle))
        {
            return PmCycle.Trim();
        }

        if (ScheduleDate is { } scheduleDate && scheduleDate != default)
        {
            return PreventiveMaintenanceCycle.FromScheduleDate(scheduleDate);
        }

        throw new InvalidOperationException("A valid PM cycle is required before a schedule can be created.");
    }
}

public sealed class GenerateScheduleCyclesDto
{
    public int Year { get; set; }

    internal Dictionary<string, string[]> Validate(int currentInstitutionalYear)
    {
        var errors = new Dictionary<string, string[]>();
        if (Year != currentInstitutionalYear)
        {
            errors[nameof(Year)] = [$"Schedule generation is limited to the current institutional calendar year ({currentInstitutionalYear})."];
        }

        return errors;
    }
}

public sealed record ScheduleEnrollmentDeferralResponse(
    Guid AssetId,
    string AssetCode,
    string Department,
    string AssetCategory,
    string DeferredPmCycle,
    string NextEligiblePmCycle,
    string ReasonCode,
    string Reason,
    string Status,
    DateTimeOffset DeferredAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedByUserId,
    string? ReviewedByDisplayName,
    string? ReviewNote);

public sealed record ScheduleEnrollmentDeferralPage(
    int Page,
    int PageSize,
    int Total,
    int PendingCount,
    int ReviewedCount,
    IReadOnlyList<ScheduleEnrollmentDeferralResponse> Items);

public sealed record ScheduleEnrollmentDeferralReviewRequest(string? Note)
{
    public const int MaximumNoteLength = 1000;
}
