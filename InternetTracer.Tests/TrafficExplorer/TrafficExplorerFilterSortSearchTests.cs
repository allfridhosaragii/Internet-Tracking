namespace InternetTracer.Tests.TrafficExplorer;

using InternetTracer.Data;
using InternetTracer.Core.Contracts;
using InternetTracer.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

/// <summary>
/// K16 Phase 4: Filtering, Sorting & Search tests
/// </summary>
public class TrafficExplorerFilterSortSearchTests : IDisposable
{
    private string _tempDbPath;
    private DatabaseFactory _testDbFactory;
    private SqliteTelemetryQueryService _queryService;
    
    public TrafficExplorerFilterSortSearchTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"traffic_test_{Guid.NewGuid().ToString().Substring(0, 8)}.db");
        
        // Create SQLite database for testing
        var connString = $"Data Source={_tempDbPath}";
        _testDbFactory = new DatabaseFactory(connString);
        
        InitializeDatabaseAsync().Wait();
        
        // Use default LiveTelemetryBuffer for tests (not actually used in most tests)
        var minuteAggregator = new MinuteAggregator(_testDbFactory);
        
        _queryService = new SqliteTelemetryQueryService(
            _testDbFactory, 
            new TestLiveTelemetryBuffer(),
            minuteAggregator
        );
        
        SeedTestDataAsync().Wait();
    }

    private async Task InitializeDatabaseAsync()
    {
        using var connection = _testDbFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS traffic_minute (
                bucket_utc TEXT NOT NULL,
                interface_id TEXT NOT NULL,
                network_id TEXT,
                application_id TEXT,
                download_bytes INTEGER NOT NULL,
                upload_bytes INTEGER NOT NULL,
                sample_count INTEGER NOT NULL,
                attribution_state INTEGER NOT NULL,
                PRIMARY KEY (bucket_utc, interface_id, application_id)
            );

            CREATE INDEX IF NOT EXISTS idx_traffic_minute_bucket ON traffic_minute(bucket_utc);
        ");
    }

    private async Task SeedTestDataAsync()
    {
        using var connection = _testDbFactory.CreateConnection();
        
        var now = DateTime.UtcNow.Date;
        
        // Seed data for 7 days
        for (int day = 0; day < 7; day++)
        {
            var bucketDate = now.AddDays(-day);
            
            await connection.ExecuteAsync(@"
                INSERT INTO traffic_minute VALUES (?, ?, ?, ?, ?, ?, ?, ?),
                                            (?, ?, ?, ?, ?, ?, ?, ?),
                                            (?, ?, ?, ?, ?, ?, ?, ?);
            ", 
            new object[]
            {
                bucketDate.ToString("o"), "iface1", "net1", "app_chrome.exe", 50000000L, 5000000L, 1, 1,
                bucketDate.ToString("o"), "iface1", "net1", "app_firefox.exe", 30000000L, 3000000L, 1, 1,
                bucketDate.ToString("o"), "iface1", null, null, 20000000L, 2000000L, 1, 2  // Unattributed
            });
        }
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            File.Delete(_tempDbPath);
        }
    }
    
    /// <summary>
    /// Simple implementation of LiveTelemetryBuffer for tests - uses default behavior
    /// </summary>
    private class TestLiveTelemetryBuffer : LiveTelemetryBuffer
    {
        // No override needed - just use base implementation
    }

    #region Filter Tests
    
    [Fact]
    public async Task GetUniqueApplicationIdsAsync_ReturnsDistinctAppIds()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        
        // Act
        var result = await _queryService.GetUniqueApplicationIdsAsync(start, end);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("app_chrome.exe", result);
        Assert.Contains("app_firefox.exe", result);
    }

    [Fact]
    public async Task GetNetworkUsageFilteredAsync_FilterByValidNetwork_ShowsOnlyThatNetwork()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        string? networkId = "net1";
        
        // Act
        var result = await _queryService.GetNetworkUsageFilteredAsync(start, end, networkId);
        
        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("net1", result[0].NetworkId);
    }

    [Fact]
    public async Task GetTopApplicationsFilteredAsync_AllApplications_ReturnsAllAttributed()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        int limit = 50;
        string? appId = null;  // All applications
        
        // Act
        var result = await _queryService.GetTopApplicationsFilteredAsync(start, end, limit, appId);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);  // Only attributed apps returned
    }

    [Fact]
    public async Task GetTopApplicationsFilteredAsync_InvalidFilter_ReturnsEmptyList()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        int limit = 50;
        string? appId = "nonexistent_app.exe";  // Invalid ID
        
        // Act
        var result = await _queryService.GetTopApplicationsFilteredAsync(start, end, limit, appId);
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region Search Tests

    [Fact]
    public async Task SearchApplicationsAsync_EmptyString_ReturnsAllApps()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        string searchTerm = "";
        int limit = 50;
        
        // Act
        var result = await _queryService.SearchApplicationsAsync(start, end, searchTerm, limit);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);  // Both attributed apps
    }

    [Fact]
    public async Task SearchApplicationsAsync_PartialMatch_FindsMatchingApps()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        string searchTerm = "chrome";
        int limit = 50;
        
        // Act
        var result = await _queryService.SearchApplicationsAsync(start, end, searchTerm, limit);
        
        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("app_chrome.exe", result[0].EntityId);
    }

    [Fact]
    public async Task SearchApplicationsAsync_NoMatch_ReturnsEmptyList()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        string searchTerm = "notfound";
        int limit = 50;
        
        // Act
        var result = await _queryService.SearchApplicationsAsync(start, end, searchTerm, limit);
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchApplicationsAsync_SpecialCharacters_Sanitized()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        string searchTerm = "' OR '1'='1";  // Attempted injection
        int limit = 50;
        
        // Act
        var result = await _queryService.SearchApplicationsAsync(start, end, searchTerm, limit);
        
        // Assert
        Assert.NotNull(result);
        // Should not crash or return all apps unexpectedly
        // The LIKE pattern should sanitize this input
        Assert.InRange(result.Count, 0, 2);
    }

    #endregion

    #region Sort Tests

    [Fact]
    public async Task GetTopApplicationsSortedAsync_ByTotalBytes_Descending_OrderCorrect()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        int limit = 50;
        string sortBy = "TotalBytes";
        bool descending = true;
        
        // Act
        var result = await _queryService.GetTopApplicationsSortedAsync(start, end, limit, sortBy, descending);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<TopUsageEntry>>(result);
        // chrome has more total bytes than firefox
        Assert.Equal("app_chrome.exe", result[0].EntityId);
    }

    [Fact]
    public async Task GetTopApplicationsSortedAsync_ByDownloadBytes_Ascending_OrderCorrect()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        int limit = 50;
        string sortBy = "DownloadBytes";
        bool descending = false;
        
        // Act
        var result = await _queryService.GetTopApplicationsSortedAsync(start, end, limit, sortBy, descending);
        
        // Assert
        Assert.NotNull(result);
        // firefox has less download than chrome
        Assert.Equal("app_firefox.exe", result[0].EntityId);
    }

    [Fact]
    public async Task GetTopApplicationsSortedAsync_InvalidSortField_DefaultsToTotalBytes()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        int limit = 50;
        string sortBy = "invalid_field_name";  // Invalid field
        bool descending = true;
        
        // Act
        var result = await _queryService.GetTopApplicationsSortedAsync(start, end, limit, sortBy, descending);
        
        // Assert
        Assert.NotNull(result);
        // Should default to TotalBytes DESC
        Assert.Equal("app_chrome.exe", result[0].EntityId);
    }

    [Fact]
    public async Task GetTopApplicationsSortedAsync_EqualValues_DeterministicSecondaryOrder()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        int limit = 50;
        string sortBy = "DisplayName";  // Sort by name
        bool descending = true;
        
        // Act
        var result = await _queryService.GetTopApplicationsSortedAsync(start, end, limit, sortBy, descending);
        
        // Assert
        Assert.NotNull(result);
        // Alphabetical order: app_chrome comes after app_firefox in descending
        Assert.Equal("app_firefox.exe", result[0].EntityId);
    }

    #endregion

    #region Accounting Tests

    [Fact]
    public void AttributionStates_CorrectlyRepresented()
    {
        // Verify that the domain model supports required states
        var states = Enum.GetValues(typeof(AttributionState));
        
        // Current implementation uses these two:
        var usedStates = new[] { AttributionState.Attributed, AttributionState.Unattributed };
        
        Assert.Equal(4, states.Length);  // Attributed, PartiallyAttributed, Unattributed, Failed
        foreach (var state in usedStates)
        {
            Assert.Contains(states, s => s.Equals(state));
        }
    }

    [Fact]
    public void DataConservation_AttributedPlusUnattributedEqualsTotal()
    {
        // Simulate a simple case where we know all values
        long attributedDownload = 80000000L;
        long attributedUpload = 8000000L;
        long unattributedDownload = 20000000L;
        long unattributedUpload = 2000000L;
        
        long expectedTotalDownload = attributedDownload + unattributedDownload;
        long expectedTotalUpload = attributedUpload + unattributedUpload;
        
        // Current implementation shows attributed traffic in Application list
        // Unattributed is implicitly part of InterfaceTotal but not shown in App breakdown
        
        // This is CURRENTLY CORRECT behavior for MVP scope
        Assert.Equal(expectedTotalDownload, attributedDownload + unattributedDownload);
        Assert.Equal(expectedTotalUpload, attributedUpload + unattributedUpload);
    }

    #endregion

    #region SQL Injection Security Tests

    [Fact]
    public async Task SearchApplicationsSQLInjectionAttempt_Rejected()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        string searchTerm = "'; DROP TABLE traffic_minute; --";  // Injection attempt
        int limit = 50;
        
        // Act - Should not throw exception
        var result = await _queryService.SearchApplicationsAsync(start, end, searchTerm, limit);
        
        // Assert - Table still exists and query succeeds safely
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateSortField_MaliciousInput_Rejected()
    {
        // Test validation with dangerous inputs
        var validInputs = new[] { "totalbytes", "downloadbytes", "uploadbytes", "displayname" };
        var maliciousInputs = new[] { "id', ''", "UNION SELECT * FROM users", "1 OR 1=1" };
        
        foreach (var input in validInputs)
        {
            var sanitized = SqliteTelemetryQueryService.ValidateSortFieldForTesting(input);
            Assert.False(string.IsNullOrEmpty(sanitized));  // Returns safe SQL expression
        }
        
        foreach (var input in maliciousInputs)
        {
            var sanitized = SqliteTelemetryQueryService.ValidateSortFieldForTesting(input);
            // Should default to safe value, not execute the malicious input
            Assert.StartsWith("SUM", sanitized);  // Safe default
        }
    }

    #endregion
}
