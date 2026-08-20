using NLog;
using Avalonia;
using System;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Rendering;

namespace ReaStage;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software]
            })
            .With(new X11PlatformOptions
            {
                RenderingMode = [X11RenderingMode.Software]
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
