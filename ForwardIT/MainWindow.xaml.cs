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

namespace ForwardIT
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Logger eventLogger { get; set; }
        private TelegramViewModel TelegramViewModel { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            eventLogger = new Logger();
            TelegramViewModel = new TelegramViewModel();
            this.DataContext = TelegramViewModel;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (phoneBox.Text.Length > 0)
            {
                if (phoneBox.Text[0] != '+')
                {
                    phoneBox.Text = "+" + phoneBox.Text;
                }
            }
            else
            {
                phoneBox.Text = "+";
            }
            phoneBox.CaretIndex = phoneBox.Text.Length;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TelegramViewModel.EventRaised += (o, s) =>
            {
                MessageBox.Show(((MessageEventArgs)s).Message, "Внимание!", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            TelegramViewModel.OnErrorReceived += async (o, s) =>
            {
                await eventLogger.LogExceptionAsync(s.ExceptionObject as Exception);
            };
        }
    }
}
