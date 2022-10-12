using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Controls;
using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Core.Models;
using Cats.Telescope.VsExtension.Core.Services;
using Cats.Telescope.VsExtension.Core.Utils;
using Cats.Telescope.VsExtension.Mvvm.Commands;
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

internal class MainWindowViewModel : ViewModelBase
{
    #region Constants

    private const string DefaultBusyText = "Loading...";
    private const string NodesCopiedText = "The list of selected nodes has been copied!";
    private const string NoNodesToCopyText = "No nodes selected to copy to clipboard";

    /// <summary>
    /// Milliseconds delay to keep any popup visible for
    /// </summary>
    private const int PopupDefaultDisplayTime = 2000;

    #endregion

    #region Fields

    private bool _isBusy;
    private string _busyText;
    private string _searchText;
    private ObservableCollection<ResourceNode> _resourceNodes;
    private ResourceNode _selectedNode;
    private FilterTargetOption _selectedFilterOption;
    private bool _isFiltering;
    private string _appliedSearchQueryText;
    private FilterBy _appliedFilterOption;
    private StringComparison _appliedStringComparison;
    private bool _nodesJustCopiedPopupOpened;
    private string _copyPopupText;
    private bool _isCopying;
    private bool _isCaseSensitive;

    private readonly TelescopeService _telescopeService;

    private readonly List<ResourceNode> _fakeResources;
    private readonly bool _isTestMode;

    #endregion

    #region Contructors

    public MainWindowViewModel()
    {
        _telescopeService = new();
        ResourceNodes = new();
        FilterByOptions = new()
        {
            new FilterTargetOption("Name Only", FilterBy.ResourceName),
            new FilterTargetOption("Data Only", FilterBy.ResourceData),
            new FilterTargetOption("Name & Data", FilterBy.ResourceName ^ FilterBy.ResourceData)
        };

        FilterCommand = new RelayCommand((parameter) => true, OnInvokeFilter);
        CopyToClipboardCommand = new RelayCommand(CanCopy, OnCopyToClipboard);

        _isTestMode = false;
        _fakeResources = new()
        {
            new ResourceNode(
                "Subscription #1", 
                ResourceNodeType.Subscription,
                () => Task.FromResult(
                    new List<ResourceNode>
                    {
                        new ResourceNode("Group #1_1", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #1_1_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                    new ResourceNode("App #1_1_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                    new ResourceNode("App #1_1_3", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #1_2", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #1_2_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                    new ResourceNode("App #1_2_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    }
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #1_3", ResourceNodeType.ResourceGroup, null, true)
                    }.AsEnumerable()),
                true){ IsExpanded = true },
            new ResourceNode(
                "Subscription #2", 
                ResourceNodeType.Subscription,
                () => Task.FromResult(
                    new List<ResourceNode>
                    {
                        new ResourceNode("Group #2_1", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #2_1_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                    new ResourceNode("App #2_1_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                    new ResourceNode("App #2_1_3", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #2_2", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #2_2_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    },
                                    new ResourceNode("App #2_2_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString()
                                    }
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #2_3", ResourceNodeType.ResourceGroup, null, true)
                    }.AsEnumerable()), 
                true){ IsExpanded = true }
        };
    }

    #endregion

    #region Commands

    public RelayCommand FilterCommand { get; }

    public RelayCommand CopyToClipboardCommand { get; }

    #endregion

    #region Properties

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

    /// <summary>
    /// Indicates if the "Copied" popup needs to be shown
    /// </summary>
    public bool NodesJustCopiedPopupOpened
    {
        get => _nodesJustCopiedPopupOpened;
        set
        {
            _nodesJustCopiedPopupOpened = value;
            RaisePropertyChanged();
        }
    }

    public MainWindow ToolWindowPane { get; set; }

    /// <summary>
    /// Selected filtration type
    /// </summary>
    public FilterTargetOption SelectedFilterOption
    {
        get => _selectedFilterOption;
        set
        {
            _selectedFilterOption = value;
            RaisePropertyChanged();

            OnInvokeFilter(new FilterOptions { QueryText = _appliedSearchQueryText, StringComparison = _appliedStringComparison});
        }
    }

    /// <summary>
    /// List of available filtrations
    /// </summary>
    public List<FilterTargetOption> FilterByOptions { get; }

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
        }
    }

    #endregion

    #region Public Methods

    public void OnInvokeFilter(object parameter)
    {
        if (parameter is not FilterOptions filterOptions)
            return;

        // fire and forget
        OnFilterAsync(filterOptions).Forget();
    }

    /// <summary>
    /// Handles filtering of tree nodes
    /// </summary>
    /// <param name="parameter">any <see cref="object"/> parameter</param>
    /// <returns></returns>
    public async Task OnFilterAsync(FilterOptions filterOptions)
    {
        if (_isFiltering)
            return;

        // avoid doing filtering to get the same result
        if (string.Equals(
                filterOptions.QueryText, _appliedSearchQueryText) && 
                _appliedFilterOption == SelectedFilterOption.Value && 
                _appliedStringComparison == filterOptions.StringComparison)
            return;

        IsCaseSensitive = !filterOptions.StringComparison.IsIgnoreCaseComparison();
        _isFiltering = true;

        await DoAsync(() =>
        {
            if (!string.IsNullOrEmpty(filterOptions.QueryText))
            {
                NodeFilter filter = new()
                {
                    SearchText = filterOptions.QueryText,
                    FilterByOptions = SelectedFilterOption.Value,
                    StringComparison = filterOptions.StringComparison
                };

                foreach (ResourceNode subscription in ResourceNodes)
                {
                    FilterNodes(subscription, filter);
                }
            }
            else
            {
                foreach (ResourceNode subscription in ResourceNodes)
                {
                    foreach (ResourceNode node in subscription.Descendants())
                    {
                        if (node.Type == ResourceNodeType.LogicApp)
                            node.IsVisible = true;
                        else
                            node.IsVisible = node.IsExpanded = node.ResourceNodes.Any();

                        // clear the matches info
                        node.FilterMatches.Apply(null);
                    }
                }
            }

            return Task.CompletedTask;
        }, "Filtering...");

        if (SelectedNode != null && !SelectedNode.IsVisible)
            SelectedNode = null;

        // remember the query text to avoid doing useless filtering
        _appliedSearchQueryText = filterOptions.QueryText;
        _appliedFilterOption = SelectedFilterOption.Value;
        _appliedStringComparison = filterOptions.StringComparison;

        // indicate that the filtering has been completed
        _isFiltering = false;
    }

    /// <summary>
    /// Handles initial loading of the view data
    /// </summary>
    /// <param name="parameter">any <see cref="object"/> parameter</param>
    /// <returns></returns>
    public async Task OnLoadedAsync(object parameter)
    {
        try
        {
            if (ResourceNodes.Any())
                ResourceNodes.Clear();

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
                await DoAsync(async () =>
                {
                    var subscriptions = await DoAsync<IEnumerable<SubscriptionResource>>(() =>
                    {
                        return _telescopeService.GetSubscriptionsAsync();
                    }, "Loading subscriptions...");

                    if (subscriptions.Any())
                    {
                        foreach (var subscription in subscriptions)
                        {
                            ResourceNode node = new(subscription.Data.DisplayName, ResourceNodeType.Subscription, () => ExpandSubscriptionAsync(subscription));

                            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                            {
                                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                ResourceNodes.Add(node);
                            });

                            node.OnExpandAsync(null).Forget();
                        }
                    }
                    else
                    {
                        await ShowWarningInfoBarAsync("No subscriptions found");
                    }
                }, "Loading Subscriptions...");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            // todo: handle
        }
    }

    #endregion

    #region Private Methods

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
    /// Copies names(IDs) of all <see cref="ResourceNodes"/> having <see cref="ResourceNode.IsSelected"/> as true to clipboard
    /// </summary>
    /// <param name="obj"></param>
    private void OnCopyToClipboard(object obj)
    {
        _isCopying = true;

        try
        {
            StringBuilder sb = new();

            foreach (ResourceNode subscription in ResourceNodes)
            {
                foreach (ResourceNode node in subscription.Descendants())
                {
                    if (node.IsSelected)
                        sb.AppendLine($"[{node.Type}] {node.Id}");
                }
            }

            string textToCopy = sb.ToString();

            if (string.IsNullOrEmpty(textToCopy))
                CopyPopupText = NoNodesToCopyText;
            else
            {
                Clipboard.SetText(sb.ToString());
                CopyPopupText = NodesCopiedText;
            }

            ShowCopyToClipboardPopupAsync().Forget();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            // todo: handling
        }
        finally
        {
            _isCopying = false;
        }
    }

    private async Task ShowCopyToClipboardPopupAsync()
    {
        NodesJustCopiedPopupOpened = true;

        await Task.Delay(PopupDefaultDisplayTime);

        NodesJustCopiedPopupOpened = false;
    }

    /// <summary>
    /// Filters the nodes collection where <paramref name="root"/> is the tree(or subtree) root
    /// </summary>
    /// <param name="root">(sub)tree root</param>
    /// <param name="filter">options of filtering</param>
    /// <returns></returns>
    private bool FilterNodes(ResourceNode root, NodeFilter filter)
    {
        if (root is null)
            return false;

        // if it's an app - just check the filter
        if (root.Type == ResourceNodeType.LogicApp)
            return root.IsVisible = root.ApplyFilter(filter);
            //return root.IsVisible = root.Matches(filter);
        else
        {
            bool hasVisibleItems = false;

            // for subscriptions and groups - check to children
            if (root.ResourceNodes?.Any() == true)
            {
                foreach (ResourceNode node in root.ResourceNodes)
                {
                    if (FilterNodes(node, filter) && !hasVisibleItems)
                        hasVisibleItems = true;
                }
            }

            // expand and shoud the node if it has searched nodes
            return root.IsExpanded = root.IsVisible = hasVisibleItems;
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

        return groups.Select(g => new ResourceNode(g.Data.Name, ResourceNodeType.ResourceGroup, () => ExpandGroupsAsync(g), true));
    }

    /// <summary>
    /// Callback that's performed when a group node is being expanded
    /// </summary>
    /// <param name="subscription">expanding group node</param>
    /// <returns></returns>
    private async Task<IEnumerable<ResourceNode>> ExpandGroupsAsync(ResourceGroupResource resourceGroup)
    {
        IEnumerable<AzureLogicAppInfo> logicApps = await _telescopeService.GetLogicAppsAsync(resourceGroup);

        return logicApps;
    }

    /// <summary>
    /// Prepares the view for doing any operation - shows the loader and loading text
    /// </summary>
    /// <param name="busyText"></param>
    private void SetBusy(string busyText = null)
    {
        IsBusy = true;
        BusyText = busyText;
    }

    /// <summary>
    /// Hides the loader and loading text - should be called right after the operation is completed
    /// </summary>
    private void Free()
    {
        IsBusy = false;
        BusyText = null;
    }

    /// <summary>
    /// Wrapping method to perform any operation with displaying the loader
    /// </summary>
    /// <param name="action">operation to perform</param>
    /// <param name="busyText">loading text to display while <paramref name="action"/> is being performed</param>
    /// <returns></returns>
    private async Task DoAsync(Func<Task> action, string busyText = null)
    {
        SetBusy(busyText);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            // TODO: handle
        }
        finally
        {
            Free();
        }
    }

    /// <summary>
    /// Wrapping method to perform any operation returning <typeparamref name="T"/> with displaying the loader
    /// </summary>
    /// <typeparam name="T">type of <paramref name="action"/> result</typeparam>
    /// <param name="action">operation to perform</param>
    /// <param name="busyText">loading text to display while <paramref name="action"/> is being performed</param>
    /// <returns></returns>
    private async Task<T> DoAsync<T>(Func<Task<T>> action, string busyText = null)
    {
        SetBusy(busyText);

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            // TODO: handle
            throw;
        }
        finally
        {
            Free();
        }
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
            isCloseButtonVisible: false);

        return infoBar;
    }

    /// <summary>
    /// Displays a warning <paramref name="message"/> based on <see cref="InfoBarModel"/>
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task ShowWarningInfoBarAsync(string message)
    {
        IVsInfoBarHost host = await ToolWindowPane.GetInfoBarHostAsync();

        await InfoBarService.Instance.ShowInfoBarAsync(host, CreateWarningInfoBar(message));
    }

    #endregion

    #endregion
}
