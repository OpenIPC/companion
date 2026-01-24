using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Companion.Models.Presets;

public class FileModification
{
    public string FileName { get; set; } = string.Empty;
    public ObservableCollection<KeyValuePair<string, string>> Changes { get; set; }

    public FileModification()
    {
        Changes = new ObservableCollection<KeyValuePair<string, string>>();
    }
}
