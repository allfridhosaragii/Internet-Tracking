using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace InternetTracer_App.Components;

public sealed partial class TimeRangeSelector : UserControl
{
    public static readonly DependencyProperty SelectTimeRangeCommandProperty =
        DependencyProperty.Register(
            "SelectTimeRangeCommand",
            typeof(ICommand),
            typeof(TimeRangeSelector),
            new PropertyMetadata(null));

    public ICommand? SelectTimeRangeCommand
    {
        get => (ICommand?)GetValue(SelectTimeRangeCommandProperty);
        set => SetValue(SelectTimeRangeCommandProperty, value);
    }

    public TimeRangeSelector()
    {
        this.InitializeComponent();
    }
}
