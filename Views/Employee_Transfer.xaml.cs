using MissionTime.Models;
using MissionTime.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;

namespace MissionTime.Views
{
    public partial class Employee_Transfer : Window
    {
        private readonly DbService _db;
        private readonly EmployeeBrief _employee;

        public Employee_Transfer(DbService db, EmployeeBrief employee)
        {
            InitializeComponent();
            _db = db;
            _employee = employee;

            lblEmployeeName.Text = _employee.Fio;
            dpStartDate.SelectedDate = DateTime.Today;
            runCurrentDep.Text = _employee.DepartmentName; // Текст в подсказку над деревом

            LoadData();
            SetCurrentData();
        }

        private void LoadData()
        {
            // 1. Грузим должности
            cbPositions.ItemsSource = _db.Positions_List().DefaultView;

            // 2. Грузим дерево отделов
            var dt = _db.Departments_List();
            var allDepsList = dt.AsEnumerable().Select(r => new Department
            {
                Id = r.Field<long>("Id"),
                ParentId = r.Field<long?>("ParentId"),
                Name = r.Field<string>("Name")
            }).ToList();

            var root = allDepsList.Where(d => d.ParentId == null).ToList();
            foreach (var r in root) FillChildren(r, allDepsList);

            tvDepartments.ItemsSource = root;
        }

        private void SetCurrentData()
        {
            // 1. Авто-выбор должности в ComboBox
            foreach (DataRowView item in cbPositions.Items)
            {
                if (item["Name"].ToString() == _employee.PositionName)
                {
                    cbPositions.SelectedItem = item;
                    break;
                }
            }

            // 2. Авто-выделение отдела в TreeView
            if (tvDepartments.ItemsSource is List<Department> rootDeps)
            {
                var current = FindDepartment(rootDeps, _employee.DepartmentName);
                if (current != null)
                {
                    current.IsSelected = true; // Подсвечиваем синим (нужен Style в XAML!)
                }
            }
        }

        // Рекурсивный поиск отдела
        private Department FindDepartment(IEnumerable<Department> deps, string name)
        {
            foreach (var d in deps)
            {
                if (d.Name == name) return d;
                var found = FindDepartment(d.Children, name);
                if (found != null) return found;
            }
            return null;
        }

        private void FillChildren(Department parent, List<Department> all)
        {
            var children = all.Where(d => d.ParentId == parent.Id).ToList();
            foreach (var child in children)
            {
                parent.Children.Add(child);
                FillChildren(child, all);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cbPositions.SelectedValue == null)
            {
                MissionMessageBox.Show(this, "Ошибка", "Выберите новую должность.");
                return;
            }
            // ВАЖНО: так как мы используем IsSelected в модели, TreeView.SelectedItem может 
            // не сразу обновиться. Лучше найти тот, у которого IsSelected == true

            var selectedDep = FindSelectedDepartment(tvDepartments.ItemsSource as List<Department>);

            if (selectedDep == null)
            {
                MissionMessageBox.Show(this, "Ошибка", "Выберите подразделение.");
                return;
            }

            try
            {
                long posId = (long)cbPositions.SelectedValue;
                long depId = selectedDep.Id;
                string sDate = dpStartDate.SelectedDate.Value.ToString("yyyy-MM-dd");
                string note = txtNote.Text.Trim();

                _db.Employee_Transfer(_employee.Id, depId, posId, note, sDate);
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", ex.Message);
            }
        }

        private Department FindSelectedDepartment(IEnumerable<Department> deps)
        {
            if (deps == null) return null;
            foreach (var d in deps)
            {
                if (d.IsSelected) return d;
                var found = FindSelectedDepartment(d.Children);
                if (found != null) return found;
            }
            return null;
        }
    }
}