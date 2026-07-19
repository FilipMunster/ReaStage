using ReaStage.Core;
using System;

namespace ReaStage.Services;

public interface ISettingsService
{
    event EventHandler? SettingsSaved;

    AppSettings Settings { get; }

    void Save();
}
