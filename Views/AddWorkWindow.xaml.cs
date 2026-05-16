using MissionTime.Services;
using System.Data;
using System.Windows;

namespace MissionTime.Views
{
    public partial class AddWorkWindow : Window
    {
        public long SelectedWorkId { get; private set; }
        public string SelectedWorkName { get; private set; }
        // SelectedProgramId теперь не нужен как выбор, он фиксирован из главного окна

        public AddWorkWindow(DbService db, string employeeName, string programName, long? currentWorkId = null)
        {
            InitializeComponent();
            lblEmployee.Text = $"Сотрудник: {employeeName}";
            lblProgram.Text = $"Программа: {programName}"; // Показываем текущую программу

            // Загружаем только виды работ
            cbWorkType.ItemsSource = db.ListOfWork_List().DefaultView;

            if (currentWorkId.HasValue)
            {
                this.Title = "Изменение работы";
                btnOk.Content = "Изменить";
                cbWorkType.SelectedValue = currentWorkId.Value;
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (cbWorkType.SelectedValue == null)
            {
                MissionMessageBox.Show(this, "Внимание", "Выберите вид работы!");
                return;
            }

            SelectedWorkId = (long)cbWorkType.SelectedValue;
            SelectedWorkName = ((DataRowView)cbWorkType.SelectedItem)["Name"].ToString();

            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}