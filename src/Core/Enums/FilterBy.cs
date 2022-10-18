using System;

namespace Cats.Telescope.VsExtension.Core.Enums;

[Flags]
internal enum FilterBy
{
    ResourceName = 1,
    ResourceData = 2,
    ResourceTagKeys = 4,
    ResourceTagValues = 8
}
