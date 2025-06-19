using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Companion.Views;

public partial class PresetsAddRepoView : UserControl
{
    public PresetsAddRepoView()
    {
        if (!Design.IsDesignMode)
            InitializeComponent();
    }
}