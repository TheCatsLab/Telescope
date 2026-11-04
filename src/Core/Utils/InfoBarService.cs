using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cats.Telescope.VsExtension.Core.Utils;

internal class InfoBarService : IVsInfoBarUIEvents
{
    private sealed class CookieBox
    {
        public uint Cookie;
    }

    private static IAsyncServiceProvider _serviceProvider;

    // the cookie is keyed weakly - a bar removed by its host without OnClosed must not
    // keep the element(and the native COM object behind it) rooted by this singleton
    private readonly ConditionalWeakTable<IVsInfoBarUIElement, CookieBox> _adviseCookies = new();

    // only the newest bar is kept alive: the previous one is closed before a new one is shown,
    // so repeated failed loads do not stack identical warnings
    private IVsInfoBarUIElement _currentBar;

    private static InfoBarService _instance;

    public static InfoBarService Instance => _instance ??= new InfoBarService();

    public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        RemoveBar(infoBarUIElement);
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
            IVsInfoBarUIElement previous = _currentBar;

            IVsInfoBarUIElement element = factory.CreateInfoBar(infoBarModel);
            element.Advise(this, out uint cookie);
            _adviseCookies.Add(element, new CookieBox { Cookie = cookie });
            _currentBar = element;
            host.AddInfoBar(element);

            // tear the replaced bar down after the new one is in place to avoid a host without any bar
            if (previous is not null)
                RemoveBar(previous);
        }
    }

    /// <summary>
    /// Unadvises the element connection, detaches it from the host and drops the cookie note;
    /// safe to call for an element that has already been torn down
    /// </summary>
    private void RemoveBar(IVsInfoBarUIElement element)
    {
        if (_adviseCookies.TryGetValue(element, out CookieBox box))
        {
            _adviseCookies.Remove(element);
            element.Unadvise(box.Cookie);
        }

        if (ReferenceEquals(_currentBar, element))
            _currentBar = null;

        // detach the element from its host - closing only hides it visually
        element.Close();
    }
}
