using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cats.Telescope.VsExtension.Core.Utils;

internal class InfoBarService : IVsInfoBarUIEvents
{
    private static IAsyncServiceProvider _serviceProvider;
    private uint _cookie;

    private static InfoBarService _instance;

    public static InfoBarService Instance => _instance ??= new InfoBarService();

    public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        infoBarUIElement.Unadvise(_cookie);
    }

    public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        string context = (string)actionItem.ActionContext;

        if (context == "yes")
        {
            MessageBox.Show("You clicked Yes!");
        }
        else
        {
            MessageBox.Show("You clicked No!");
        }
    }

    public static void Initialize(IAsyncServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ShowInfoBarAsync(InfoBarModel infoBarModel)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (await _serviceProvider.GetServiceAsync(typeof(SVsShell)) is IVsShell shell)
        {
            // Get the main window handle to host our InfoBar
            shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj);
            var host = (IVsInfoBarHost)obj;

            await ShowInfoBarAsync(host, infoBarModel);
        }
    }

    public async Task ShowInfoBarAsync(IVsInfoBarHost host, InfoBarModel infoBarModel)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        //If we cannot find the handle, we cannot do much, so return.
        if (host == null)
        {
            return;
        }

        //Get the factory object from IVsInfoBarUIFactory, create it and add it to host.
        if (await _serviceProvider.GetServiceAsync(typeof(SVsInfoBarUIFactory)).ConfigureAwait(false) is IVsInfoBarUIFactory factory)
        {
            IVsInfoBarUIElement element = factory.CreateInfoBar(infoBarModel);
            element.Advise(this, out _cookie);
            host.AddInfoBar(element);
        }
    }
}
