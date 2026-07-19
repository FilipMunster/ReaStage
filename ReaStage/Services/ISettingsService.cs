using ReaStage.Core;

namespace ReaStage.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    void Save();
}
