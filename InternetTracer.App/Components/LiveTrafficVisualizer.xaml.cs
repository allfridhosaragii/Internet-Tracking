using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using System;
using InternetTracer.Core.Contracts;
using InternetTracer.Core.Models;
using InternetTracer_App.Converters;
using Windows.Foundation;
using Microsoft.UI.Xaml.Input;

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
        if (ZeroLine == null) return;
        ZeroLine.Y1 = e.NewSize.Height;
        ZeroLine.Y2 = e.NewSize.Height;
        ZeroLine.X2 = e.NewSize.Width;
        RedrawChart();
    }

    private void RedrawChart()
    {
        if (Timeline == null || Timeline.Points.Count < 2 || ChartCanvas == null || ChartCanvas.ActualWidth == 0 || ChartCanvas.ActualHeight == 0)
        {
            if (DownloadPath != null) DownloadPath.Data = null;
            if (DownloadLinePath != null) DownloadLinePath.Data = null;
            if (UploadPath != null) UploadPath.Data = null;
            if (UploadLinePath != null) UploadLinePath.Data = null;
            if (MaxDownloadText != null) MaxDownloadText.Text = "DL: 0 B/s";
            if (MaxUploadText != null) MaxUploadText.Text = "UL: 0 B/s";
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

        var dlPoints = new List<Point>();
        var ulPoints = new List<Point>();

        DateTime startTime = points.First().TimestampUtc;
        DateTime endTime = points.Last().TimestampUtc;
        double totalSeconds = (endTime - startTime).TotalSeconds;

        if (totalSeconds <= 0) totalSeconds = 1; // Fallback

        foreach (var pt in points)
        {
            double elapsed = (pt.TimestampUtc - startTime).TotalSeconds;
            double x = (elapsed / totalSeconds) * width;
            
            double dlY = height - ((pt.DownloadBytes / maxValue) * height);
            double ulY = height - ((pt.UploadBytes / maxValue) * height);

            dlPoints.Add(new Point(x, dlY));
            ulPoints.Add(new Point(x, ulY));
        }

        DownloadLinePath.Data = CreateSmoothedPath(dlPoints, false, height);
        DownloadPath.Data = CreateSmoothedPath(dlPoints, true, height);
        
        UploadLinePath.Data = CreateSmoothedPath(ulPoints, false, height);
        UploadPath.Data = CreateSmoothedPath(ulPoints, true, height);

        // Update labels
        MaxDownloadText.Text = $"DL: {_valueConverter.Convert(maxDownload, typeof(string), string.Empty, string.Empty)} {_unitConverter.Convert(maxDownload, typeof(string), string.Empty, string.Empty)}/s";
        MaxUploadText.Text = $"UL: {_valueConverter.Convert(maxUpload, typeof(string), string.Empty, string.Empty)} {_unitConverter.Convert(maxUpload, typeof(string), string.Empty, string.Empty)}/s";
    }

    private PathGeometry CreateSmoothedPath(List<Point> points, bool closePath, double height)
    {
        PathGeometry geometry = new PathGeometry();
        PathFigure figure = new PathFigure { StartPoint = points[0] };
        
        if (points.Count > 1)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = i > 0 ? points[i - 1] : points[0];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = i < points.Count - 2 ? points[i + 2] : p2;

                // Tension factor for Catmull-Rom
                double tension = 0.2;

                var cp1 = new Point(p1.X + (p2.X - p0.X) * tension, p1.Y + (p2.Y - p0.Y) * tension);
                var cp2 = new Point(p2.X - (p3.X - p1.X) * tension, p2.Y - (p3.Y - p1.Y) * tension);

                figure.Segments.Add(new BezierSegment { Point1 = cp1, Point2 = cp2, Point3 = p2 });
            }
        }

        if (closePath)
        {
            if (points.Count > 0)
            {
                figure.Segments.Add(new LineSegment { Point = new Point(points.Last().X, height) });
                figure.Segments.Add(new LineSegment { Point = new Point(points.First().X, height) });
            }
            figure.IsClosed = true;
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Timeline == null || Timeline.Points.Count == 0 || ChartCanvas == null || TooltipCanvas == null) return;

        var position = e.GetCurrentPoint(ChartCanvas).Position;
        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;

        if (width == 0 || height == 0) return;

        var points = Timeline.Points.OrderBy(p => p.TimestampUtc).ToList();
        
        DateTime startTime = points.First().TimestampUtc;
        DateTime endTime = points.Last().TimestampUtc;
        double totalSeconds = (endTime - startTime).TotalSeconds;
        if (totalSeconds <= 0) totalSeconds = 1;

        // Find the closest point based on X coordinate
        TrafficTimelinePoint closestPoint = points[0];
        double minDistance = double.MaxValue;
        double closestX = 0;

        foreach (var pt in points)
        {
            double elapsed = (pt.TimestampUtc - startTime).TotalSeconds;
            double x = (elapsed / totalSeconds) * width;
            double distance = Math.Abs(x - position.X);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = pt;
                closestX = x;
            }
        }

        TooltipLine.Visibility = Visibility.Visible;
        TooltipBox.Visibility = Visibility.Visible;
        
        TooltipLine.X1 = closestX;
        TooltipLine.X2 = closestX;
        TooltipLine.Y1 = 0;
        TooltipLine.Y2 = height;

        TooltipTimeText.Text = closestPoint.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        TooltipDlText.Text = $"{_valueConverter.Convert(closestPoint.DownloadBytes, typeof(string), string.Empty, string.Empty)} {_unitConverter.Convert(closestPoint.DownloadBytes, typeof(string), string.Empty, string.Empty)}/s";
        TooltipUlText.Text = $"{_valueConverter.Convert(closestPoint.UploadBytes, typeof(string), string.Empty, string.Empty)} {_unitConverter.Convert(closestPoint.UploadBytes, typeof(string), string.Empty, string.Empty)}/s";

        // Position tooltip box
        TooltipBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double boxWidth = TooltipBox.DesiredSize.Width;
        double boxHeight = TooltipBox.DesiredSize.Height;

        double boxX = closestX + 12;
        if (boxX + boxWidth > width) boxX = closestX - boxWidth - 12;
        
        double boxY = position.Y - (boxHeight / 2);
        if (boxY < 0) boxY = 0;
        if (boxY + boxHeight > height) boxY = height - boxHeight;

        Canvas.SetLeft(TooltipBox, boxX);
        Canvas.SetTop(TooltipBox, boxY);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (TooltipLine != null) TooltipLine.Visibility = Visibility.Collapsed;
        if (TooltipBox != null) TooltipBox.Visibility = Visibility.Collapsed;
    }
}
