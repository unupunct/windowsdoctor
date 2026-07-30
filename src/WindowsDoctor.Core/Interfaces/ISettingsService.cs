using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Loads and persists user-configurable <see cref="AppSettings"/> to a local settings store
/// (e.g. a JSON file in the application's data directory).
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the current application settings from the backing store.
    /// Returns a default <see cref="AppSettings"/> instance when no settings file exists yet.
    /// </summary>
    /// <returns>
    /// The persisted <see cref="AppSettings"/>, or a sensible default when the file is absent
    /// or cannot be parsed.
    /// </returns>
    AppSettings Load();

    /// <summary>
    /// Persists the supplied <see cref="AppSettings"/> to the backing store,
    /// overwriting any previously saved values.
    /// </summary>
    /// <param name="settings">The settings to save. Must not be <see langword="null"/>.</param>
    void Save(AppSettings settings);
}
