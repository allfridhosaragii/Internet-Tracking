using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using System;
using InternetTracer.Core.Contracts;
using InternetTracer_App.Converters;
using Windows.Foundation;

namespace InternetTracer_App.Components;

public sealed partial class LiveTrafficVisualizer : UserControl
{
    private readonly ByteFormatValueConverter _valueConverter = new();
    private readonly ByteFormatUnitConverter _unitConverter = new();

    public LiveTrafficVisualizer()
    {
        this.InitializeComponent();
    }

    public static readonly DependencyProperty TimelineProperty =
        DependencyProperty.Register("Timeline", typeof(TrafficTimeline), typeof(LiveTrafficVisualizer), new PropertyMetadata(null, OnTimelineChanged));

    public TrafficTimeline Timeline
    {
        get => (TrafficTimeline)GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LiveTrafficVisualizer visualizer)
        {
            visualizer.RedrawChart();
        }
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ZeroLine.Y1 = e.NewSize.Height;
        ZeroLine.Y2 = e.NewSize.Height;
        ZeroLine.X2 = e.NewSize.Width;
        RedrawChart();
    }

    private void RedrawChart()
    {
        if (Timeline == null || Timeline.Points.Count < 2 || ChartCanvas.ActualWidth == 0 || ChartCanvas.ActualHeight == 0)
        {
            DownloadPolygon.Points.Clear();
            DownloadLine.Points.Clear();
            UploadPolygon.Points.Clear();
            UploadLine.Points.Clear();
            MaxDownloadText.Text = "DL: 0 B/s";
            MaxUploadText.Text = "UL: 0 B/s";
            return;
        }

        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;

        var points = Timeline.Points.OrderBy(p => p.TimestampUtc).ToList();
        
        long maxDownload = points.Max(p => p.DownloadBytes);
        long maxUpload = points.Max(p => p.UploadBytes);
        long maxOverall = Math.Max(maxDownload, Math.Max(maxUpload, 1024)); // Minimum 1KB/s scale

        // Prevent division by zero and pad the top
        double maxValue = maxOverall * 1.1;

        var dlPolygonPoints = new PointCollection();
        var dlLinePoints = new PointCollection();
        
        var ulPolygonPoints = new PointCollection();
        var ulLinePoints = new PointCollection();

        DateTime startTime = points.First().TimestampUtc;
        DateTime endTime = points.Last().TimestampUtc;
        double totalSeconds = (endTime - startTime).TotalSeconds;

        if (totalSeconds <= 0) totalSeconds = 1; // Fallback

        // Start polygons at bottom-left of the first point
        dlPolygonPoints.Add(new Point(0, height));
        ulPolygonPoints.Add(new Point(0, height));

        foreach (var pt in points)
        {
            double elapsed = (pt.TimestampUtc - startTime).TotalSeconds;
            double x = (elapsed / totalSeconds) * width;
            
            double dlY = height - ((pt.DownloadBytes / maxValue) * height);
            double ulY = height - ((pt.UploadBytes / maxValue) * height);

            dlPolygonPoints.Add(new Point(x, dlY));
            dlLinePoints.Add(new Point(x, dlY));
            
            ulPolygonPoints.Add(new Point(x, ulY));
            ulLinePoints.Add(new Point(x, ulY));
        }

        // End polygons at bottom-right of the last point
        dlPolygonPoints.Add(new Point(width, height));
        ulPolygonPoints.Add(new Point(width, height));

        DownloadPolygon.Points = dlPolygonPoints;
        DownloadLine.Points = dlLinePoints;
        
        UploadPolygon.Points = ulPolygonPoints;
        UploadLine.Points = ulLinePoints;

        // Update labels
        MaxDownloadText.Text = $"DL: {_valueConverter.Convert(maxDownload, typeof(string), null, "")} {_unitConverter.Convert(maxDownload, typeof(string), null, "")}";
        MaxUploadText.Text = $"UL: {_valueConverter.Convert(maxUpload, typeof(string), null, "")} {_unitConverter.Convert(maxUpload, typeof(string), null, "")}";
    }
}
