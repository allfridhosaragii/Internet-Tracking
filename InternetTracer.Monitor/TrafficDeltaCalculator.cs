namespace InternetTracer.Monitor;

using InternetTracer.Core.Models;
using System;
using System.Collections.Generic;

public class TrafficDeltaCalculator
{
    private readonly Dictionary<string, NetworkInterfaceInfo> _previousStates = new();

    public IEnumerable<TrafficSample> CalculateDeltas(IEnumerable<NetworkInterfaceInfo> currentStates, DateTime timestampUtc)
    {
        var samples = new List<TrafficSample>();

        foreach (var current in currentStates)
        {
            if (_previousStates.TryGetValue(current.Id, out var previous))
            {
                // Normal delta
                long rxDelta = current.BytesReceived - previous.BytesReceived;
                long txDelta = current.BytesSent - previous.BytesSent;

                // Handle counter wrap or reset
                // In Windows, a counter reset/wrap means current < previous.
                // A true 64-bit wrap is rare, usually it's a reset (e.g. sleep/wake or adapter disable/enable).
                // To prevent massive artificial spikes on reset, we treat negative deltas as 0 for this interval.
                if (rxDelta < 0) rxDelta = 0;
                if (txDelta < 0) txDelta = 0;

                samples.Add(new TrafficSample
                {
                    TimestampUtc = timestampUtc,
                    InterfaceId = current.Id,
                    BytesReceived = rxDelta,
                    BytesSent = txDelta
                });
            }

            // Update state
            _previousStates[current.Id] = current;
        }

        return samples;
    }
}
