using Cats.Telescope.VsExtension.Core.Enums;
using System.Linq;

namespace Cats.Telescope.VsExtension.Core.Models;

class FilterMatchViewModel : ViewModelBase
{
    private int _byNameCount;
    private int _byDataCount;
    private int _byTagsCount;

    public int ByNameCount
    {
        get => _byNameCount;
        set
        {
            _byNameCount = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Matches));
        }
    }

    public int ByDataCount
    {
        get => _byDataCount;
        set
        {
            _byDataCount = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Matches));
        }
    }

    public int ByTagsCount
    {
        get => _byTagsCount;
        set
        {
            _byTagsCount = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Matches));
        }
    }

    /// <summary>
    /// Indicates if the node matches the filter at all
    /// </summary>
    public bool Matches => ByTagsCount > 0 || ByDataCount > 0 || ByNameCount > 0;

    /// <summary>
    /// Extracts the filter findings for displaying
    /// </summary>
    /// <param name="filterResult"></param>
    public void Apply(NodeFilterResult? filterResult)
    {
        if (filterResult is null)
        {
            ByNameCount = ByDataCount = ByTagsCount = 0;
        }
        else
        {
            ByNameCount = filterResult.Value.Matches.FirstOrDefault(m => m.By == FilterBy.ResourceName).Count;
            ByDataCount = filterResult.Value.Matches.FirstOrDefault(m => m.By == FilterBy.ResourceData).Count;
            ByTagsCount = filterResult.Value.Matches.FirstOrDefault(m => m.By == FilterBy.ResourceTags).Count;
        }
    }
}
