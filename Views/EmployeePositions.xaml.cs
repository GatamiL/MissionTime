using System.Windows;
using MissionTime.Services;
using MissionTime.ViewModels;

namespace MissionTime.Views
{
    public partial class EmployeePositions : Window
    {
        // Конструктор теперь принимает сервис базы данных
        public EmployeePositions(DbService db)
        {
            InitializeComponent();

            // Привязываем ViewModel и передаем ей базу
            this.DataContext = new EmployeePositionsViewModel(db);
        }
    }
}