using Avalonia.Controls;
using Avalonia.Interactivity;
using MsBox.Avalonia.Enums;

namespace Companion.Views;

public partial class SimpleMessageDialog : Window
{
    public SimpleMessageDialog()
    {
        InitializeComponent();
    }

    public SimpleMessageDialog(string title, string message, ButtonEnum buttons = ButtonEnum.Ok) : this()
    {
        Title = title;
        MessageTextBlock.Text = message;
        BuildButtons(buttons);
        ConfigureLayout(buttons, message);
    }

    private void BuildButtons(ButtonEnum buttons)
    {
        foreach (var (caption, result) in GetButtons(buttons))
        {
            var button = new Button
            {
                Content = caption,
                Width = 84
            };
            button.Click += (_, _) => Close(result);
            ButtonsPanel.Children.Add(button);
        }
    }

    private void ConfigureLayout(ButtonEnum buttons, string message)
    {
        var isSimpleOkDialog = buttons == ButtonEnum.Ok && !message.Contains('\n');

        if (isSimpleOkDialog)
        {
            Width = 320;
            Height = 140;
            MinHeight = 120;
            SizeToContent = SizeToContent.Manual;
            return;
        }

        Width = 420;
        Height = double.NaN;
        MinHeight = 140;
        SizeToContent = SizeToContent.Height;
    }

    private static (string Caption, ButtonResult Result)[] GetButtons(ButtonEnum buttons)
    {
        return buttons switch
        {
            ButtonEnum.Ok => [("OK", ButtonResult.Ok)],
            ButtonEnum.YesNo => [("Yes", ButtonResult.Yes), ("No", ButtonResult.No)],
            ButtonEnum.OkCancel => [("OK", ButtonResult.Ok), ("Cancel", ButtonResult.Cancel)],
            ButtonEnum.OkAbort => [("OK", ButtonResult.Ok), ("Abort", ButtonResult.Abort)],
            _ => [("OK", ButtonResult.Ok)]
        };
    }
}
