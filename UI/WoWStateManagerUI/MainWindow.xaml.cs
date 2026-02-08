using WoWStateManagerUI.Views;
using System.Windows;

namespace WoWStateManagerUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly StateManagerViewModel _stateManagerViewModel;
        public MainWindow()
        {
            _stateManagerViewModel = new StateManagerViewModel();
            DataContext = _stateManagerViewModel;
        }
    }
}