using MissionTime.Models;
using MissionTime.Services;
using MissionTime.ViewModels; // Чтобы видеть EmployeeBrief или твою модель
using System;
using System.Data.SQLite;
using System.Windows;

namespace MissionTime.Views
{
    public partial class Employee_EditFio : Window
    {
        private readonly DbService _db;
        private readonly long _employeeId;

        public Employee_EditFio(DbService db, EmployeeBrief employee)
        {
            InitializeComponent();
            _db = db;
            _employeeId = employee.Id;

            // Сразу подставляем старое имя в текстовое поле
            txtFio.Text = employee.Fio;
            txtFio.Focus();
            txtFio.SelectAll();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string newFio = txtFio.Text.Trim();

            if (string.IsNullOrWhiteSpace(newFio))
            {
                MissionMessageBox.Show(this, "Внимание", "ФИО не может быть пустым.");
                return;
            }

            try
            {
                _db.Execute("UPDATE Employees SET Fio = @fio WHERE Id = @id",
                    new SQLiteParameter("@fio", newFio),
                    new SQLiteParameter("@id", _employeeId));

                LogService.Log($"ИЗМЕНЕНО ФИО СОТРУДНИКА (ID: {_employeeId}): Новое значение '{newFio}'");

                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", "Не удалось обновить ФИО: " + ex.Message);
            }
        }
    }
}