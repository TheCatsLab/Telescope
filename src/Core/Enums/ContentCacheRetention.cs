namespace Cats.Telescope.VsExtension.Core.Enums;

/// <summary>
/// Defines how long the fetched environment variables are kept in the content cache;
/// the numeric value of every member is the retention time in hours
/// </summary>
internal enum ContentCacheRetention
{
    DoNotStore = 0,
    OneHour = 1,
    ThreeHours = 3,
    SixHours = 6,
    TwelveHours = 12,
    TwentyFourHours = 24
}
