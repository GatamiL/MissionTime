using MissionTime.Models;
using MissionTime.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;

public class DepartmentCreateViewModel : ObservableObject
{
    private string _name;
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    private string _shortName;
    public string ShortName { get => _shortName; set { _shortName = value; OnPropertyChanged(); } }

    private bool _hasResponsible;
    public bool HasResponsible
    {
        get => _hasResponsible;
        set { _hasResponsible = value; OnPropertyChanged(); }
    }

    // Списки можно оставить автосвойствами, они не меняются целиком
    public ObservableCollection<Department> ParentCandidates { get; set; }

    private Department _selectedParent;
    public Department SelectedParent
    {
        get => _selectedParent;
        set { _selectedParent = value; OnPropertyChanged(); }
    }

    public ObservableCollection<dynamic> Employees { get; set; }

    private dynamic _selectedEmployee;
    public dynamic SelectedEmployee
    {
        get => _selectedEmployee;
        set { _selectedEmployee = value; OnPropertyChanged(); }
    }

    public DepartmentCreateViewModel(DbService db, List<Department> allCurrentDeps)
    {
        ParentCandidates = new ObservableCollection<Department>();
        ParentCandidates.Add(new Department { Id = -1, Name = "< Добавить новый центр >", Level = -1 });

        foreach (var dep in allCurrentDeps) ParentCandidates.Add(dep);

        // Устанавливаем значение по умолчанию
        SelectedParent = ParentCandidates[0];

        Employees = new ObservableCollection<dynamic>();
        var dt = db.Employees_List_Brief();
        foreach (DataRow row in dt.Rows)
            Employees.Add(new { Id = Convert.ToInt64(row["Id"]), Fio = row["Fio"].ToString() });
    }
}