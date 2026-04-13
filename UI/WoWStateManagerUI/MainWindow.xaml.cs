using WoWStateManagerUI.ViewModels;
using System.Windows;

namespace WoWStateManagerUI
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            InitializeComponent();
            Closed += (_, _) => _viewModel.Dispose();
        }
    }
}
