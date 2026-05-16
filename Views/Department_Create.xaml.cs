using System;
using System.Windows;
using MissionTime.Services;
using MissionTime.ViewModels;
using System.Collections.Generic;
using MissionTime.Models;

namespace MissionTime.Views
{
    public partial class Department_Create : Window
    {
        private readonly DbService _db;

        // Конструктор принимает базу и список текущих отделов для комбобокса
        public Department_Create(DbService db, List<Department> allCurrentDeps)
        {
            InitializeComponent();
            _db = db;

            // Инициализируем ViewModel
            this.DataContext = new DepartmentCreateViewModel(db, allCurrentDeps);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var vm = (DepartmentCreateViewModel)this.DataContext;

            // Валидация
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                MissionMessageBox.Show(this, "Внимание", "Введите название подразделения.");
                return;
            }

            // Рассчитываем иерархию
            // Если выбран ID -1 (наш фиктивный корень), то ParentId = null, Level = 0
            long? parentId = vm.SelectedParent.Id == -1 ? (long?)null : vm.SelectedParent.Id;
            int level = vm.SelectedParent.Id == -1 ? 0 : vm.SelectedParent.Level + 1;

            // Ответственный (если чекбокс нажат)
            long? respId = vm.HasResponsible && vm.SelectedEmployee != null
                ? (long?)vm.SelectedEmployee.Id
                : (long?)null;

            try
            {
                // Вызываем твой мощный метод из сервиса
                _db.Department_Create(vm.Name, vm.ShortName, parentId, respId, level);

                // Закрываем окно с успехом
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка сохранения", ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}