using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniPM.Api.Models;

namespace UniPM.Api.Features.MaintenanceHistory;

public interface IMaintenanceHistoryRetrievalService
{
    /// <summary>
    /// Retrieves relevant maintenance history records using a hybrid search approach.
    /// This will be used to bound the context before sending it to an LLM for summarization.
    /// </summary>
    Task<IEnumerable<InspectionRecord>> RetrieveRelevantHistoryAsync(
        Guid assetId, 
        string query, 
        int maxResults = 5,
        CancellationToken cancellationToken = default);
}
