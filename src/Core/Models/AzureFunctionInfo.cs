using Cats.Telescope.VsExtension.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.Core.Models;

internal class WebAppInfo : ResourceNode
{
    public WebAppInfo(string id, ResourceNodeType resourceType, Func<Task<IEnumerable<ResourceNode>>> loadMoreAction = null)
        : base(id, resourceType, loadMoreAction, isAutoExpanded: true)
    {

    }
}
