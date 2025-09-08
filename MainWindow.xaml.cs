using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FanucWpf.ViewModels;

namespace FanucWpf
{
    /// <summary>
    /// MainWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // ViewModel'den LogEntries koleksiyonunu dinle
            var viewModel = (ViewModels.MainViewModel)DataContext;
            viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }

        private void LogEntries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Yeni öğeler eklendiğinde
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                // UI thread'de çalıştığımızdan emin olalım
                Dispatcher.InvokeAsync(() =>
                {
                    // Log listesinin en alt kısmına kaydır
                    if (LogListView.Items.Count > 0)
                    {
                        LogListView.ScrollIntoView(LogListView.Items[0]);
                        
                        // Scroll bar'ı görünür tutmak için ListView'e odaklan
                        LogListView.Focus();
                    }
                }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }
    }
}
