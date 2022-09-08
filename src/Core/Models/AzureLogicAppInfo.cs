using Cats.Telescope.VsExtension.Core.Enums;

namespace Cats.Telescope.VsExtension.Core.Models;

internal class AzureLogicAppInfo : ResourceNode
{
    public AzureLogicAppInfo(string id) 
        : base(id, ResourceNodeType.LogicApp)
    {

    }

}
