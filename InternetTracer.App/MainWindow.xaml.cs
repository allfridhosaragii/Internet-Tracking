using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InternetTracer_App.Views;
using System;

namespace InternetTracer_App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(DashboardPage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            // Settings page not fully implemented yet, but route is active.
            // RootFrame.Navigate(typeof(SettingsPage));
        }
        else
        {
            var item = args.InvokedItemContainer as NavigationViewItem;
            switch (item?.Tag?.ToString())
            {
                case "DashboardPage":
                    RootFrame.Navigate(typeof(DashboardPage));
                    break;
                case "TrafficPage":
                    RootFrame.Navigate(typeof(TrafficPage));
                    break;
                case "TrafficExplorerPage":
                    RootFrame.Navigate(typeof(TrafficExplorerPage));
                    break;
                case "ApplicationsPage":
                    RootFrame.Navigate(typeof(ApplicationsPage));
                    break;
                case "NetworksPage":
                    RootFrame.Navigate(typeof(NetworksPage));
                    break;
                case "SessionsPage":
                    RootFrame.Navigate(typeof(SessionsPage));
                    break;
                case "AnalyticsPage":
                    RootFrame.Navigate(typeof(AnalyticsPage));
                    break;
            }
        }
    }
}
