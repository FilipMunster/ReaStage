using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ReaStage.Core;
using System;
using System.IO;
using System.Reflection;

namespace ReaStage.Services;

// Minimal ASP.NET Core (Kestrel) server exposing the stage view for phones on the
// LAN. No authentication by design. Page polls /state and posts to /cmd/*.
internal sealed class WebControlServer : IWebControlServer, IDisposable
{
    private const string IndexResource = "ReaStage.Web.index.html";

    private readonly IPlaybackCoordinator coordinator;
    private readonly IRegionCatalog regionCatalog;
    private readonly ISettingsService settingsService;
    private readonly ILogger<WebControlServer> logger;
    private WebApplication? app;

    public WebControlServer(
        IPlaybackCoordinator coordinator,
        IRegionCatalog regionCatalog,
        ISettingsService settingsService,
        ILogger<WebControlServer> logger)
    {
        this.coordinator = coordinator;
        this.regionCatalog = regionCatalog;
        this.settingsService = settingsService;
        this.logger = logger;
    }

    public void Start()
    {
        AppSettings.WebSettings web = settingsService.Settings.Web;
        if (!web.Enabled || app is not null)
        {
            return;
        }

        try
        {
            string indexHtml = LoadIndexHtml();

            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(web.Port));

            WebApplication application = builder.Build();

            application.MapGet("/", () => Results.Content(indexHtml, "text/html; charset=utf-8"));
            application.MapGet("/state", () => Results.Json(BuildState()));
            application.MapPost("/cmd/playpause", async () => { await coordinator.TogglePlayPauseAsync(); return Results.Ok(); });
            application.MapPost("/cmd/prev", async () => { await coordinator.GoToPreviousAsync(); return Results.Ok(); });
            application.MapPost("/cmd/next", async () => { await coordinator.GoToNextAsync(); return Results.Ok(); });
            application.MapPost("/cmd/jump/{index:int}", async (int index) => { await coordinator.JumpToItemAtAsync(index); return Results.Ok(); });

            app = application;
            _ = application.StartAsync();
            logger.LogInformation("Web control server listening on port {Port}", web.Port);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start web control server");
            app = null;
        }
    }

    public void Stop()
    {
        if (app is null)
        {
            return;
        }

        try
        {
            app.StopAsync().GetAwaiter().GetResult();
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop web control server");
        }
        finally
        {
            app = null;
        }
    }

    private WebState BuildState()
    {
        return WebStateBuilder.Build(
            coordinator.ActiveItems,
            coordinator.CurrentIndex,
            coordinator.Position,
            regionCatalog.IsReaperReachable,
            DateTime.Now);
    }

    private static string LoadIndexHtml()
    {
        Assembly assembly = typeof(WebControlServer).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(IndexResource);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{IndexResource}' not found.");
        }

        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        Stop();
    }
}
