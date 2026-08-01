namespace ReaStage.Services;

public interface IWebControlServer
{
    // Starts the server if enabled in settings; no-op otherwise
    void Start();

    void Stop();
}
