using System.Windows;
using System.Media;

namespace MissionTime.Views
{
    public partial class MissionMessageBox : Window
    {
        public enum MessageBoxResult { Ok, Yes, No }
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Ok;

        public MissionMessageBox(string title, string message, bool isQuestion = false)
        {
            InitializeComponent();
            txtTitle.Text = title;
            txtMessage.Text = message;

            if (isQuestion)
            {
                btnOk.Content = "Да";
                btnCancel.Visibility = Visibility.Visible;
                // Звук вопроса (стандартный "тыдым")
                SystemSounds.Question.Play();
            }
            else
            {
                // Звук восклицания или ошибки для обычных алертов
                SystemSounds.Exclamation.Play();
            }
        }

        // Статический метод для вызова
        public static bool? Show(Window owner, string title, string message, bool isQuestion = false)
        {
            var msg = new MissionMessageBox(title, message, isQuestion);
            msg.Owner = owner ?? Application.Current.MainWindow;
            return msg.ShowDialog();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}