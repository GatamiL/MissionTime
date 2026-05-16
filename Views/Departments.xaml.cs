using MissionTime.Services;
using MissionTime.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MissionTime.Views
{
    public partial class Departments : Window
    {
        public Departments(DbService db)
        {
            InitializeComponent();

            // Привязываем логику дерева и передаем сервис базы
            this.DataContext = new DepartmentsViewModel(db);
        }

        // Обработчик для кнопки закрытия
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void tvDepartments_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Находим нашу ViewModel
            if (this.DataContext is DepartmentsViewModel vm)
            {
                // Передаем выбранный элемент (Department) в свойство SelectedDepartment
                vm.SelectedDepartment = e.NewValue as MissionTime.Models.Department;

                // Маленький хак: заставляем кнопки перепроверить свое состояние (активны или нет)
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}