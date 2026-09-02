using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternetTracer_App.Components;

[Microsoft.UI.Xaml.Markup.ContentProperty(Name = "InnerContent")]
public sealed partial class DataStateContainer : UserControl
{
    public DataStateContainer()
    {
        this.InitializeComponent();
        UpdateState();
    }

    public static readonly DependencyProperty InnerContentProperty =
        DependencyProperty.Register("InnerContent", typeof(object), typeof(DataStateContainer), new PropertyMetadata(null));

    public object InnerContent
    {
        get => GetValue(InnerContentProperty);
        set => SetValue(InnerContentProperty, value);
    }

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register("State", typeof(ComponentDataState), typeof(DataStateContainer), new PropertyMetadata(ComponentDataState.Normal, OnStateChanged));

    public ComponentDataState State
    {
        get => (ComponentDataState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly DependencyProperty LoadingMessageProperty =
        DependencyProperty.Register("LoadingMessage", typeof(string), typeof(DataStateContainer), new PropertyMetadata("Loading..."));

    public string LoadingMessage
    {
        get => (string)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public static readonly DependencyProperty EmptyMessageProperty =
        DependencyProperty.Register("EmptyMessage", typeof(string), typeof(DataStateContainer), new PropertyMetadata("No data available."));

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public static readonly DependencyProperty OfflineMessageProperty =
        DependencyProperty.Register("OfflineMessage", typeof(string), typeof(DataStateContainer), new PropertyMetadata("Internet Tracer is running, but the telemetry service is unavailable."));

    public string OfflineMessage
    {
        get => (string)GetValue(OfflineMessageProperty);
        set => SetValue(OfflineMessageProperty, value);
    }

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register("ErrorMessage", typeof(string), typeof(DataStateContainer), new PropertyMetadata("An error occurred."));

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataStateContainer container)
        {
            container.UpdateState();
        }
    }

    private void UpdateState()
    {
        MainContentPresenter.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Collapsed;
        EmptyPanel.Visibility = Visibility.Collapsed;
        OfflinePanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;

        switch (State)
        {
            case ComponentDataState.Normal:
            case ComponentDataState.Stale:
            case ComponentDataState.Degraded:
                MainContentPresenter.Visibility = Visibility.Visible;
                break;
            case ComponentDataState.Loading:
                LoadingPanel.Visibility = Visibility.Visible;
                break;
            case ComponentDataState.Empty:
                EmptyPanel.Visibility = Visibility.Visible;
                break;
            case ComponentDataState.Offline:
                OfflinePanel.Visibility = Visibility.Visible;
                break;
            case ComponentDataState.Error:
                ErrorPanel.Visibility = Visibility.Visible;
                break;
        }
    }
}
