using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Mvvm.Commands;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.Core.Models;

internal class ResourceNode : ViewModelBase
{
    private ObservableCollection<ResourceNode> _resourceNodes;
    private Func<Task<IEnumerable<ResourceNode>>> _loadMoreAction;
    private bool _isLoaded;
    private bool _isVisible;

    public ResourceNode(string id, ResourceNodeType type, Func<Task<IEnumerable<ResourceNode>>> loadMoreAction = null, bool isAutoExpanded = false)
    {
        Id = id;
        Type = type;
        IsAutoExpanded = isAutoExpanded;

        _loadMoreAction = loadMoreAction;

        if (Type != ResourceNodeType.Empty)
            ResourceNodes = new ObservableCollection<ResourceNode>()
            {
                new ResourceNode("Loading...", ResourceNodeType.Empty)
            };

        // DS
        // group node should consider if it has some children
        IsVisible = Type == ResourceNodeType.LogicApp || Type == ResourceNodeType.Subscription;

        ExpandCommand = new AsyncRelayCommand(OnExpandAsync);
    }


    public AsyncRelayCommand ExpandCommand { get; }

    public string Id { get; }

    public ResourceNodeType Type { get; }

    public bool IsAutoExpanded { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
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

    public async Task OnExpandAsync(object parameter)
    {
        if (_isLoaded)
        {
            return;
        }

        ResourceNodes.Clear();
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

                    // DS
                    // here we can consider a setting if empty items should be shown
                    // but for now - just hide it
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
            ResourceNodes.Add(new ResourceNode("-No items-", ResourceNodeType.Empty));
        }
    }
}
