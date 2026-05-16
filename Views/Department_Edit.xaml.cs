using System;
using System.Windows;
using MissionTime.Models;
using MissionTime.Services;
using MissionTime.ViewModels;

namespace MissionTime.Views
{
    public partial class Department_Edit : Window
    {
        private readonly DbService _db;
        private readonly long _deptId;

        public Department_Edit(DbService db, Department dept)
        {
            InitializeComponent();
            _db = db;
            _deptId = dept.Id;

            // Используем ту же ViewModel, что и для создания
            // Но передаем пустой список родителей, так как тут мы их не меняем
            var vm = new DepartmentCreateViewModel(db, new System.Collections.Generic.List<Department>());

            // Заполняем данными из базы
            vm.Name = dept.Name;
            vm.ShortName = dept.ShortName;
            vm.HasResponsible = dept.ResponsibleId.HasValue;

            // Ищем сотрудника в списке загруженных
            if (dept.ResponsibleId.HasValue)
            {
                foreach (var emp in vm.Employees)
                {
                    if (emp.Id == dept.ResponsibleId)
                    {
                        vm.SelectedEmployee = emp;
                        break;
                    }
                }
            }

            this.DataContext = vm;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var vm = (DepartmentCreateViewModel)this.DataContext;

            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                MissionMessageBox.Show(this, "Внимание", "Название не может быть пустым.");
                return;
            }

            long? respId = vm.HasResponsible && vm.SelectedEmployee != null
                ? (long?)vm.SelectedEmployee.Id
                : (long?)null;

            try
            {
                _db.Department_Update(_deptId, vm.Name, vm.ShortName, respId);
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;
    }
}