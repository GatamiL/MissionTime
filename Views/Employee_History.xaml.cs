using MissionTime.Models;
using MissionTime.Services;
using MissionTime.ViewModels;
using System;
using System.Data;
using System.Windows;

namespace MissionTime.Views
{
    public partial class Employee_History : Window
    {
        private readonly DbService _db;
        private readonly EmployeeBrief _employee;

        public Employee_History(DbService db, EmployeeBrief employee)
        {
            InitializeComponent();
            _db = db;
            _employee = employee;

            lblEmployeeName.Text = _employee.Fio;
            this.Title = $"История перемещений — {_employee.Fio}";

            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                // Получаем DataTable из нашего сервиса
                DataTable dt = _db.Employee_GetHistory(_employee.Id);

                // Добавим вычисляемую колонку для текста действия, 
                // чтобы не городить сложные конвертеры в XAML
                if (!dt.Columns.Contains("ActionText"))
                    dt.Columns.Add("ActionText", typeof(string));

                foreach (DataRow row in dt.Rows)
                {
                    // Преобразуем Action в число
                    int action = Convert.ToInt32(row["Action"]);

                    // Классический switch, который поймет даже самая старая Visual Studio
                    switch (action)
                    {
                        case 1:
                            row["ActionText"] = "Прием";
                            break;
                        case 2:
                            row["ActionText"] = "Перевод";
                            break;
                        case 3:
                            row["ActionText"] = "Увольнение";
                            break;
                        default:
                            row["ActionText"] = "Неизвестно";
                            break;
                    }
                }

                dgHistory.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", "Ошибка при загрузке истории: " + ex.Message);
            }
        }

        private void btnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (MissionMessageBox.Show(this, "Подтверждение отмены", "Вы действительно хотите ОТМЕНИТЬ (удалить) последнюю операцию в истории сотрудника?\n\nЭто действие необратимо!", true) == true)
            {
                try
                {
                    _db.Employee_UndoLastHistory(_employee.Id);
                    MissionMessageBox.Show(this, "Готово", "Последняя операция отменена.");
                    
                    // Перезагружаем, чтобы увидеть изменения
                    LoadHistory();
                }
                catch (Exception ex)
                {
                    MissionMessageBox.Show(this, "Предупреждение", $"Не удалось отменить операцию:\n{ex.Message}");
                }
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}