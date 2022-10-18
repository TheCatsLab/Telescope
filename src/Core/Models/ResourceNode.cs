using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Mvvm.Commands;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        // group node should consider if it has some children
        IsVisible = Type == ResourceNodeType.LogicApp || Type == ResourceNodeType.Subscription;

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
    /// Resource type. The value is <see cref="ResourceNodeType"/>
    /// </summary>
    public ResourceNodeType Type { get; }

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
                IEnumerable<ResourceNode> children = await _loadMoreAction();

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
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
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
    public bool ApplyFilter(NodeFilter filter)
    {
        NodeFilterResult result = MatchesCount(filter);

        FilterMatches.Apply(result);

        return result.Success;
    }

    /// <summary>
    /// Applies <paramref name="filter"/> to the node and calculates how many matches there are
    /// </summary>
    /// <param name="filter">node filter</param>
    /// <returns></returns>
    public NodeFilterResult MatchesCount(NodeFilter filter)
    {
        NodeFilterResult result = new();

        if (filter is null)
            return result;

        if (string.IsNullOrEmpty(filter.SearchText))
            return result;

        if (Id is null)
            return result;

        RegexOptions regexOptions = RegexOptions.None;

        if (filter.StringComparison.IsIgnoreCaseComparison())
            regexOptions ^= RegexOptions.IgnoreCase;

        // filter by name
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceName))
        {
            int findings = Regex.Matches(Id, filter.SearchText, regexOptions).Count;

            if(findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceName, findings));
        }

        // filter by data
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceData))
        {
            int findings = Regex.Matches(Data, filter.SearchText, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceData, findings));
        }

        // filter by tag keys
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceTagKeys) && Tags?.Keys != null)
        {
            int findings = Regex.Matches(string.Concat(Tags.Keys), filter.SearchText, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceTagKeys, findings));
        }

        // filter by tag values
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceTagValues) && Tags?.Values != null)
        {
            int findings = Regex.Matches(string.Concat(Tags.Values), filter.SearchText, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceTagValues, findings));
        }

        return result;
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
            if (Type == ResourceNodeType.LogicApp && ParentNode?.Type == ResourceNodeType.ResourceGroup)
            {
                link = ParentNode.LinkToResource + "/providers/Microsoft.Logic/workflows/" + Id;
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