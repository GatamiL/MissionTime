using System.Windows;
using System.Windows.Input;

namespace MissionTime.Views
{
    public partial class EmployeePosition_Edit : Window
    {
        public string PositionName { get; set; }
        public string WindowTitle { get; set; }

        public EmployeePosition_Edit(string initialName = "")
        {
            InitializeComponent();
            this.DataContext = this;
            PositionName = initialName;
            WindowTitle = string.IsNullOrEmpty(initialName) ? "Новая должность" : "Редактирование";

            txtName.Focus();
            if (!string.IsNullOrEmpty(PositionName)) txtName.SelectAll();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PositionName))
            {
                MissionMessageBox.Show(this, "Внимание", "Название не может быть пустым.");
                return;
            }
            this.DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;

        private void txtName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnSave_Click(null, null);
        }
    }
}