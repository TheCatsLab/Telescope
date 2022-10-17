using Cats.Telescope.VsExtension.Core.Settings;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using System;

namespace Cats.Telescope.VsExtension.Core.Utils;

/// <summary>
/// Presents a functional service for management of user settings
/// </summary>
internal class UserSettingsService
{
    private static WritableSettingsStore _userSettingsStore;
    private static UserSettingsService _instance;

    public static UserSettingsService Instance => _instance ??= new UserSettingsService();

    public UserSettingsService()
    {
    }

    public static void Initialize(IServiceProvider serviceProvider)
    {
        SettingsManager settingsManager = new ShellSettingsManager(serviceProvider);
        _userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
    }

    /// <summary>
    /// Save <paramref name="setting"/> to the user setting store
    /// </summary>
    /// <param name="setting"><see cref="IStringSetting"/></param>
    /// <exception cref="ArgumentNullException">occurs if <paramref name="setting"/> is null</exception>
    /// <exception cref="ApplicationException">occurs if <see cref="Initialize(IServiceProvider)"/> method has not been called yet</exception>
    public void SetSetting(IStringSetting setting)
    {
        if (setting is null)
            throw new ArgumentNullException(nameof(setting));

        if (_userSettingsStore is null)
            throw new ApplicationException("The service is not initialized yet");

        if (!_userSettingsStore.CollectionExists(setting.Collection))
            _userSettingsStore.CreateCollection(setting.Collection);

        _userSettingsStore.SetString(setting.Collection, setting.Key, setting.Value);
    }

    /// <summary>
    /// Extracs value of setting of the <typeparamref name="TSetting"/> type from the user setting store
    /// </summary>
    /// <typeparam name="TSetting">a setting type</typeparam>
    /// <returns></returns>
    public TSetting GetSetting<TSetting>() where TSetting : StringSetting
    {
        TSetting setting = Activator.CreateInstance<TSetting>();

        string collection = setting.Collection;
        string key = setting.Key;

        if (!_userSettingsStore.CollectionExists(collection))
            _userSettingsStore.CreateCollection(collection);

        if (!_userSettingsStore.PropertyExists(collection, key))
            return null;

        string settingStringValue = _userSettingsStore.GetString(collection, key);
        setting.Value = settingStringValue;

        return setting;
    }
}