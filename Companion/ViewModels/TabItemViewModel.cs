using System;

namespace Companion.ViewModels;

/// <summary>
/// ViewModel for managing individual tab items in the application
/// </summary>
public class TabItemViewModel
{
    #region Public Properties
    /// <summary>
    /// Gets the display name of the tab
    /// </summary>
    public string TabName { get; }

    /// <summary>
    /// Gets the content associated with the tab
    /// </summary>
    public object Content { get; }

    /// <summary>
    /// Gets the icon path/name for the tab
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Gets or sets whether the tabs are in collapsed state
    /// </summary>
    public bool IsTabsCollapsed { get; set; }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of TabItemViewModel
    /// </summary>
    /// <param name="tabName">The name to display for the tab</param>
    /// <param name="icon">The icon to display for the tab</param>
    /// <param name="content">The content to display in the tab</param>
    /// <param name="isTabsCollapsed">Whether the tab should start collapsed</param>
    public TabItemViewModel(
        string tabName,
        string icon,
        object content,
        bool isTabsCollapsed)
    {
        TabName = tabName ?? throw new ArgumentNullException(nameof(tabName));
        Icon = icon ?? throw new ArgumentNullException(nameof(icon));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        IsTabsCollapsed = isTabsCollapsed;
    }
    #endregion
}