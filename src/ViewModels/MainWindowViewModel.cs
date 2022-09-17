﻿using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Models;
using Cats.Telescope.VsExtension.Core.Services;
using Cats.Telescope.VsExtension.Mvvm.Commands;
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

    private TelescopeService _telescopeService;

    private List<ResourceNode> _fakeResources;
    private bool _isTestMode;

    #endregion

    #region Contructors

    public MainWindowViewModel()
    {
        _telescopeService = new();
        ResourceNodes = new();

        SearchCommand = new AsyncRelayCommand(OnSearchAsync);

        _isTestMode = true;
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

    public AsyncRelayCommand SearchCommand { get; }

    #endregion

    #region Properties

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            RaisePropertyChanged();
        }
    }

    public ResourceNode SelectedNode
    {
        get => _selectedNode;
        set
        {
            _selectedNode = value;
            RaisePropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            RaisePropertyChanged();
        }
    }

    public string BusyText
    {
        get => _busyText ?? DefaultBusyText;
        set
        {
            _busyText = value;
            RaisePropertyChanged();
        }
    }

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

    public Task OnSearchAsync(object parameter)
    {
        string searchText = SearchText?.ToUpper();

        if (!string.IsNullOrEmpty(searchText))
        {
            NodeFilter filter = new() { SearchText = searchText };

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

        return Task.CompletedTask;
    }

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
                var subscriptions = await Do<IEnumerable<SubscriptionResource>>(() =>
                {
                    return _telescopeService.GetSubscriptionsAsync();
                }, "Loading subscriptions...");


                if (subscriptions.Any())
                {
                    foreach (var subscription in subscriptions)
                    {
                        ResourceNodes.Add(new ResourceNode(subscription.Data.DisplayName, ResourceNodeType.Subscription, () => ExpandSubscriptionAsync(subscription)));
                    }
                }
            }
        }
        catch (Exception ex)
        {

        }
    }

    #endregion

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

            return root.IsVisible = hasVisibleItems;
        }
    }

    private async Task<IEnumerable<ResourceNode>> ExpandSubscriptionAsync(SubscriptionResource subscription)
    {
        IEnumerable<ResourceGroupResource> groups = await _telescopeService.GetResourceGroupsAsync(subscription);

        return groups.Select(g => new ResourceNode(g.Data.Name, ResourceNodeType.ResourceGroup, () => ExpandGroupsAsync(g), true));
    }

    private async Task<IEnumerable<ResourceNode>> ExpandGroupsAsync(ResourceGroupResource resourceGroup)
    {
        IEnumerable<AzureLogicAppInfo> logicApps = await _telescopeService.GetLogicAppsAsync(resourceGroup);

        if (logicApps.Any())
        {

        }

        return logicApps;
    }

    #region Private Methods

    private void SetBusy(string busyText = null)
    {
        IsBusy = true;
        BusyText = busyText;
    }

    private void Free()
    {
        IsBusy = false;
        BusyText = null;
    }

    private async Task Do(Func<Task> action, string busyText = null)
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

    private async Task<T> Do<T>(Func<Task<T>> action, string busyText = null)
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

internal class NodeFilter
{
    public string SearchText { get; set; }

    //TODO: add the rest filters here
}
