namespace Cats.Telescope.VsExtension.Core.Settings;

sealed class ContentGridWidth
{
    /// <summary>
    /// Width of the tree view part
    /// </summary>
    public double TreeColumnWidth { get; set; }

    /// <summary>
    /// Width of the resource data part
    /// </summary>
    public double ResourceDataColumnWidth { get; set; }
}

/// <summary>
/// Contains definitions of the grid parts positions
/// </summary>
sealed class ContentGridWidthSetting : UISetting<ContentGridWidth>
{
    public ContentGridWidthSetting()
    {

    }

    public ContentGridWidthSetting(ContentGridWidth contentGridWidth)
    {
        OriginalValue = contentGridWidth;
    }
}
