using Cats.Telescope.VsExtension.Core.Enums;
using System;

namespace Cats.Telescope.VsExtension.Core.Settings;

/// <summary>
/// Contains the currently selected retention time of the content cache
/// </summary>
sealed class ContentCacheRetentionSetting : UISetting<ContentCacheRetention>
{
    public ContentCacheRetentionSetting()
    {

    }

    public ContentCacheRetentionSetting(ContentCacheRetention retention)
    {
        OriginalValue = retention;
    }
}
