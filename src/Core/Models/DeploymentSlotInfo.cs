using Cats.Telescope.VsExtension.Core.Enums;

namespace Cats.Telescope.VsExtension.Core.Models;

/// <summary>
/// Presents a tree node for an App Service deployment slot
/// </summary>
internal class DeploymentSlotInfo : ResourceNode
{
    public DeploymentSlotInfo(string id)
        : base(id, ResourceNodeType.DeploymentSlot)
    {

    }
}
