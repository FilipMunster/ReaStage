using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Extensions.Logging;
using ReaStage.Factories;
using ReaStage.ViewModels;
using ReaStage.Views;
using System;
using System.Linq;
using ReaStage.Services;

namespace ReaStage;

public partial class App : Application
{
    private ServiceProvider serviceProvider = null!;
    private IConfigurationRoot configuration = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        serviceProvider = ConfigureServices()
            .BuildServiceProvider(validateScopes: true);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var windowFactory = serviceProvider.GetRequiredService<WindowFactory>();
            desktop.MainWindow = windowFactory.Create<MainWindow, MainWindowViewModel>();
            //desktop.MainWindow.RendererDiagnostics.DebugOverlays = Avalonia.Rendering.RendererDebugOverlays.Fps | Avalonia.Rendering.RendererDebugOverlays.DirtyRects;
            desktop.MainWindow.Show();

            var webServer = serviceProvider.GetRequiredService<IWebControlServer>();
            webServer.Start();
            desktop.ShutdownRequested += (_, _) => webServer.Stop();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private IServiceCollection ConfigureServices()
    {
        return new ServiceCollection()
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                loggingBuilder.AddNLog(configuration);
            })
            .AddSingleton(configuration)
            .AddSingleton<WindowFactory>()
            .AddSingleton<ISettingsService, SettingsService>()
            .AddSingleton<IReaperClient, ReaperClient>()
            .AddSingleton<IRegionCatalog, RegionCatalog>()
            .AddSingleton<IPlaylistService, PlaylistService>()
            .AddSingleton<IPlaybackCoordinator, PlaybackCoordinator>()
            .AddSingleton<IWebControlServer, WebControlServer>()
            .AddSingleton<StageViewModel>()
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<PlaylistEditorViewModel>()
            ;
    }
}