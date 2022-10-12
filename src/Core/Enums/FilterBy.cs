using System;

namespace Cats.Telescope.VsExtension.Core.Enums;

[Flags]
internal enum FilterBy
{
    ResourceName = 1,
    ResourceData = 2,
    ResourceTags = 4
}
