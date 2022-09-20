using System.Windows;
using System.Windows.Controls;

namespace Cats.Telescope.VsExtension.Core.Controls;

internal class SearchControl : ComboBox
{
	static SearchControl()
	{
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SearchControl),
            new FrameworkPropertyMetadata(typeof(SearchControl)));
    }


}
