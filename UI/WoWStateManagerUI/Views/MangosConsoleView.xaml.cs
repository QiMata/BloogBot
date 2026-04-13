using System.Collections.Specialized;
using System.Windows.Controls;

namespace WoWStateManagerUI.Views
{
    public partial class MangosConsoleView : UserControl
    {
        public MangosConsoleView()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                // Auto-scroll output log to bottom when new items are added
                if (OutputListBox.ItemsSource is INotifyCollectionChanged collection)
                {
                    collection.CollectionChanged += (_, _) =>
                    {
                        if (OutputListBox.Items.Count > 0)
                            OutputListBox.ScrollIntoView(OutputListBox.Items[^1]);
                    };
                }
            };
        }
    }
}
