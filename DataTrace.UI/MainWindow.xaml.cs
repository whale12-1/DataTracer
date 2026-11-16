using DataTrace.UI.ViewModels;
using System.Windows;

namespace DataTrace.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 1. Создаем ViewModel и устанавливаем DataContext
            var viewModel = new MainViewModel();
            this.DataContext = viewModel;

            // 2. Загружаем данные при старте окна
            Loaded += async (s, e) => await viewModel.LoadDataAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.LoadDataAsync();
        }
    }
}