using MissionTime.Models;
using MissionTime.Services;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;

namespace MissionTime.ViewModels
{
    public class EmployeesViewModel : ObservableObject
    {
        private readonly DbService _db;
        public ObservableCollection<EmployeeBrief> Employees { get; set; } = new ObservableCollection<EmployeeBrief>();

        private bool _showFired;
        public bool ShowFired
        {
            get => _showFired;
            set { _showFired = value; OnPropertyChanged(); LoadData(); }
        }

        private EmployeeBrief _selectedEmployee;
        public EmployeeBrief SelectedEmployee
        {
            get => _selectedEmployee;
            set { _selectedEmployee = value; OnPropertyChanged(); }
        }

        public EmployeesViewModel(DbService db)
        {
            _db = db;
            LoadData();
        }

        public void LoadData()
        {
            Employees.Clear();

            // Используем метод из сервиса!
            DataTable dt = _db.Employees_List_Full(ShowFired);

            foreach (DataRow row in dt.Rows)
            {
                Employees.Add(new EmployeeBrief
                {
                    Id = Convert.ToInt64(row["Id"]),
                    Fio = row["Fio"].ToString(),
                    PositionName = row["PositionName"].ToString(),
                    DepartmentName = row["DepartmentName"].ToString(),
                    IsFired = Convert.ToInt32(row["Action"]) == 3
                });
            }
        }
    }
}