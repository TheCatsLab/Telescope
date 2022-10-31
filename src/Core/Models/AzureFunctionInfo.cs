using Cats.Telescope.VsExtension.Core.Enums;

namespace Cats.Telescope.VsExtension.Core.Models;

internal class WebAppInfo : ResourceNode
{
    public WebAppInfo(string id, ResourceNodeType resourceType)
        : base(id, resourceType, isAutoExpanded: true)
    {

    }
}
