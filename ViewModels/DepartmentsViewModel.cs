using MissionTime.Models;
using MissionTime.Services;
using MissionTime.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;

public class DepartmentsViewModel : ObservableObject
{
    private readonly DbService _db;

    // Список для дерева
    public ObservableCollection<Department> RootDepartments { get; set; } = new ObservableCollection<Department>();

    // Выбранный отдел в дереве
    private Department _selectedDepartment;
    public Department SelectedDepartment
    {
        get => _selectedDepartment;
        set { _selectedDepartment = value; OnPropertyChanged(); }
    }

    // Команды для кнопок
    public ICommand CreateCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

    public DepartmentsViewModel(DbService db)
    {
        _db = db;

        // Инициализация команд
        CreateCommand = new RelayCommand(p => ExecuteCreate());
        EditCommand = new RelayCommand(p => ExecuteEdit(), p => SelectedDepartment != null);
        DeleteCommand = new RelayCommand(p => ExecuteDelete(), p => SelectedDepartment != null);

        LoadData();
    }

    private void LoadData()
    {
        RootDepartments.Clear();
        var dt = _db.Departments_List();
        var allDeps = new List<Department>();

        foreach (DataRow row in dt.Rows)
        {
            allDeps.Add(new Department
            {
                Id = Convert.ToInt64(row["Id"]),
                ParentId = row["ParentId"] == DBNull.Value ? (long?)null : Convert.ToInt64(row["ParentId"]),
                Name = row["Name"].ToString(),
                Level = Convert.ToInt32(row["Level"]),
                ShortName = row["ShortName"]?.ToString(),
                ResponsibleId = row["ResponsibleId"] == DBNull.Value ? (long?)null : Convert.ToInt64(row["ResponsibleId"]),
                ResponsibleFio = row["ResponsibleFio"]?.ToString()
            });
        }

        if (allDeps.Count == 0) return;

        var lookup = allDeps.ToDictionary(d => d.Id);
        foreach (var dep in allDeps)
        {
            if (dep.ParentId.HasValue && lookup.ContainsKey(dep.ParentId.Value))
            {
                lookup[dep.ParentId.Value].Children.Add(dep);
            }
            else
            {
                RootDepartments.Add(dep);
            }
        }
    }

    private void ExecuteCreate()
    {
        var allDeps = RootDepartments.SelectMany(GetFlatList).ToList();
        var createWin = new MissionTime.Views.Department_Create(_db, allDeps);

        // Ищем окно "Подразделения" среди открытых
        createWin.Owner = Application.Current.Windows
            .Cast<Window>()
            .FirstOrDefault(w => w is MissionTime.Views.Departments);

        // Скрываем из панели задач
        createWin.ShowInTaskbar = false;

        if (createWin.ShowDialog() == true)
        {
            LoadData();
        }
    }

    private void ExecuteEdit()
    {
        if (SelectedDepartment == null) return;

        var editWin = new Department_Edit(_db, SelectedDepartment);

        // Находим родительское окно
        editWin.Owner = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w is Departments);

        // Скрываем вторую иконку в панели задач
        editWin.ShowInTaskbar = false;

        if (editWin.ShowDialog() == true)
        {
            LoadData();
        }
    }

    private void ExecuteDelete()
    {
        if (SelectedDepartment == null) return;

        // Если есть дети — запрещаем удаление (чтобы не оставить сирот)
        if (SelectedDepartment.Children.Count > 0)
        {
            MissionTime.Views.MissionMessageBox.Show(null, "Внимание", "Нельзя удалить подразделение, у которого есть вложенные отделы!");
            return;
        }

        if (MissionTime.Views.MissionMessageBox.Show(null, "Удаление", $"Вы уверены, что хотите удалить '{SelectedDepartment.Name}'?", true) == true)
        {
            try
            {
                _db.Department_Delete(SelectedDepartment.Id);
                LoadData();
            }
            catch (Exception ex)
            {
                MissionTime.Views.MissionMessageBox.Show(null, "Ошибка", "Ошибка при удалении: " + ex.Message);
            }
        }
    }

    private IEnumerable<Department> GetFlatList(Department d)
    {
        yield return d;
        foreach (var child in d.Children.SelectMany(GetFlatList))
            yield return child;
    }
}