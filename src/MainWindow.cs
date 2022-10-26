using Cats.Telescope.VsExtension.Views;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension;

/// <summary>
/// This class implements the tool window exposed by this package and hosts a user control.
/// </summary>
/// <remarks>
/// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
/// usually implemented by the package implementer.
/// <para>
/// This class derives from the ToolWindowPane class provided from the MPF in order to use its
/// implementation of the IVsUIElementPane interface.
/// </para>
/// </remarks>
[Guid("7c34bf20-8d05-4190-b4c6-28413bd4327f")]
public class MainWindow : ToolWindowPane
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow() : base(null)
    {
        this.Caption = "Telescope";

        // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
        // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
        // the object returned by the Content property.
        this.Content = new MainWindowControl(this);
    }

    public async Task<IVsInfoBarHost> GetInfoBarHostAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        IVsWindowFrame frame = (IVsWindowFrame)this.Frame;
        frame.GetProperty((int)__VSFPROPID7.VSFPROPID_InfoBarHost, out object value);

        if (value is IVsInfoBarHost host)
        {
            return host;
        }

        return null;
    }

    /// <summary>
    /// Sets <paramref name="width"/> and <paramref name="height"/> for the current window
    /// </summary>
    /// <param name="width">desired window width</param>
    /// <param name="height">desired window height</param>
    /// <returns></returns>
    public async Task SetThisWindowSizeAsync(int width, int height)
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IVsWindowFrame w = (IVsWindowFrame)this.Frame;

            Guid m = Guid.Empty;
            w.SetFramePos(VSSETFRAMEPOS.SFP_fSize, ref m, 0, 0, width, height);
        }
        catch(Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }
}
