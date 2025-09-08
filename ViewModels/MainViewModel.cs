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
        private bool _isConnected;
        private string _messageToSend = "STATUS?";
        private string _statusText = "Ready";
        private string message;

        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); }
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
            _robotService = new FanucInterface(IpAddress);

            //_robotService.LogMessage += (msg) => Log(msg);
            //_robotService.MessageReceived += (msg) => Log($"[ROBOT] -> {msg}");
            //_robotService.ConnectionStateChanged += (state) => IsConnected = state;

            ConnectCommand = new RelayCommand(async (_) => Connect(), (_) => !IsConnected);
            DisconnectCommand = new RelayCommand(async (_) => Disconnect(), (_) => IsConnected);
            //SendMessageCommand = new RelayCommand(async (_) => await SendMessageAsync(), (_) => IsConnected);
            GetRegisterCommand = new RelayCommand(async (_) => RefreshData(), (_) => IsConnected);
            SetRegisterCommand = new RelayCommand(async (_) => SetRegisterAsync(), (_) => IsConnected);
            //GetPositionRegister = new RelayCommand(async (_) => await GetPositionAsync(), (_) => IsConnected);
        }

        private void Connect()
        {
            try
            {
                _robotService.InitAndConnect();
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Log($"Connection error: {ex.Message}");
            }
        }

        private void Disconnect()
        {
            try
            {
                _robotService.Disconnect();
                IsConnected = false;
            }
            catch (Exception ex)
            {
                Log($"Disconnection error: {ex.Message}");
            }
        }

        private void RefreshData()
        {
            String Message = _robotService.RefreshData();
            Log(Message);
        }
        private void SetRegisterAsync()
        {
            _robotService.SetNumRegs();
        }
        
        

        private void Log(string message)
        {
            // Arka plan thread'lerinden gelen logları UI thread'e güvenli bir şekilde aktar
            _dispatcher.Invoke(() =>
            {
                StatusText = message;
                LogEntries.Insert(0, $"{System.DateTime.Now:HH:mm:ss} - {message}");
                //if (LogEntries.Count > 200) // Log listesini çok büyütme
                //{
                //    LogEntries.RemoveAt(LogEntries.Count - 1);
                //}
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
