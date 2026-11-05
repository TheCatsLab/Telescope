namespace Cats.Telescope.VsExtension.Core.Models;

class FilterMatchViewModel : ViewModelBase
{
    private int _byNameCount;
    private int _byDataCount;
    private int _byTagsCount;
    private int _byEnvironmentVariablesCount;

    public int ByNameCount
    {
        get => _byNameCount;
        set
        {
            _byNameCount = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Matches));
            RaisePropertyChanged(nameof(TotalCount));
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
            RaisePropertyChanged(nameof(TotalCount));
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
            RaisePropertyChanged(nameof(TotalCount));
        }
    }

    public int ByEnvironmentVariablesCount
    {
        get => _byEnvironmentVariablesCount;
        set
        {
            _byEnvironmentVariablesCount = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Matches));
            RaisePropertyChanged(nameof(TotalCount));
        }
    }

    /// <summary>
    /// Indicates if the node matches the filter at all
    /// </summary>
    public bool Matches => ByTagsCount > 0 || ByDataCount > 0 || ByNameCount > 0 || ByEnvironmentVariablesCount > 0;

    /// <summary>
    /// Total number of the filter findings across all the sources
    /// </summary>
    public int TotalCount => ByNameCount + ByDataCount + ByTagsCount + ByEnvironmentVariablesCount;

    /// <summary>
    /// Extracts the filter findings for displaying
    /// </summary>
    /// <param name="filterResult"></param>
    public void Apply(NodeFilterResult? filterResult)
    {
        if (filterResult is null)
        {
            ByNameCount = ByDataCount = ByTagsCount = ByEnvironmentVariablesCount = 0;
        }
        else
        {
            // the result keeps the per-source totals, so no LINQ pass over the matches is needed
            NodeFilterResult result = filterResult.Value;

            ByNameCount = result.NameCount;
            ByDataCount = result.DataCount;
            ByTagsCount = result.TagsCount;
            ByEnvironmentVariablesCount = result.EnvironmentVariablesCount;
        }
    }
}
