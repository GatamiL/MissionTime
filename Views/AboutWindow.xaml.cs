using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace MissionTime.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch
            {
                // Игнорируем ошибки при открытии браузера
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
