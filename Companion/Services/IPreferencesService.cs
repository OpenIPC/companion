using Companion.Models;

namespace Companion.Services;

public interface IPreferencesService
{
    UserPreferences Load();
    void Save(UserPreferences preferences);
}
