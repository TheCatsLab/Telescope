using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Models;
using Cats.Telescope.VsExtension.Core.Services;
using Cats.Telescope.VsExtension.Mvvm.Commands;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.ViewModels;

internal class MainWindowViewModel : ViewModelBase
{
    #region Constants

    private const string DefaultBusyText = "Loading...";

    #endregion

    #region Fields

    private bool _isBusy;
    private string _busyText;
    private string _searchText;
    private ObservableCollection<ResourceNode> _resourceNodes;
    private ResourceNode _selectedNode;
    private FilterByOption _selectedFilterOption;

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
            new FilterByOption("Name Only", FilterBy.ResourceName),
            new FilterByOption("Data Only", FilterBy.ResourceData),
            new FilterByOption("Name & Data", FilterBy.ResourceName ^ FilterBy.ResourceData)
        };

        FilterCommand = new AsyncRelayCommand(OnFilterAsync);

        _isTestMode = false;
        _fakeResources = new()
        {
            new ResourceNode("Subscription #1", ResourceNodeType.Subscription,
                () => Task.FromResult(
                    new List<ResourceNode>
                    {
                        new ResourceNode("Group #1_1", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #1_1_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                    new ResourceNode("App #1_1_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                    new ResourceNode("App #1_1_3", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #1_2", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #1_2_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                    new ResourceNode("App #1_2_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    }
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #1_3", ResourceNodeType.ResourceGroup, null, true)
                    }.AsEnumerable())),
            new ResourceNode("Subscription #2", ResourceNodeType.Subscription,
                () => Task.FromResult(
                    new List<ResourceNode>
                    {
                        new ResourceNode("Group #2_1", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #2_1_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                    new ResourceNode("App #2_1_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                    new ResourceNode("App #2_1_3", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #2_2", ResourceNodeType.ResourceGroup,
                            () => Task.FromResult(
                                new List<ResourceNode>
                                {
                                    new ResourceNode("App #2_2_1", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    },
                                    new ResourceNode("App #2_2_2", ResourceNodeType.LogicApp)
                                    {
                                        Data = "Json here"
                                    }
                                }.AsEnumerable()), true),
                        new ResourceNode("Group #2_3", ResourceNodeType.ResourceGroup, null, true)
                    }.AsEnumerable()))
        };
    }

    #endregion

    #region Commands

    public AsyncRelayCommand FilterCommand { get; }

    #endregion

    #region Properties

    /// <summary>
    /// Selected filtration type
    /// </summary>
    public FilterByOption SelectedFilterOption
    {
        get => _selectedFilterOption;
        set
        {
            _selectedFilterOption = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// List of available filtrations
    /// </summary>
    public List<FilterByOption> FilterByOptions { get; }

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

            // fire and forget
            // todo: move to an extension
            _ = OnFilterAsync(_searchText).ConfigureAwait(false);
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
            _selectedNode = value;
            RaisePropertyChanged();
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

    /// <summary>
    /// Handles filtering of tree nodes
    /// </summary>
    /// <param name="parameter">any <see cref="object"/> parameter</param>
    /// <returns></returns>
    public async Task OnFilterAsync(object parameter)
    {
        SelectedNode = null;
        string searchText = (parameter as string)?.ToUpper();

        await DoAsync(async () =>
        {
            if (!string.IsNullOrEmpty(searchText))
            {
                NodeFilter filter = new() 
                { 
                    SearchText = searchText,
                    FilterByOptions = SelectedFilterOption.Value
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
                        node.IsVisible = true;
                    }
                }
            }
        }, "Filtering...");    
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

                            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate {
                                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                ResourceNodes.Add(node);
                            });

                            _ = node.OnExpandAsync(null).ConfigureAwait(false);
                        }
                    }
                }, "Loading Subscriptions...");
            }
        }
        catch (Exception ex)
        {

        }
    }

    #endregion

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
            return root.IsVisible = root.Matches(filter);
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

            if (hasVisibleItems)
            {

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

        if (logicApps.Any())
        {

        }

        return logicApps;
    }

    #region Private Methods

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
            // TODO: handle
            throw;
        }
        finally
        {
            Free();
        }
    }

    #endregion
}
