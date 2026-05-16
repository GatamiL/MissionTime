using MissionTime.Services;
using MissionTime.Models;
using System;
using System.Data;
using System.Linq;
using System.Windows;

namespace MissionTime.Views
{
    public partial class Employee_Create : Window
    {
        private readonly DbService _db;

        public Employee_Create(DbService db)
        {
            InitializeComponent();
            _db = db;
            LoadData();

            // Ставим дату по умолчанию - сегодня
            dpStartDate.SelectedDate = DateTime.Today;
            txtFio.Focus();
        }

        private void LoadData()
        {
            // 1. Грузим должности
            cbPositions.ItemsSource = _db.Positions_List().DefaultView;

            // 2. Грузим дерево отделов (используем твой метод из DbService)
            var dt = _db.Departments_List();
            var allDeps = dt.AsEnumerable().Select(r => new Department
            {
                Id = r.Field<long>("Id"),
                ParentId = r.Field<long?>("ParentId"),
                Name = r.Field<string>("Name")
            }).ToList();

            var root = allDeps.Where(d => d.ParentId == null).ToList();
            foreach (var r in root) FillChildren(r, allDeps);
            tvDepartments.ItemsSource = root;
        }

        private void FillChildren(Department parent, System.Collections.Generic.List<Department> all)
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
            // Валидация
            if (string.IsNullOrWhiteSpace(txtFio.Text))
            {
                MissionMessageBox.Show(this, "Ошибка", "Введите ФИО сотрудника");
                return;
            }
            if (cbPositions.SelectedValue == null)
            {
                MissionMessageBox.Show(this, "Ошибка", "Выберите должность");
                return;
            }
            if (tvDepartments.SelectedItem == null)
            {
                MissionMessageBox.Show(this, "Ошибка", "Выберите подразделение в дереве");
                return;
            }

            try
            {
                string fio = txtFio.Text.Trim();
                long posId = (long)cbPositions.SelectedValue;
                long depId = ((Department)tvDepartments.SelectedItem).Id;
                string sDate = dpStartDate.SelectedDate.Value.ToString("yyyy-MM-dd");

                // Вызываем метод из DbService (мы его обсуждали ранее)
                _db.InTransaction((conn, tx) => {
                    // Создаем сотрудника
                    var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO Employees (Fio) VALUES (@f); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@f", fio);
                    long empId = Convert.ToInt64(cmd.ExecuteScalar());

                    // Создаем историю (Action = 1 - Прием)
                    cmd.CommandText = "INSERT INTO EmployeePositionsHistory (EmployeeId, DepartmentId, PositionId, StartDate, Action) " +
                                     "VALUES (@eid, @did, @pid, @date, 1)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@eid", empId);
                    cmd.Parameters.AddWithValue("@did", depId);
                    cmd.Parameters.AddWithValue("@pid", posId);
                    cmd.Parameters.AddWithValue("@date", sDate);
                    cmd.ExecuteNonQuery();
                });

                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка базы данных", ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;
    }
}