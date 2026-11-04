using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core;
using Cats.Telescope.VsExtension.Core.Controls;
using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Core.Models;
using Cats.Telescope.VsExtension.Core.Services;
using Cats.Telescope.VsExtension.Core.Settings;
using Cats.Telescope.VsExtension.Core.Utils;
using Cats.Telescope.VsExtension.Mvvm.Commands;
using Community.VisualStudio.Toolkit;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Cats.Telescope.VsExtension.ViewModels;

internal class MainWindowViewModel : ViewModelBase, IDisposable
{
    #region Constants

    private const string DefaultBusyText = "Loading...";
    private const string AzurePortalDomain = "https://portal.azure.com/#";

    #endregion

    #region Fields

    private bool _isBusy;
    private string _busyText;
    private string _searchText;
    private ObservableCollection<ResourceNode> _resourceNodes;
    private ResourceNode _selectedNode;
    private bool _isFiltering;
    private string _appliedSearchQueryText;
    private FilterBy _appliedFilterOption;
    private StringComparison _appliedStringComparison;
    private string _copyPopupText;
    private bool _isCopying;
    private bool _isCaseSensitive;
    private bool _isFilterOptionsOpened;
    private FilterBy _selectedFilterOptions;
    private string _treeStatusText;
    private bool _isEmptyState;
    private IEnumerable<ScopeSuggestion> _scopeSuggestions;
    private bool _showEnvironmentVariableValues;
    private FilterOptions _pendingFilter;
    private bool _appliedIsRegex;

    // contains ids of all the loading activities, if empty - it means there are no active loadings;
    // guarded by _loadingsSync since loadings start and complete on different threads
    private readonly object _loadingsSync = new();
    private readonly HashSet<Guid> _activeLoadings = new();

    private IHighlightingDefinition _highlightingDefinition;
    private readonly TelescopeService _telescopeService;

    private readonly List<ResourceNode> _fakeResources;
    private readonly bool _isTestMode;

    private bool _isDisposed;

    #endregion

    #region Contructors

    public MainWindowViewModel()
    {
        _telescopeService = new();
        ResourceNodes = new();
        SelectedFilterOptions = FilterBy.ResourceName;

        FilterCommand = new RelayCommand((parameter) => true, InvokeFilter);
        CopyToClipboardCommand = new RelayCommand(CanCopy, OnCopyToClipboard);
        CopyNodeNameCommand = new RelayCommand(CanCopyNode, OnCopyNodeName);
        CopyNodeArmIdCommand = new RelayCommand(CanCopyNodeArmId, OnCopyNodeArmId);
        OpenResourceCommand = new RelayCommand(CanOpenResource, OnOpenResource);
        RefreshCommand = new RelayCommand(CanRefresh, OnRefresh);
        CancelLoadingCommand = new RelayCommand(CanCancelLoading, OnCancelLoading);
        ExpandAllCommand = new RelayCommand((p) => true, OnExpandAll);
        CollapseAllCommand = new RelayCommand((p) => true, OnCollapseAll);
        ToggleFilterOptionsVisibilityCommand = new RelayCommand((p) => true, OnToggleFilterOptionsVisibility);
        ResetFilterScopesCommand = new RelayCommand((p) => true, OnResetFilterScopes);
        EnableScopeCommand = new RelayCommand((p) => true, OnEnableScope);

        _telescopeService.LoadingStarted += TelescopeService_LoadingStarted;
        _telescopeService.LoadingCompleted += TelescopeService_LoadingCompleted;
                
        _isTestMode = false;
        _fakeResources = new();
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when any text has been copied to Clipboard 
    /// </summary>
    internal event EventHandler<string> CopiedToClipboard;

    /// <summary>
    /// Occurs when any filter option has been changed
    /// </summary>
    internal event EventHandler<ActiveFilterOptions> FilterSettingsChanged;

    #endregion

    #region Commands

    public RelayCommand FilterCommand { get; }
    public RelayCommand CopyToClipboardCommand { get; }
    public RelayCommand CopyNodeNameCommand { get; }
    public RelayCommand CopyNodeArmIdCommand { get; }
    public RelayCommand OpenResourceCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand CancelLoadingCommand { get; }
    public RelayCommand ExpandAllCommand { get; }
    public RelayCommand CollapseAllCommand { get; }
    public RelayCommand ToggleFilterOptionsVisibilityCommand { get; }
    public RelayCommand ResetFilterScopesCommand { get; }
    public RelayCommand EnableScopeCommand { get; }

    #endregion

    #region Properties

    public IHighlightingDefinition HighlightingDefinition
    {
        get => _highlightingDefinition;

        set
        {
            if (_highlightingDefinition != value)
            {
                _highlightingDefinition = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Contains all selected filter options
    /// </summary>
    public FilterBy SelectedFilterOptions
    {
        get => _selectedFilterOptions;
        set
        {
            _selectedFilterOptions = value;
            RaisePropertyChanged();
            RaiseFilterSettingsChanged();

            // the scope chips and the options popup checkboxes apply live:
            // the current search is re-run as soon as the scopes change
            ReRunCurrentFilter();
        }
    }

    /// <summary>
    /// Text summarizing the tree state: total resources and, when filtering, the number of matches
    /// </summary>
    public string TreeStatusText
    {
        get => _treeStatusText;
        set
        {
            _treeStatusText = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Indicates if a filter has been applied and nothing matches it
    /// </summary>
    public bool IsEmptyState
    {
        get => _isEmptyState;
        set
        {
            _isEmptyState = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Filter scopes the user can enable right from the empty state to widen the search
    /// </summary>
    public IEnumerable<ScopeSuggestion> ScopeSuggestions
    {
        get => _scopeSuggestions;
        set
        {
            _scopeSuggestions = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Indicates if the filter options popup is opened
    /// </summary>
    public bool IsFilterOptionsOpened
    {
        get => _isFilterOptionsOpened;
        set
        {
            _isFilterOptionsOpened = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Indicates if the active filter is case sensitive
    /// </summary>
    public bool IsCaseSensitive
    {
        get => _isCaseSensitive;
        set
        {
            _isCaseSensitive = value;
            RaisePropertyChanged();
            RaiseFilterSettingsChanged();
        }
    }

    /// <summary>
    /// Text displayed in the "Copied" popup
    /// </summary>
    public string CopyPopupText
    {
        get => _copyPopupText;
        set
        {
            _copyPopupText = value;
            RaisePropertyChanged();
        }
    }

    private bool _useRegex;

    /// <summary>
    /// Indicates if the query text is treated as a regular expression -
    /// mirrors the search control toggle, used by the match marks of the configuration editor
    /// </summary>
    public bool UseRegex
    {
        get => _useRegex;
        set
        {
            _useRegex = value;
            RaisePropertyChanged();
        }
    }

    public MainWindow ToolWindowPane { get; set; }

    /// <summary>
    /// Text entered in UI to filter the nodes
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Currently selected tree node
    /// </summary>
    public ResourceNode SelectedNode
    {
        get => _selectedNode;
        set
        {
            if(value is null || value.IsVisible)
            {
                _selectedNode = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates if there is any operation is being performed. Used for displaying loaders
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShowGlobalLoader));
        }
    }

    /// <summary>
    /// Indicates if the global loading overlay should be displayed:
    /// only while the tree is still empty(usually the very first load),
    /// so the tree stays interactive while the nodes are being loaded
    /// </summary>
    public bool ShowGlobalLoader => IsBusy && ResourceNodes?.Count == 0;

    /// <summary>
    /// Indicates if the environment variable values should be displayed as is -
    /// they are masked by default since they often contain secrets
    /// </summary>
    public bool ShowEnvironmentVariableValues
    {
        get => _showEnvironmentVariableValues;
        set
        {
            _showEnvironmentVariableValues = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Text that should be displayed when <see cref="IsBusy"/> set to true
    /// </summary>
    public string BusyText
    {
        get => _busyText ?? DefaultBusyText;
        set
        {
            _busyText = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Azure Resources nodes collection
    /// </summary>
    public ObservableCollection<ResourceNode> ResourceNodes
    {
        get => _resourceNodes;
        set
        {
            _resourceNodes = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ShowGlobalLoader));
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Invokes filtering of nodes considering filter <paramref name="parameter"/> as <see cref="FilterOptions"/>
    /// </summary>
    /// <param name="parameter"></param>
    public void InvokeFilter(object parameter)
    {
        if (parameter is not FilterOptions filterOptions)
            return;

        // fire and forget
        OnFilterAsync(filterOptions).Forget();
    }

    /// <summary>
    /// Handles filtering of tree nodes: the requests arriving during a running pass are queued, the latest one wins
    /// </summary>
    /// <param name="filterOptions">filter to apply</param>
    /// <returns></returns>
    public async Task OnFilterAsync(FilterOptions filterOptions)
    {
        if (_isFiltering)
        {
            // only the latest request matters - it runs right after the current pass
            _pendingFilter = filterOptions;
            return;
        }

        _isFiltering = true;

        try
        {
            await OnFilterAsyncCore(filterOptions, forceReapply: false);
        }
        finally
        {
            _isFiltering = false;

            FilterOptions pending = _pendingFilter;
            if (pending != null)
            {
                _pendingFilter = null;
                await OnFilterAsync(pending);
            }
        }
    }

    /// <summary>
    /// Applies the filter unless the very same result has just been calculated
    /// </summary>
    private async Task OnFilterAsyncCore(FilterOptions filterOptions, bool forceReapply)
    {
        // avoid doing filtering to get the same result
        if (!forceReapply &&
            string.Equals(filterOptions.QueryText, _appliedSearchQueryText) &&
            _appliedFilterOption == SelectedFilterOptions &&
            _appliedStringComparison == filterOptions.StringComparison &&
            _appliedIsRegex == filterOptions.UseRegex)
            return;

        bool hasQuery = !string.IsNullOrEmpty(filterOptions.QueryText) && ((int)SelectedFilterOptions) > 0;

        // snapshot the tree for the background pass
        List<ResourceNode> roots = ResourceNodes.ToList();

        Dictionary<ResourceNode, NodeFilterResult> matches = new();

        if (hasQuery)
        {
            NodeFilter filter = new()
            {
                SearchText = filterOptions.QueryText,
                FilterByOptions = SelectedFilterOptions,
                StringComparison = filterOptions.StringComparison,
                IsRegex = filterOptions.UseRegex
            };

            // the matching is the expensive part - it runs off the UI thread;
            // the nodes are only read here, nothing is mutated
            await Task.Run(() =>
            {
                foreach (ResourceNode root in roots)
                {
                    foreach (ResourceNode node in root.Descendants())
                    {
                        matches[node] = node.MatchesCount(filter);
                    }
                }
            });
        }

        // apply the results on the UI thread
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        foreach (ResourceNode root in roots)
        {
            ApplyFilterResults(root, hasQuery, matches);
        }

        _isCaseSensitive = !filterOptions.StringComparison.IsIgnoreCaseComparison();

        if (SelectedNode != null && !SelectedNode.IsVisible)
            SelectedNode = null;

        // remember the query text to avoid doing useless filtering
        _appliedSearchQueryText = filterOptions.QueryText;
        _appliedFilterOption = SelectedFilterOptions;
        _appliedStringComparison = filterOptions.StringComparison;
        _appliedIsRegex = filterOptions.UseRegex;

        UpdateFilterFeedback();
    }

    /// <summary>
    /// Applies the precalculated filter results to the subtree rooted by <paramref name="root"/>
    /// </summary>
    /// <returns>true if the subtree has any visible node</returns>
    private bool ApplyFilterResults(ResourceNode root, bool hasQuery, Dictionary<ResourceNode, NodeFilterResult> matches)
    {
        if (root is null)
            return false;

        // leaf node - the precalculated match result decides;
        // leaves with children(apps with slots) stay visible when any of their children matches
        if (!root.IsContainerNode)
        {
            // nodes loaded after the snapshot are not in the dictionary - they are treated as non-matching
            if (hasQuery && matches.TryGetValue(root, out NodeFilterResult result))
                root.FilterMatches.Apply(result);
            else
                root.FilterMatches.Apply(null);

            bool childrenVisible = false;

            if (root.ResourceNodes?.Any() == true)
            {
                foreach (ResourceNode child in root.ResourceNodes)
                {
                    if (ApplyFilterResults(child, hasQuery, matches) && !childrenVisible)
                        childrenVisible = true;
                }
            }

            root.IsVisible = !hasQuery || root.FilterMatches.Matches || childrenVisible;

            // reveal the branch leading to a matched child
            if (hasQuery && childrenVisible)
                root.IsExpanded = true;

            return root.IsVisible;
        }

        // container - it follows its children
        bool hasVisibleItems = false;

        if (root.ResourceNodes?.Any() == true)
        {
            foreach (ResourceNode node in root.ResourceNodes)
            {
                if (ApplyFilterResults(node, hasQuery, matches) && !hasVisibleItems)
                    hasVisibleItems = true;
            }
        }

        root.FilterMatches.Apply(null);

        if (hasQuery)
        {
            // expand and show the node if it contains matching nodes
            root.IsExpanded = root.IsVisible = hasVisibleItems;
        }
        else
        {
            root.IsExpanded = root.IsVisible = root.ResourceNodes?.Any() == true;
        }

        return hasVisibleItems;
    }

    /// <summary>
    /// Updates the loading text displayed by the in-window loading form
    /// </summary>
    /// <param name="waitMessage"></param>
    /// <param name="progressText"></param>
    /// <param name="statusBarText"></param>
    /// <param name="currentStep"></param>
    /// <param name="totalStep"></param>
    /// <returns></returns>
    private Task UpdateLoaderAsync(string waitMessage, string progressText, string statusBarText, int currentStep, int totalStep)
    {
        BusyText = progressText;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Raises property changed event for <see cref="SelectedNode"/> property to update it on UI
    /// </summary>
    public void RefreshSelected()
    {
        RaisePropertyChanged(nameof(SelectedNode));
    }

    /// <summary>
    /// Cancels every loading operation of the current session; used when the tool
    /// window is unloaded or closed while a load is still running
    /// </summary>
    public void CancelLoading()
    {
        if (_isDisposed)
            return;

        _telescopeService.CancelLoading();
    }

    /// <summary>
    /// Detaches the view model from the service and releases the loading session
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _telescopeService.LoadingStarted -= TelescopeService_LoadingStarted;
        _telescopeService.LoadingCompleted -= TelescopeService_LoadingCompleted;
        _telescopeService.Dispose();

        lock (_loadingsSync)
        {
            _activeLoadings.Clear();
        }

        ToolWindowPane = null;
        SelectedNode = null;
        ResourceNodes?.Clear();
    }

    /// <summary>
    /// Handles initial loading of the view data
    /// </summary>
    /// <param name="parameter">any <see cref="object"/> parameter</param>
    /// <returns></returns>
    public async Task OnLoadedAsync(object parameter)
    {
        if (_isDisposed)
            return;

        try
        {
            if (ResourceNodes.Any())
                ResourceNodes.Clear();

            RaisePropertyChanged(nameof(ShowGlobalLoader));

            if (_isTestMode)
            {
                foreach (var node in _fakeResources)
                {
                    ResourceNodes.Add(node);
                    node.OnExpandAsync(null).Forget();
                }
            }
            else
            {
                await LoadDataAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }

    #endregion

    #region Private Methods

    #region Data

    /// <summary>
    /// Loads all the data: tenants, subscriptions, groups etc.
    /// </summary>
    /// <returns></returns>
    private async Task LoadDataAsync()
    {
        if (_isDisposed)
            return;

        // loader will be visible until the id is in the list
        Guid loadingId = Guid.NewGuid();

        lock (_loadingsSync)
        {
            _activeLoadings.Add(loadingId);
        }

        try
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await UpdateLoaderAsync("Please wait...", "Loading tenants and subscriptions...", "Loading subscriptions...", 1, 3);

            var tenantsLoad = _telescopeService.GetTenantsAsync().ConfigureAwait(false);
            var subscriptionsLoad = _telescopeService.GetSubscriptionsAsync().ConfigureAwait(false);

            var tenants = await tenantsLoad;
            var subscriptions = await subscriptionsLoad;

            await UpdateLoaderAsync("Please wait...", "Loading resource groups...", "Loading resource groups...", 2, 3);

            if (subscriptions != null && subscriptions.Any())
            {
                foreach (var subscription in subscriptions)
                {
                    string domain = tenants.FirstOrDefault(t => t.Data.TenantId == subscription.Data.TenantId)?.Data.DefaultDomain;

                    ResourceNode node = new(subscription.Data.DisplayName, ResourceNodeType.Subscription, () => ExpandSubscriptionAsync(subscription))
                    {
                        LinkToResource = $"{AzurePortalDomain}@{domain}/resource/subscriptions/{subscription.Id.SubscriptionId}",
                        ArmId = subscription.Id.ToString()
                    };

                    await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        ResourceNodes.Add(node);

                        // the tree is not empty anymore - the global overlay gives way to inline indicators
                        RaisePropertyChanged(nameof(ShowGlobalLoader));
                    });

                    node.OnExpandAsync(null).Forget();
                }
            }
            else
            {
                await ShowWarningInfoBarAsync("No subscriptions found");
            }

            await UpdateLoaderAsync("Please wait...", "Loading logic apps, azure functions and web services...", "Loading logic apps, azure functions and web services...", 3, 3);
        }
        catch (OperationCanceledException)
        {
            // the loading has been cancelled by the user
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
            await ShowWarningInfoBarAsync("Cannot load subscription data. Please check your credentials."); // see this answer https://stackoverflow.com/a/64341779/7084252
        }
        finally
        {
            if (!_isDisposed)
                TelescopeService_LoadingCompleted(this, loadingId);
        }
    }

    /// <summary>
    /// Callback that's performed when a subscription node is being expanded
    /// </summary>
    /// <param name="subscription">expanding subscription node</param>
    /// <returns></returns>
    private async Task<IEnumerable<ResourceNode>> ExpandSubscriptionAsync(SubscriptionResource subscription)
    {
        IEnumerable<ResourceGroupResource> groups = await _telescopeService.GetResourceGroupsAsync(subscription);

        return groups.Select(g => new ResourceNode(g.Data.Name, ResourceNodeType.ResourceGroup, () => ExpandGroupsAsync(g), true)
        {
            ArmId = g.Data.Id?.ToString()
        });
    }

    /// <summary>
    /// Callback that's performed when a group node is being expanded
    /// </summary>
    /// <param name="subscription">expanding group node</param>
    /// <returns></returns>
    private async Task<IEnumerable<ResourceNode>> ExpandGroupsAsync(ResourceGroupResource resourceGroup)
    {
        Task<IEnumerable<AzureLogicAppInfo>> logicAppsLoad = _telescopeService.GetLogicAppsAsync(resourceGroup);
        Task<IEnumerable<WebAppInfo>> functionsLoad = _telescopeService.GetWebAppsAsync(resourceGroup);

        List<ResourceNode> resources = new(20);

        IEnumerable<AzureLogicAppInfo> logicApps = await logicAppsLoad;
        IEnumerable<WebAppInfo> functions = await functionsLoad;

        if (logicApps.Any())
            resources.AddRange(logicApps);

        if (functions.Any())
            resources.AddRange(functions);

        return resources;
    }

    #endregion

    #region Clipboard

    /// <summary>
    /// Returns true if copying to clipboard is available
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    private bool CanCopy(object param)
    {
        return !_isCopying;
    }

    /// <summary>
    /// Returns true if the name of the passed tree node can be copied
    /// </summary>
    /// <param name="param">a <see cref="ResourceNode"/> to copy the name of</param>
    /// <returns></returns>
    private bool CanCopyNode(object param)
    {
        return !_isCopying && param is ResourceNode;
    }

    /// <summary>
    /// Returns true if the ARM identifier of the passed tree node can be copied
    /// </summary>
    /// <param name="param">a <see cref="ResourceNode"/> to copy the identifier of</param>
    /// <returns></returns>
    private bool CanCopyNodeArmId(object param)
    {
        return !_isCopying && param is ResourceNode node && !string.IsNullOrEmpty(node.ArmId);
    }

    /// <summary>
    /// Copies a <paramref name="text"/> to the clipboard and shows the confirmation popup
    /// </summary>
    /// <param name="text">text to copy</param>
    /// <param name="target">copying target used to place the popup</param>
    /// <param name="popupText">text of the confirmation popup</param>
    private void CopyItemToClipboard(string text, string target, string popupText)
    {
        _isCopying = true;

        try
        {
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);

            CopyPopupText = popupText;
            CopiedToClipboard?.Invoke(this, target);
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
        finally
        {
            _isCopying = false;
        }
    }

    /// <summary>
    /// Copies the name of the <paramref name="param"/> node to the clipboard
    /// </summary>
    /// <param name="param">a <see cref="ResourceNode"/> to copy the name of</param>
    private void OnCopyNodeName(object param)
    {
        if (param is ResourceNode node)
            CopyItemToClipboard(node.Id, "name", TelescopeConstants.Clipboard.ResourceNameCopiedText);
    }

    /// <summary>
    /// Copies the ARM identifier of the <paramref name="param"/> node to the clipboard
    /// </summary>
    /// <param name="param">a <see cref="ResourceNode"/> to copy the identifier of</param>
    private void OnCopyNodeArmId(object param)
    {
        if (param is ResourceNode node)
            CopyItemToClipboard(node.ArmId, "armid", TelescopeConstants.Clipboard.ResourceArmIdCopiedText);
    }

    /// <summary>
    /// Copies names(IDs) of all <see cref="ResourceNodes"/> having <see cref="ResourceNode.IsSelected"/> as true to clipboard
    /// </summary>
    /// <param name="obj"></param>
    private void OnCopyToClipboard(object valueToCopy)
    {
        _isCopying = true;

        string target = null;

        try
        {
            StringBuilder sb = new();

            // copy the tree nodes by default
            if (valueToCopy is null)
            {
                target = "tree";

                foreach (ResourceNode subscription in ResourceNodes)
                {
                    foreach (ResourceNode node in subscription.Descendants())
                    {
                        if (node.IsSelected)
                            sb.AppendLine($"[{node.Type}] {node.Id}");
                    }
                }

                CopyPopupText = sb.Length > 0 ? TelescopeConstants.Clipboard.NodesCopiedText : TelescopeConstants.Clipboard.NoNodesToCopyText;
            }
            else
            {
                switch (valueToCopy)
                {
                    case "name":
                        sb.AppendLine(SelectedNode?.Id);
                        target = "name";
                        CopyPopupText = TelescopeConstants.Clipboard.ResourceNameCopiedText;
                        break;
                    case "data":
                        sb.AppendLine(SelectedNode?.Data);
                        target = "data";
                        CopyPopupText = TelescopeConstants.Clipboard.ResourceDataCopiedText;
                        break;
                    default:
                        sb.AppendLine(valueToCopy?.ToString() ?? string.Empty);
                        CopyPopupText = TelescopeConstants.Clipboard.CopiedToClipboardDefaultText;
                        break;
                }
            }

            string textToCopy = sb.ToString();

            if (!string.IsNullOrEmpty(textToCopy))
                Clipboard.SetText(sb.ToString());

            CopiedToClipboard?.Invoke(this, target);
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
        finally
        {
            _isCopying = false;
        }
    }

    #endregion

    #region Filter

    /// <summary>
    /// Raises <see cref="FilterSettingsChanged"/> event with curently selected filter options
    /// </summary>
    private void RaiseFilterSettingsChanged()
    {
        FilterSettingsChanged?.Invoke(this, new ActiveFilterOptions() { FilterByOptions = SelectedFilterOptions, IsCaseSensitive = IsCaseSensitive });
    }

    /// <summary>
    /// Resets the filter scopes to the default name-only search
    /// </summary>
    private void OnResetFilterScopes(object param)
    {
        SelectedFilterOptions = FilterBy.ResourceName;
    }

    /// <summary>
    /// Re-runs the currently applied search - used when the filter scopes change outside of the search text editing
    /// </summary>
    private void ReRunCurrentFilter()
    {
        if (string.IsNullOrEmpty(_appliedSearchQueryText))
            return;

        InvokeFilter(new FilterOptions { QueryText = _appliedSearchQueryText, StringComparison = _appliedStringComparison, UseRegex = _appliedIsRegex });
    }

    /// <summary>
    /// Applies <paramref name="isVisibleValue"/> to the filter options popup visibility
    /// </summary>
    /// <param name="isVisibleValue"></param>
    private void OnToggleFilterOptionsVisibility(object isVisibleValue)
    {
        if (isVisibleValue is bool isVisible)
        {
            IsFilterOptionsOpened = isVisible;
        }
    }

    /// <summary>
    /// Enables an additional filter <paramref name="scope"/> and re-runs the last applied search -
    /// used by the empty state to widen the search with one click
    /// </summary>
    /// <param name="scope">a <see cref="FilterBy"/> scope to enable</param>
    private void OnEnableScope(object scope)
    {
        if (scope is FilterBy flags)
        {
            SelectedFilterOptions |= flags;

            InvokeFilter(new FilterOptions { QueryText = _appliedSearchQueryText, StringComparison = _appliedStringComparison, UseRegex = _appliedIsRegex });
        }
    }

    /// <summary>
    /// Re-applies the current filter to the nodes that have been loaded after the filter was applied,
    /// so the search results stay complete while the tree is still loading
    /// </summary>
    private void ReApplyFilterToLoadedNodes()
    {
        if (string.IsNullOrEmpty(_appliedSearchQueryText))
            return;

        // dedup is skipped: the same query must cover the freshly loaded nodes as well
        OnFilterAsyncCore(new FilterOptions { QueryText = _appliedSearchQueryText, StringComparison = _appliedStringComparison, UseRegex = _appliedIsRegex }, forceReapply: true).Forget();
    }

    /// <summary>
    /// Recalculates the tree status line and the empty state after a filter pass or a data load
    /// </summary>
    private void UpdateFilterFeedback()
    {
        bool isFilterActive = !string.IsNullOrEmpty(_appliedSearchQueryText);

        int totalApps = 0;
        int matchedApps = 0;

        foreach (ResourceNode subscription in ResourceNodes)
        {
            foreach (ResourceNode node in subscription.Descendants())
            {
                if (node.IsContainerNode || node.Type == ResourceNodeType.Empty || node.Type == ResourceNodeType.GenericResource)
                    continue;

                totalApps++;

                if (node.IsVisible)
                    matchedApps++;
            }
        }

        TreeStatusText = isFilterActive && totalApps > 0
            ? $"{matchedApps} of {totalApps} apps match"
            : $"{totalApps} apps in {ResourceNodes.Count} subscription(s)";

        bool isEmptyState = isFilterActive && totalApps > 0 && matchedApps == 0;
        IsEmptyState = isEmptyState;

        if (isEmptyState)
            ScopeSuggestions = BuildScopeSuggestions();
    }

    /// <summary>
    /// Builds the list of filter scopes that are currently disabled and could widen the empty search
    /// </summary>
    private IEnumerable<ScopeSuggestion> BuildScopeSuggestions()
    {
        List<ScopeSuggestion> suggestions = new();

        if (!SelectedFilterOptions.HasFlag(FilterBy.ResourceData))
            suggestions.Add(new ScopeSuggestion("Configuration data", FilterBy.ResourceData));

        if (!SelectedFilterOptions.HasFlag(FilterBy.ResourceTagKeys))
            suggestions.Add(new ScopeSuggestion("Tag keys", FilterBy.ResourceTagKeys));

        if (!SelectedFilterOptions.HasFlag(FilterBy.ResourceTagValues))
            suggestions.Add(new ScopeSuggestion("Tag values", FilterBy.ResourceTagValues));

        if (!SelectedFilterOptions.HasFlag(FilterBy.ResourceEnvironmentVariableKeys))
            suggestions.Add(new ScopeSuggestion("Environment variable keys", FilterBy.ResourceEnvironmentVariableKeys));

        if (!SelectedFilterOptions.HasFlag(FilterBy.ResourceEnvironmentVariableValues))
            suggestions.Add(new ScopeSuggestion("Environment variable values", FilterBy.ResourceEnvironmentVariableValues));

        if (!SelectedFilterOptions.HasFlag(FilterBy.ResourceName))
            suggestions.Add(new ScopeSuggestion("Resource name", FilterBy.ResourceName));

        return suggestions;
    }

    #endregion

    #region Event Handlers

    private void TelescopeService_LoadingCompleted(object sender, Guid actionId)
    {
        if (_isDisposed)
            return;

        bool removed;
        bool becameIdle;

        lock (_loadingsSync)
        {
            removed = _activeLoadings.Remove(actionId);
            becameIdle = removed && _activeLoadings.Count == 0;
        }

        // duplicate completions and completions of unknown ids must not reset the UI twice
        if (!becameIdle)
            return;

        Free();
        ReApplyFilterToLoadedNodes();
        UpdateFilterFeedback();
    }

    private void TelescopeService_LoadingStarted(object sender, Guid actionId)
    {
        if (_isDisposed)
            return;

        lock (_loadingsSync)
        {
            _activeLoadings.Add(actionId);
        }

        if (!IsBusy)
            SetBusy();
    }

    #endregion

    private bool CanOpenResource(object obj)
    {
        // the command supports a node parameter(the tree context menu) and falls back to the selected node
        return !string.IsNullOrEmpty((obj as ResourceNode ?? SelectedNode)?.LinkToResource);
    }

    /// <summary>
    /// Returns true if the tree can be reloaded
    /// </summary>
    private bool CanRefresh(object param)
    {
        return !IsBusy;
    }

    /// <summary>
    /// Cancels the current loading and reloads the whole tree
    /// </summary>
    private void OnRefresh(object param)
    {
        if (_isDisposed)
            return;

        CancelLoading();

        SelectedNode = null;
        OnLoadedAsync(null).Forget();
    }

    /// <summary>
    /// Returns true if there is a loading operation to cancel
    /// </summary>
    private bool CanCancelLoading(object param)
    {
        return IsBusy;
    }

    /// <summary>
    /// Cancels all the loading operations that are currently in progress
    /// </summary>
    private void OnCancelLoading(object param)
    {
        CancelLoading();
    }

    /// <summary>
    /// Expands every loaded container node of the tree
    /// </summary>
    private void OnExpandAll(object param)
    {
        foreach (ResourceNode subscription in ResourceNodes)
        {
            foreach (ResourceNode node in subscription.Descendants(n => n.IsContainerNode))
            {
                node.IsExpanded = true;
            }
        }
    }

    /// <summary>
    /// Collapses every container node of the tree
    /// </summary>
    private void OnCollapseAll(object param)
    {
        foreach (ResourceNode subscription in ResourceNodes)
        {
            foreach (ResourceNode node in subscription.Descendants())
            {
                if (node.IsContainerNode)
                    node.IsExpanded = false;
            }
        }
    }

    private void OnOpenResource(object obj)
    {
        ResourceNode node = obj as ResourceNode ?? SelectedNode;

        try
        {
            // disposing only releases the local process handle, it does not kill the browser
            using var process = Process.Start(new ProcessStartInfo(node.LinkToResource));
        }
        catch(Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }

    /// <summary>
    /// Prepares the view for doing any operation - shows the loader and loading text
    /// </summary>
    private void SetBusy()
    {
        IsBusy = true;
        BusyText = DefaultBusyText;
    }

    /// <summary>
    /// Hides the loader and loading text - should be called right after the operation is completed
    /// </summary>
    private void Free()
    {
        IsBusy = false;
        BusyText = null;
    }

    #region InfoBars

    /// <summary>
    /// Returns <see cref="InfoBarModel"/> that contains warning <paramref name="message"/> to display
    /// </summary>
    /// <param name="message">warning message</param>
    /// <returns></returns>
    private InfoBarModel CreateWarningInfoBar(string message)
    {
        InfoBarModel infoBar = new(
            textSpans: new[]
            {
                new InfoBarTextSpan(message)
            },
            image: KnownMonikers.ApplicationWarning,
            // the close button lets the user dismiss the bar, which raises OnClosed
            // and tears down the advise connection - without it failed loads pile up
            // indistinguishable bars in the tool window
            isCloseButtonVisible: true);

        return infoBar;
    }

    /// <summary>
    /// Displays a warning <paramref name="message"/> based on <see cref="InfoBarModel"/>
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task ShowWarningInfoBarAsync(string message)
    {
        if (_isDisposed || ToolWindowPane is null)
            return;

        IVsInfoBarHost host = await ToolWindowPane.GetInfoBarHostAsync();

        await InfoBarService.Instance.ShowInfoBarAsync(host, CreateWarningInfoBar(message));
    }

    #endregion

    #endregion
}

/// <summary>
/// Presents a filter scope suggestion displayed in the empty search state
/// </summary>
internal sealed class ScopeSuggestion
{
    public ScopeSuggestion(string label, FilterBy flags)
    {
        Label = label;
        Flags = flags;
    }

    /// <summary>
    /// User-friendly scope name
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The filter flag(s) to enable when the suggestion is clicked
    /// </summary>
    public FilterBy Flags { get; }
}
