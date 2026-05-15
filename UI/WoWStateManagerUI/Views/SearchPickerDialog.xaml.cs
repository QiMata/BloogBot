using System.Windows;
using WoWStateManagerUI.Models;
using WoWStateManagerUI.Services;
using WoWStateManagerUI.ViewModels;

namespace WoWStateManagerUI.Views
{
    public partial class SearchPickerDialog : Window
    {
        public SearchPickerViewModel ViewModel { get; }
        public WorldSearchResult? PickedResult { get; private set; }

        public SearchPickerDialog(SearchKind kind, string initialQuery)
        {
            InitializeComponent();
            ViewModel = new SearchPickerViewModel(kind, initialQuery);
            DataContext = ViewModel;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedResult == null) return;
            PickedResult = ViewModel.SelectedResult;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnResultsDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel.SelectedResult == null) return;
            PickedResult = ViewModel.SelectedResult;
            DialogResult = true;
            Close();
        }
    }
}
