using Cats.Telescope.VsExtension.Core.Enums;

namespace Cats.Telescope.VsExtension.Core.Models;

/// <summary>
/// Presents a tree node for an Azure Logic App
/// </summary>
internal class AzureLogicAppInfo : ResourceNode
{
    public AzureLogicAppInfo(string id) 
        : base(id, ResourceNodeType.LogicApp, isAutoExpanded: true)
    {

    }
}
