﻿using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Mvvm.Commands;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.Core.Models;

/// <summary>
/// Presents a tree node for an Azure Resource
/// </summary>
internal class ResourceNode : ViewModelBase
{
    #region Fields

    private readonly Func<Task<IEnumerable<ResourceNode>>> _loadMoreAction;
    private ObservableCollection<ResourceNode> _resourceNodes;
    private bool _isLoaded;
    private bool _isVisible;
    private bool _isExpanded;
    private bool _isSelected;

    #endregion

    #region Constructors

    public ResourceNode(string id, ResourceNodeType type, Func<Task<IEnumerable<ResourceNode>>> loadMoreAction = null, bool isAutoExpanded = false)
    {
        Id = id;
        Type = type;
        IsAutoExpanded = isAutoExpanded;

        _loadMoreAction = loadMoreAction;

        // To make it available for expanding
        if (Type == ResourceNodeType.Subscription)
            ResourceNodes = new ObservableCollection<ResourceNode>()
            {
                new ResourceNode("Loading...", ResourceNodeType.Empty)
            };

        // DS
        // group node should consider if it has some children
        IsVisible = Type == ResourceNodeType.LogicApp || Type == ResourceNodeType.Subscription;

        ExpandCommand = new AsyncRelayCommand(OnExpandAsync);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Command to perform expanding of the node
    /// </summary>
    public AsyncRelayCommand ExpandCommand { get; }

    /// <summary>
    /// Node id(usually it is an azure resource identifier as well)
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Node data(usually - json of the resource properties)
    /// </summary>
    public string Data { get; set; }

    /// <summary>
    /// Resource tags
    /// </summary>
    public Dictionary<string, string> Tags { get; set; }

    /// <summary>
    /// Resource type. The value is <see cref="ResourceNodeType"/>
    /// </summary>
    public ResourceNodeType Type { get; }

    /// <summary>
    /// Indicates if the node should be expanded automatically while the parent node is being expanded
    /// </summary>
    public bool IsAutoExpanded { get; }

    /// <summary>
    /// Idicates if the node should be visible
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;

            if (!_isVisible)
                IsSelected = false;

            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Indicates if the node is selected
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            RaisePropertyChanged();

            ApplySelectionToChildrenAsync().Forget();
        }
    }

    /// <summary>
    /// Indicate if the node is expanded
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Child nodes collection
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

    #region Methods

    /// <summary>
    /// Handles expanding of the node
    /// </summary>
    /// <param name="parameter">any <see cref="object"/> parameter</param>
    /// <returns></returns>
    public async Task OnExpandAsync(object parameter)
    {
        IsExpanded = true;

        if (_isLoaded)
        {
            return;
        }

        if (ResourceNodes != null)
            ResourceNodes.Clear();
        else
            ResourceNodes = new();

        _isLoaded = true;

        if (_loadMoreAction != null)
        {
            try
            {
                IEnumerable<ResourceNode> children = await _loadMoreAction();

                if (children.Any())
                {
                    foreach (ResourceNode node in children)
                    {
                        await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                            ResourceNodes.Add(node);
                        });

                        if(node.IsAutoExpanded)
                            node.ExpandCommand.Execute(this);
                    }

                    if (Type == ResourceNodeType.ResourceGroup)
                    {
                        IsVisible = true;
                    }
                }
            }
            catch(Exception ex)
            {
                // TODO: add logging
            }
        }
        else
        {
            //if(Type != ResourceNodeType.LogicApp)
            //    ResourceNodes.Add(new ResourceNode("-No items-", ResourceNodeType.Empty));
        }
    }

    /// <summary>
    /// Returns all node descendants
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ResourceNode> Descendants(Func<ResourceNode, bool> predicate = null)
    {
        Stack<ResourceNode> nodes = new (new[] { this });
        while (nodes.Any())
        {
            ResourceNode node = nodes.Pop();
            yield return node;
            
            if(node.ResourceNodes != null && node.ResourceNodes.Any())
                foreach (var n in node.ResourceNodes)
                {
                    if (predicate is null)
                        nodes.Push(n);
                    else if (predicate(n))
                        nodes.Push(n);
                }
        }
    }

    /// <summary>
    /// Returns <see cref="true"/> if the node mathes <paramref name="filter"/>
    /// </summary>
    /// <param name="filter">node filter to apply</param>
    /// <returns></returns>
    public bool Matches(NodeFilter filter)
    {
        if (filter is null)
            return true;

        if (string.IsNullOrEmpty(filter.SearchText))
            return true;

        if (Id is null)
            return true;

        bool result = false;

        StringComparison stringComparison = filter.StringComparison;

        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceName))
        {
            result ^= Id.Contains(filter.SearchText, stringComparison);
        }

        if(!result && filter.FilterByOptions.HasFlag(FilterBy.ResourceData))
        {
            result ^= !string.IsNullOrEmpty(Data) && Data.Contains(filter.SearchText, stringComparison);
        }

        return result;
    }

    private async Task ApplySelectionToChildrenAsync()
    {
        if(ResourceNodes != null && ResourceNodes.Any())
            foreach (ResourceNode node in ResourceNodes)
            {
                await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    node.IsSelected = IsSelected;
                });
            }
    }

    #endregion
}
