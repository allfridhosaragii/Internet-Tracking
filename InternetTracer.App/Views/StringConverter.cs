using System;
using Microsoft.UI.Xaml.Data;

namespace InternetTracer_App.Views;

[Windows.Foundation.Metadata.WebHostHidden]
public class StringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return char.ToUpper(str[0]).ToString();
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
