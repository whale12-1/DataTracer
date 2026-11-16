using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using DataTrace.Core.Processing;
using DataTrace.Core.Rules;

namespace DataTrace.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private string _currentView = "Dashboard";
        private string _searchText = "";

        // --- Свойства для навигации ---
        public string CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        // --- Свойства для поиска ---
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                AlertsView.Refresh(); // Обновляем фильтр при вводе
            }
        }

        // --- Данные ---
        public ObservableCollection<SecurityAlert> Alerts { get; set; } = new();
        public ObservableCollection<ProcessNode> ProcessTree { get; set; } = new();

        // Для фильтрации DataGrid
        public ICollectionView AlertsView { get; set; }

        // --- Статистика для Дашборда ---
        public int TotalAlerts => Alerts.Count;
        public int CriticalAlerts => Alerts.Count(a => a.Severity == "Critical");
        public int TotalProcesses => ProcessTree.Count;

        public MainViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };

            // Настраиваем фильтр для DataGrid
            AlertsView = CollectionViewSource.GetDefaultView(Alerts);
            AlertsView.Filter = FilterAlerts;
        }

        private bool FilterAlerts(object obj)
        {
            if (obj is SecurityAlert alert && !string.IsNullOrWhiteSpace(SearchText))
            {
                return alert.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       alert.ProcessName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       alert.RemoteDomain.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        public async Task LoadDataAsync()
        {
            try
            {
                var tree = await _httpClient.GetFromJsonAsync<ProcessNode[]>("api/processes/tree");
                if (tree != null)
                {
                    ProcessTree.Clear();
                    foreach (var node in tree) ProcessTree.Add(node);
                }

                var alerts = await _httpClient.GetFromJsonAsync<SecurityAlert[]>("api/alerts");
                if (alerts != null)
                {
                    Alerts.Clear();
                    foreach (var alert in alerts) Alerts.Add(alert);
                }

                // Обновляем UI для карточек статистики
                OnPropertyChanged(nameof(TotalAlerts));
                OnPropertyChanged(nameof(CriticalAlerts));
                OnPropertyChanged(nameof(TotalProcesses));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка API: {ex.Message}\nЗапущен ли DataTrace.Service?");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}