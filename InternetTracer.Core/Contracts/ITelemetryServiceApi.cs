namespace InternetTracer.Core.Contracts;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;

// Defines the strict IPC boundary. The UI only talks to the Service via this interface over Named Pipes.
// This ensures the UI never accesses SQLite or ETW directly.
public interface ITelemetryServiceApi
{
    Task<DashboardSummary> GetDashboardSummaryAsync();
    Task<TrafficTimeline> GetTrafficTimelineAsync(DateTime startUtc, DateTime endUtc, string resolution);
    Task<List<TopUsageEntry>> GetTopApplicationsAsync(DateTime startUtc, DateTime endUtc, int limit);
    Task<List<NetworkUsage>> GetNetworkUsageAsync(DateTime startUtc, DateTime endUtc);
    Task<ApplicationUsage> GetApplicationUsageAsync(string applicationId, DateTime startUtc, DateTime endUtc);
    Task<List<ConnectionEvent>> GetConnectionEventsAsync(int limit);
}
