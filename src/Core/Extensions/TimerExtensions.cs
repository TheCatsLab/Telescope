using System.Timers;
using System.Windows.Threading;

namespace Cats.Telescope.VsExtension.Core.Extensions;

internal static class TimerExtensions
{
    /// <summary>
    /// Resets the <paramref name="timer"/> counting
    /// </summary>
    /// <param name="timer"><see cref="Timer"/></param>
    public static void Reset(this Timer timer)
    {
        timer.Stop();
        timer.Start();
    }

    /// <summary>
    /// Resets the <paramref name="timer"/> counting
    /// </summary>
    /// <param name="timer"><see cref="Timer"/></param>
    public static void Reset(this DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }
}
