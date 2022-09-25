using System.Timers;

namespace Cats.Telescope.VsExtension.Core.Extensions;

internal static class TimerExtensions
{
    /// <summary>
    /// Resets the timer counting
    /// </summary>
    /// <param name="timer"><see cref="Timer"/></param>
    public static void Reset(this Timer timer)
    {
        timer.Stop();
        timer.Start();
    }
}
