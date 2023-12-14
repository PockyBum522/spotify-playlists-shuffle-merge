using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SpotifyPlaylistUtility.Logic;
using SpotifyPlaylistUtility.ViewModels;
using SpotifyPlaylistUtility.Views;

namespace SpotifyPlaylistUtility;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Dependency Injection:
        var loggerConfiguration = LoggerSetup.ConfigureLogger();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(
                    loggerConfiguration)
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel(
                    loggerConfiguration)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}