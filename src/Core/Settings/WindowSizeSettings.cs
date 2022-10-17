namespace Cats.Telescope.VsExtension.Core.Settings;

sealed internal class WindowSize
{
    public double Height { get; set; }
    public double Width { get; set; }
}

/// <summary>
/// Contains the main window height and width
/// </summary>
sealed internal class WindowSizeSettings : UISetting<WindowSize>
{
    public WindowSizeSettings()
    {

    }

    public WindowSizeSettings(WindowSize windowSize)
    {
        OriginalValue = windowSize;
    }
}
