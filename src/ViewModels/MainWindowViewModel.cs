﻿using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core;
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

    // conatins ids of all the loadings activities, if empty - it means there are no active loadings
    private SynchronizedCollection<Guid> _activeLoadings = new();

    private readonly TelescopeService _telescopeService;

    private readonly List<ResourceNode> _fakeResources;
    private readonly bool _isTestMode;

    #endregion

    #region Contructors

    public MainWindowViewModel()
    {
        _telescopeService = new();
        ResourceNodes = new();
        SelectedFilterOptions = FilterBy.ResourceName;

        FilterCommand = new RelayCommand((parameter) => true, OnInvokeFilter);
        CopyToClipboardCommand = new RelayCommand(CanCopy, OnCopyToClipboard);
        OpenResourceCommand = new RelayCommand(CanOpenResource, OnOpenResource);
        ToggleFilterOptionsVisibilityCommand = new RelayCommand((p) => true, OnToggleFilterOptionsVisibility);

        _telescopeService.LoadingStarted += _telescopeService_LoadingStarted;
        _telescopeService.LoadingCompleted += _telescopeService_LoadingCompleted;

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
                                        Data = "Json here" + Guid.NewGuid().ToString(),
                                        Tags = new Dictionary<string, string>() { { "tag#1_1", "tag#1value" + Guid.NewGuid()} }
                                    },
                                    new ResourceNode("App #1_1_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString(),
                                        Tags = new Dictionary<string, string>() { { "tag#1_2", "tag#1value" + Guid.NewGuid()} }
                                    },
                                    new ResourceNode("App #1_1_3", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here" + Guid.NewGuid().ToString(),
                                        Tags = new Dictionary<string, string>() { { "tag#1_3", "tag#1value" + Guid.NewGuid()} }
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

    #region Events

    /// <summary>
    /// Occurs when any text has been copied to Clipboard 
    /// </summary>
    internal event EventHandler<string> CopiedToClipboard;

    #endregion

    #region Commands

    public RelayCommand FilterCommand { get; }
    public RelayCommand CopyToClipboardCommand { get; }
    public RelayCommand OpenResourceCommand { get; }
    public RelayCommand ToggleFilterOptionsVisibilityCommand { get; }

    #endregion

    #region Properties

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
                _appliedFilterOption == SelectedFilterOptions && 
                _appliedStringComparison == filterOptions.StringComparison)
            return;

        IsCaseSensitive = !filterOptions.StringComparison.IsIgnoreCaseComparison();
        _isFiltering = true;

        await DoAsync(() =>
        {
            if (!string.IsNullOrEmpty(filterOptions.QueryText) && ((int)SelectedFilterOptions) > 0)
            {
                NodeFilter filter = new()
                {
                    SearchText = filterOptions.QueryText,
                    FilterByOptions = SelectedFilterOptions,
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
                        if (node.IsContainerNode)
                            node.IsVisible = node.IsExpanded = node.ResourceNodes.Any();
                        else
                            node.IsVisible = true;

                        // clear the matches info
                        node.FilterMatches.Apply(null);
                    }
                }
            }

            return Task.CompletedTask;
        });

        if (SelectedNode != null && !SelectedNode.IsVisible)
            SelectedNode = null;

        // remember the query text to avoid doing useless filtering
        _appliedSearchQueryText = filterOptions.QueryText;
        _appliedFilterOption = SelectedFilterOptions;
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
                    var tenantsLoad = _telescopeService.GetTenantsAsync();

                    var subscriptions = await DoAsync<IEnumerable<SubscriptionResource>>(() =>
                    {
                        return _telescopeService.GetSubscriptionsAsync();
                    });

                    var tenants = await tenantsLoad;

                    if (subscriptions != null && subscriptions.Any())
                    {
                        foreach (var subscription in subscriptions)
                        {
                            string domain = tenants.FirstOrDefault(t => t.Data.TenantId == subscription.Data.TenantId)?.Data.DefaultDomain;

                            ResourceNode node = new(subscription.Data.DisplayName, ResourceNodeType.Subscription, () => ExpandSubscriptionAsync(subscription))
                            {
                                LinkToResource = $"{AzurePortalDomain}@{domain}/resource/subscriptions/{subscription.Id.SubscriptionId}"
                            };

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
                });
            }
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }

    #endregion

    #region Private Methods

    private void OnToggleFilterOptionsVisibility(object isVisibleValue)
    {
        if (isVisibleValue is bool isVisible)
        {
            IsFilterOptionsOpened = isVisible;

            // trigger filtering if the options popup is closed
            if(!IsFilterOptionsOpened)
                OnInvokeFilter(new FilterOptions { QueryText = _appliedSearchQueryText, StringComparison = _appliedStringComparison });
        }
    }

    private bool CanOpenResource(object obj)
    {
        return !string.IsNullOrEmpty(SelectedNode?.LinkToResource);
    }

    private void OnOpenResource(object obj)
    {
        try
        {
            Process.Start(new ProcessStartInfo(SelectedNode.LinkToResource));
        }
        catch(Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }

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
    private void OnCopyToClipboard(object valueToCopy)
    {
        _isCopying = true;

        string target = null;

        try
        {
            StringBuilder sb = new();

            // copy the tree nodes by default
            if(valueToCopy is null)
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
                        sb.AppendLine(valueToCopy.ToString());
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
        if (!root.IsContainerNode)
            return root.IsVisible = root.ApplyFilter(filter);
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
        Task<IEnumerable<AzureLogicAppInfo>> logicAppsLoad = _telescopeService.GetLogicAppsAsync(resourceGroup);
        Task<IEnumerable<WebAppInfo>> functionsLoad = _telescopeService.GetWebAppsAsync(resourceGroup);

        await Task.WhenAll(logicAppsLoad, functionsLoad);

        List<ResourceNode> resources = new(100);

        if (logicAppsLoad.Result.Any())
            resources.AddRange(logicAppsLoad.Result);

        if (functionsLoad.Result.Any())
            resources.AddRange(functionsLoad.Result);

        return resources;
    }

    private void _telescopeService_LoadingCompleted(object sender, Guid actionId)
    {
        _activeLoadings.Remove(actionId);

        if (_activeLoadings.Count == 0)
            Free();
    }

    private void _telescopeService_LoadingStarted(object sender, Guid actionId)
    {
        _activeLoadings.Add(actionId);

        if (IsBusy)
            return;
        else
            SetBusy();
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

    /// <summary>
    /// Wrapping method to perform any operation with displaying the loader
    /// </summary>
    /// <param name="action">operation to perform</param>
    /// <returns></returns>
    private async Task DoAsync(Func<Task> action)
    {
        SetBusy();

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
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
    /// <returns></returns>
    private async Task<T> DoAsync<T>(Func<Task<T>> action)
    {
        SetBusy();

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
            return default;
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
