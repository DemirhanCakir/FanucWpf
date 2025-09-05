using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using FanucWpf.Commands;

namespace FanucWpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FanucInterface _robotService;
        private readonly Dispatcher _dispatcher; // UI thread'e erişim için

        private string _ipAddress = "127.0.0.1"; // Varsayılan IP
        private int _port = 60008; // Dokümandaki örnek port
        private bool _isConnected;
        private string _messageToSend = "STATUS?";
        private string _statusText = "Ready";

        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); }
        }

        public int Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(nameof(Port)); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); }
        }

        public string MessageToSend
        {
            get => _messageToSend;
            set { _messageToSend = value; OnPropertyChanged(nameof(MessageToSend)); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public ObservableCollection<string> LogEntries { get; } = new ObservableCollection<string>();

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand SendMessageCommand { get; }
        public ICommand GetRegisterCommand { get; }
        public ICommand SetRegisterCommand { get; }
        public ICommand GetPositionRegister { get; }

        public MainViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _robotService = new FanucInterface();

            _robotService.LogMessage += (msg) => Log(msg);
            _robotService.MessageReceived += (msg) => Log($"[ROBOT] -> {msg}");
            _robotService.ConnectionStateChanged += (state) => IsConnected = state;

            ConnectCommand = new RelayCommand(async (_) => await ConnectAsync(), (_) => !IsConnected);
            DisconnectCommand = new RelayCommand(async (_) => await DisconnectAsync(), (_) => IsConnected);
            SendMessageCommand = new RelayCommand(async (_) => await SendMessageAsync(), (_) => IsConnected);
            GetRegisterCommand = new RelayCommand(async (_) => await GetRegisterAsync(), (_) => IsConnected);
            SetRegisterCommand = new RelayCommand(async (_) => await SetRegisterAsync(), (_) => IsConnected);
            GetPositionRegister = new RelayCommand(async (_) => await GetPositionAsync(), (_) => IsConnected);
        }

        private async Task ConnectAsync()
        {
            await _robotService.ConnectAsync(IpAddress, Port);
        }

        private async Task DisconnectAsync()
        {
            await _robotService.DisconnectAsync();
        }

        private async Task SendMessageAsync()
        {
            if (!string.IsNullOrWhiteSpace(MessageToSend))
            {
                await _robotService.SendMessageAsync(MessageToSend);
            }
        }
        private async Task SetRegisterAsync()
        {
            await _robotService.SetRegisterValue(24, 123);
        }
        private async Task GetRegisterAsync()
        {
            await _robotService.GetRegisterValue(24);
        }
        private async Task GetPositionAsync()
        {
            await _robotService.GetPositionRegister(1);
        }

        private void Log(string message)
        {
            // Arka plan thread'lerinden gelen logları UI thread'e güvenli bir şekilde aktar
            _dispatcher.Invoke(() =>
            {
                StatusText = message;
                LogEntries.Insert(0, $"{System.DateTime.Now:HH:mm:ss} - {message}");
                if (LogEntries.Count > 200) // Log listesini çok büyütme
                {
                    LogEntries.RemoveAt(LogEntries.Count - 1);
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // Komutların CanExecute durumunu yeniden değerlendir
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
