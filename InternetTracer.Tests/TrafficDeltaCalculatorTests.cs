namespace InternetTracer.Tests;

using InternetTracer.Monitor;
using InternetTracer.Core.Models;
using System;
using System.Linq;
using Xunit;

public class TrafficDeltaCalculatorTests
{
    [Fact]
    public void CalculateDeltas_NormalTraffic_CalculatesCorrectly()
    {
        var calc = new TrafficDeltaCalculator();
        var time1 = DateTime.UtcNow;
        
        var state1 = new[] { new NetworkInterfaceInfo { Id = "if1", BytesReceived = 100, BytesSent = 50 } };
        var deltas1 = calc.CalculateDeltas(state1, time1).ToList();
        
        Assert.Empty(deltas1); // First run, no baseline
        
        var time2 = time1.AddSeconds(1);
        var state2 = new[] { new NetworkInterfaceInfo { Id = "if1", BytesReceived = 150, BytesSent = 75 } };
        var deltas2 = calc.CalculateDeltas(state2, time2).ToList();
        
        Assert.Single(deltas2);
        Assert.Equal("if1", deltas2[0].InterfaceId);
        Assert.Equal(50, deltas2[0].BytesReceived);
        Assert.Equal(25, deltas2[0].BytesSent);
    }
    
    [Fact]
    public void CalculateDeltas_CounterReset_ReturnsZeroDeltaToPreventSpikes()
    {
        var calc = new TrafficDeltaCalculator();
        var time1 = DateTime.UtcNow;
        
        var state1 = new[] { new NetworkInterfaceInfo { Id = "if1", BytesReceived = 1000, BytesSent = 1000 } };
        calc.CalculateDeltas(state1, time1);
        
        var time2 = time1.AddSeconds(1);
        var state2 = new[] { new NetworkInterfaceInfo { Id = "if1", BytesReceived = 100, BytesSent = 100 } }; // Reset
        var deltas2 = calc.CalculateDeltas(state2, time2).ToList();
        
        Assert.Single(deltas2);
        Assert.Equal(0, deltas2[0].BytesReceived);
        Assert.Equal(0, deltas2[0].BytesSent);
    }
}
