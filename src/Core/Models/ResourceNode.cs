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

    private Func<Task<IEnumerable<ResourceNode>>> _loadMoreAction;
    private Func<Task<string>> _dataLoader;
    private Func<Task<Dictionary<string, string>>> _environmentVariablesLoader;
    private Task<string> _dataLoadTask;
    private Task<Dictionary<string, string>> _environmentVariablesLoadTask;
    private readonly object _detailsSync = new();
    private string _data;
    private Dictionary<string, string> _environmentVariables;
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
        RetryCommand = new RelayCommand(_ => true, _ => ParentNode?.RetryLoad());
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to perform expanding of the node
    /// </summary>
    public AsyncRelayCommand ExpandCommand { get; }

    /// <summary>
    /// Command to retry the failed loading; used by the error placeholder nodes
    /// </summary>
    public RelayCommand RetryCommand { get; }

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
    /// Node data(usually - json of the resource properties); materialized on demand
    /// by <see cref="EnsureDataAsync"/> when a loader is set
    /// </summary>
    public string Data
    {
        get => _data;
        set
        {
            _data = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Loads the node data on demand; used to avoid materializing the configuration
    /// JSON of every app in the tree
    /// </summary>
    public Func<Task<string>> DataLoader
    {
        get => _dataLoader;
        set => _dataLoader = value;
    }

    /// <summary>
    /// Link to the resource in Azure Portal
    /// </summary>
    public string LinkToResource { get; set; }

    /// <summary>
    /// Resource tags
    /// </summary>
    public Dictionary<string, string> Tags { get; set; }

    /// <summary>
    /// Resource environment variables (app settings); materialized on demand by
    /// <see cref="EnsureEnvironmentVariablesAsync"/> when a loader is set
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables
    {
        get => _environmentVariables;
        set
        {
            _environmentVariables = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Loads the environment variables on demand; they cost an extra ARM request per
    /// app, so they are only fetched when the node is selected or a filter needs them
    /// </summary>
    public Func<Task<Dictionary<string, string>>> EnvironmentVariablesLoader
    {
        get => _environmentVariablesLoader;
        set => _environmentVariablesLoader = value;
    }

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
            RaisePropertyChanged(nameof(IsChecked));

            // the parent checkbox state is derived from the children
            ParentNode?.RaisePropertyChanged(nameof(IsChecked));

            ApplySelectionToChildren();
        }
    }

    /// <summary>
    /// Three-state checkbox value of the node:
    /// for containers it is derived from the children(all selected, none selected or mixed),
    /// for leaves it repeats <see cref="IsSelected"/>
    /// </summary>
    public bool? IsChecked
    {
        get
        {
            if (!IsContainerNode)
                return IsSelected;

            List<ResourceNode> realChildren = ResourceNodes?
                .Where(n => n.Type != ResourceNodeType.Empty && n.Type != ResourceNodeType.Error)
                .ToList();

            if (realChildren is null || realChildren.Count == 0)
                return false;

            if (realChildren.All(n => n.IsChecked == true))
                return true;

            return realChildren.Any(n => n.IsChecked != false) ? null : false;
        }
        set
        {
            if (!value.HasValue)
                return;

            IsSelected = value.Value;
        }
    }

    /// <summary>
    /// Resets the private loading state so the next expand reloads the children
    /// </summary>
    internal void RetryLoad()
    {
        _isLoaded = false;
        ResourceNodes?.Clear();
        ExpandCommand.Execute(null);
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
    /// Materializes <see cref="Data"/>; concurrent callers share the same load
    /// </summary>
    public Task<string> EnsureDataAsync()
    {
        lock (_detailsSync)
        {
            if (_data != null)
                return Task.FromResult(_data);

            return _dataLoadTask ??= LoadDataCoreAsync();
        }
    }

    /// <summary>
    /// Materializes <see cref="EnvironmentVariables"/>; concurrent callers share the same load
    /// </summary>
    public Task<Dictionary<string, string>> EnsureEnvironmentVariablesAsync()
    {
        lock (_detailsSync)
        {
            if (_environmentVariables != null)
                return Task.FromResult(_environmentVariables);

            return _environmentVariablesLoadTask ??= LoadEnvironmentVariablesCoreAsync();
        }
    }

    private async Task<string> LoadDataCoreAsync()
    {
        // take the loader out of the field so its closure (and the payload it captured)
        // is released as soon as the data has been produced
        Func<Task<string>> loader = _dataLoader;
        _dataLoader = null;

        string data = null;

        if (loader != null)
        {
            try
            {
                data = await loader();
            }
            catch
            {
                // the configuration is not critical - the details pane just stays empty
            }
        }

        // both properties are bound to the UI, so assign them on the main thread
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        Data = data;

        return data;
    }

    private async Task<Dictionary<string, string>> LoadEnvironmentVariablesCoreAsync()
    {
        Func<Task<Dictionary<string, string>>> loader = _environmentVariablesLoader;
        _environmentVariablesLoader = null;

        Dictionary<string, string> environmentVariables = null;

        if (loader != null)
        {
            try
            {
                environmentVariables = await loader();
            }
            catch
            {
                // app settings are not critical for the app representation
            }
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        EnvironmentVariables = environmentVariables;

        return environmentVariables;
    }

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

        // take the delegate out of the field: its closure captures the Azure SDK resources
        // and the view model, which should not be kept alive once the load has succeeded;
        // on failure it is put back so the retry link keeps working
        Func<Task<IEnumerable<ResourceNode>>> loadMoreAction = _loadMoreAction;
        _loadMoreAction = null;

        if (loadMoreAction != null)
        {
            try
            {
                // show an inline loading indicator while the children are being loaded
                ResourceNodes.Add(new ResourceNode("Loading...", ResourceNodeType.Empty));

                IEnumerable<ResourceNode> children = await loadMoreAction();

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
                _loadMoreAction = loadMoreAction;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ResourceNodes.Clear();
            }
            catch(Exception ex)
            {
                // replace the loading placeholder with a retryable error node
                _loadMoreAction = loadMoreAction;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ResourceNodes.Clear();

                ResourceNode errorNode = new("Loading failed. Click to retry", ResourceNodeType.Error);
                errorNode.ParentNode = this;
                ResourceNodes.Add(errorNode);

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
    private void ApplySelectionToChildren()
    {
        if (ResourceNodes != null && ResourceNodes.Any())
        {
            foreach (ResourceNode node in ResourceNodes)
            {
                node.IsSelected = IsSelected;
            }
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
            else if (Type == ResourceNodeType.DeploymentSlot &&
                     (ParentNode?.Type == ResourceNodeType.Function || ParentNode?.Type == ResourceNodeType.WebService))
            {
                link = ParentNode.LinkToResource + "/slots/" + Id;
            }
        }

        return link;
    }

    #endregion
}