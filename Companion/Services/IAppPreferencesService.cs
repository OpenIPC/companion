using Companion.Models;

namespace Companion.Services;

public interface IAppPreferencesService
{
    AppPreferences Load();
    void Save(AppPreferences preferences);
}
