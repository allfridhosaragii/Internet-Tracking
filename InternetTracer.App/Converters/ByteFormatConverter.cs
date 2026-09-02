namespace InternetTracer_App.Converters;

using Microsoft.UI.Xaml.Data;
using System;

public class ByteFormatValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not long bytes) return "0";
        
        double val = bytes;
        if (val < 1024) return val.ToString("F0");
        val /= 1024;
        if (val < 1024) return val.ToString("F1");
        val /= 1024;
        if (val < 1024) return val.ToString("F1");
        val /= 1024;
        return val.ToString("F2");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class ByteFormatUnitConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not long bytes) return "B/s";
        
        double val = bytes;
        if (val < 1024) return "B/s";
        val /= 1024;
        if (val < 1024) return "KB/s";
        val /= 1024;
        if (val < 1024) return "MB/s";
        val /= 1024;
        return "GB/s";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class ByteVolumeFormatUnitConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not long bytes) return "B";
        
        double val = bytes;
        if (val < 1024) return "B";
        val /= 1024;
        if (val < 1024) return "KB";
        val /= 1024;
        if (val < 1024) return "MB";
        val /= 1024;
        if (val < 1024) return "GB";
        val /= 1024;
        return "TB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

