using MissionTime.Services;
using MissionTime.ViewModels;
using System.Windows;

namespace MissionTime.Views
{
    public partial class Employees : Window
    {
        private readonly DbService _db;
        private readonly EmployeesViewModel _vm;

        public Employees(DbService db)
        {
            InitializeComponent();
            _db = db;
            _vm = new EmployeesViewModel(db);
            this.DataContext = _vm;
        }

        // 1. Добавить нового (окно уже создали)
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var win = new Employee_Create(_db);
            win.Owner = this;
            win.ShowInTaskbar = false;
            if (win.ShowDialog() == true) _vm.LoadData();
        }

        // 2. Изменить ФИО (без истории, просто правим таблицу Employees)
        private void btnEditFio_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, выбрано ли что-то в DataGrid через ViewModel
            if (_vm.SelectedEmployee == null)
            {
                MissionMessageBox.Show(this, "Внимание", "Выберите сотрудника из списка.");
                return;
            }

            // Открываем окно и передаем сервис + выбранную модель
            var win = new Employee_EditFio(_db, _vm.SelectedEmployee);
            win.Owner = this;
            win.ShowInTaskbar = false;

            if (win.ShowDialog() == true)
            {
                // Если нажали "Сохранить", просто обновляем список
                _vm.LoadData();
            }
        }

        // 3. Перевести (Новая запись в историю)
        private void btnTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedEmployee == null) return;

            // Если сотрудник уволен, переводить его странно, да?
            if (_vm.SelectedEmployee.IsFired)
            {
                MissionMessageBox.Show(this, "Внимание", "Нельзя перевести уволенного сотрудника.");
                return;
            }

            var win = new Employee_Transfer(_db, _vm.SelectedEmployee);
            win.Owner = this;
            win.ShowInTaskbar = false;
            if (win.ShowDialog() == true) _vm.LoadData();
        }

        // 4. Уволить (Добавляем запись в историю с Action=3)
        private void btnFire_Click(object sender, RoutedEventArgs e)
        {
            var emp = _vm.SelectedEmployee;
            if (emp == null || emp.IsFired) return;

            string msg = $"Вы уверены, что хотите уволить сотрудника:\n{emp.Fio}?";
            if (MissionMessageBox.Show(this, "Подтверждение", msg, true) == true)
            {
                try
                {
                    _db.Employee_Fire(emp.Id);
                    _vm.LoadData();
                }
                catch (System.Exception ex)
                {
                    MissionMessageBox.Show(this, "Ошибка", ex.Message);
                }
            }
        }

        // 5. История (Просто просмотр записей из EmployeePositionsHistory)
        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedEmployee == null) return;

            var win = new Employee_History(_db, _vm.SelectedEmployee);
            win.Owner = this;
            win.ShowInTaskbar = false;
            win.ShowDialog();
        }
    }
}