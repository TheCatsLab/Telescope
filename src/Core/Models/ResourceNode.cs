using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Mvvm.Commands;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
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
    private FilterMatchViewModel _filterMatches;
    private ResourceNode _parentNode;

    #endregion

    #region Constructors

    public ResourceNode(string id, ResourceNodeType type, Func<Task<IEnumerable<ResourceNode>>> loadMoreAction = null, bool isAutoExpanded = false)
    {
        Id = id;
        Type = type;
        IsAutoExpanded = isAutoExpanded;
        FilterMatches = new();

        _loadMoreAction = loadMoreAction;

        // To make it available for expanding
        if (Type == ResourceNodeType.Subscription)
            ResourceNodes = new ObservableCollection<ResourceNode>()
            {
                new ResourceNode("Loading...", ResourceNodeType.Empty)
            };

        // DS
        // group node should consider if it has some children;
        // the Empty nodes are used as inline loading placeholders, so they stay visible
        IsVisible = Type != ResourceNodeType.GenericResource;

        ExpandCommand = new AsyncRelayCommand(OnExpandAsync);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to perform expanding of the node
    /// </summary>
    public AsyncRelayCommand ExpandCommand { get; }

    #endregion

    #region Properties

    /// <summary>
    /// Contains results of filter application to this node
    /// </summary>
    public FilterMatchViewModel FilterMatches
    {
        get => _filterMatches;
        set
        {
            _filterMatches = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Node id(usually it is an azure resource identifier as well)
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Full azure resource manager identifier of the resource, e.g. /subscriptions/../resourcegroups/../providers/../workflows/name
    /// </summary>
    public string ArmId { get; set; }

    /// <summary>
    /// Node data(usually - json of the resource properties)
    /// </summary>
    public string Data { get; set; }

    /// <summary>
    /// Link to the resource in Azure Portal
    /// </summary>
    public string LinkToResource { get; set; }

    /// <summary>
    /// Resource tags
    /// </summary>
    public Dictionary<string, string> Tags { get; set; }

    /// <summary>
    /// Resource environment variables (app settings)
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; }

    /// <summary>
    /// Resource type. The value is <see cref="ResourceNodeType"/>
    /// </summary>
    public ResourceNodeType Type { get; }

    /// <summary>
    /// Indicates if the node can contain other nodes, e.g. Subscription, Resource Group etc.
    /// </summary>
    public bool IsContainerNode => Type == ResourceNodeType.Subscription || Type == ResourceNodeType.ResourceGroup;

    /// <summary>
    /// Indicates if the node should be expanded automatically while the parent node is being expanded
    /// </summary>
    public bool IsAutoExpanded { get; }

    /// <summary>
    /// The node which contains this one
    /// </summary>
    public ResourceNode ParentNode
    {
        get => _parentNode;
        set
        {
            _parentNode = value;
            RaisePropertyChanged();

            // rebuild the link considering the parent node link
            LinkToResource = RebuildResourceLink();
        }
    }

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
                // show an inline loading indicator while the children are being loaded
                ResourceNodes.Add(new ResourceNode("Loading...", ResourceNodeType.Empty));

                IEnumerable<ResourceNode> children = await _loadMoreAction();

                // back to the UI thread to update the nodes collection
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ResourceNodes.Clear();

                if (children.Any())
                {
                    foreach (ResourceNode node in children)
                    {
                        // set the parent
                        node.ParentNode = this;

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
            catch (OperationCanceledException)
            {
                // the loading has been cancelled by the user
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ResourceNodes.Clear();
            }
            catch(Exception ex)
            {
                // remove the loading placeholder if the children cannot be loaded
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ResourceNodes.Clear();

                ex.LogAsync().Forget();
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
    /// Applies <paramref name="filter"/> to the node and calculates how many matches there are
    /// </summary>
    /// <param name="filter">node filter</param>
    /// <returns></returns>
    public NodeFilterResult MatchesCount(NodeFilter filter)
    {
        return NodeFilterMatcher.MatchesCount(Id, Data, Tags, EnvironmentVariables, filter);
    }

    /// <summary>
    /// Applies the current value of <see cref="IsSelected"/> to all children-nodes
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// Builds and returns a link to the resource based on <see cref="ParentNode" /> link
    /// </summary>
    /// <returns></returns>
    private string RebuildResourceLink()
    {
        string link = null;

        // DS
        // A resource link is built based on the parent one
        if (!string.IsNullOrEmpty(ParentNode.LinkToResource))
        {
            if(ParentNode?.Type == ResourceNodeType.ResourceGroup)
            {
                if (Type == ResourceNodeType.LogicApp)
                {
                    link = ParentNode.LinkToResource + "/providers/Microsoft.Logic/workflows/" + Id;
                }
                else if (Type == ResourceNodeType.Function || Type == ResourceNodeType.WebService)
                {
                    link = ParentNode.LinkToResource + "/providers/Microsoft.Web/sites/" + Id;
                }
            }            
            else if (Type == ResourceNodeType.ResourceGroup && ParentNode?.Type == ResourceNodeType.Subscription)
            {
                link = ParentNode.LinkToResource + "/resourcegroups/" + Id;
            }
        }

        return link;
    }

    #endregion
}