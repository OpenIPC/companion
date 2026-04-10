using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Companion.Models;
using Companion.Services;
using Companion.ViewModels;
using Moq;
using Serilog;

namespace OpenIPC.Companion.Tests.ViewModels;

[TestFixture]
public class MainViewModelTests
{
    private Mock<ILogger> _loggerMock = null!;
    private string _preferencesFilePath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _preferencesFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_preferences.json");
        _loggerMock = new Mock<ILogger>();
        _loggerMock.Setup(x => x.ForContext<PreferencesService>()).Returns(_loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_preferencesFilePath))
            File.Delete(_preferencesFilePath);
    }

    [Test]
    public void SavePreferences_WhenTabChanges_PreservesPreferredFirmwareSourceFromDisk()
    {
        var preferencesService = new PreferencesService(_loggerMock.Object, _preferencesFilePath);
        preferencesService.Save(new UserPreferences
        {
            CheckForUpdatesOnStartup = true,
            PreferredFirmwareSource = "Greg APFPV",
            FirmwareFocusedMode = true,
            LastSelectedTab = "Preferences",
            IsTabsCollapsed = false
        });

#pragma warning disable SYSLIB0050
        var mainViewModel = (MainViewModel)FormatterServices.GetUninitializedObject(typeof(MainViewModel));
#pragma warning restore SYSLIB0050

        SetPrivateField(mainViewModel, "_preferencesService", preferencesService);
        SetPrivateField(mainViewModel, "_preferencesInitialized", true);
        SetPrivateField(mainViewModel, "_userPreferences", new UserPreferences
        {
            CheckForUpdatesOnStartup = true,
            PreferredFirmwareSource = "OpenIPC Builder",
            FirmwareFocusedMode = true,
            LastSelectedTab = "Preferences",
            IsTabsCollapsed = false
        });
        SetPrivateField(mainViewModel, "_isTabsCollapsed", true);
        SetPrivateField(mainViewModel, "_selectedTab",
            new TabItemViewModel("Firmware", "icon_dark.svg", new object(), false));

        InvokePrivateMethod(mainViewModel, "SavePreferences");

        var saved = preferencesService.Load();
        Assert.That(saved.PreferredFirmwareSource, Is.EqualTo("Greg APFPV"));
        Assert.That(saved.LastSelectedTab, Is.EqualTo("Firmware"));
        Assert.That(saved.IsTabsCollapsed, Is.True);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().FullName, fieldName);

        field.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().FullName, methodName);

        method.Invoke(target, null);
    }
}
