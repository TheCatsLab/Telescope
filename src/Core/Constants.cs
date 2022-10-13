﻿namespace Cats.Telescope.VsExtension.Core;

/// <summary>
/// The extension constants
/// </summary>
public static class TelescopeConstants
{
    /// <summary>
    /// Contains constants that are used for the tree nodes filtering
    /// </summary>
    public static class Filter
    {
        /// <summary>
        /// Presents a delay to wait before applying the entered value
        /// </summary>
        public const int Delay = 800;
    }

    public static class Clipboard
    {

        /// <summary>
        /// Milliseconds delay to keep any popup visible for
        /// </summary>
        public const int PopupDefaultDisplayTime = 2000;
        public const string CopiedToClipboardDefaultText = "Copied to clipboard!";
        public const string NodesCopiedText = "The list of selected nodes has been copied!";
        public const string ResourceNameCopiedText = "The resource name has been copied!";
        public const string ResourceDataCopiedText = "The resource data has been copied!";
        public const string NoNodesToCopyText = "No nodes selected to copy to clipboard";
    }
}
