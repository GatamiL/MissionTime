using MissionTime.Models;
using MissionTime.Services;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MissionTime.ViewModels
{
    public class EmployeePositionsViewModel : ObservableObject
    {
        private readonly DbService _db;
        public ObservableCollection<Position> Positions { get; set; }

        private Position _selectedPosition;
        public Position SelectedPosition
        {
            get => _selectedPosition;
            set { _selectedPosition = value; OnPropertyChanged(); }
        }

        public ICommand CreateCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public EmployeePositionsViewModel(DbService db)
        {
            _db = db;
            Positions = new ObservableCollection<Position>();

            CreateCommand = new RelayCommand(p => ExecuteCreate());
            EditCommand = new RelayCommand(p => ExecuteEdit(), p => SelectedPosition != null);
            DeleteCommand = new RelayCommand(p => ExecuteDelete(), p => SelectedPosition != null);

            LoadData();
        }

        private void LoadData()
        {
            Positions.Clear();
            // Просто вызываем метод сервиса и заполняем коллекцию
            var dt = _db.Positions_List();
            foreach (DataRow row in dt.Rows)
            {
                Positions.Add(new Position
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Name = row["Name"].ToString()
                });
            }
        }

        private void ExecuteCreate()
        {
            var editWin = new Views.EmployeePosition_Edit();
            editWin.Owner = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w is Views.EmployeePositions);

            if (editWin.ShowDialog() == true)
            {
                try
                {
                    // Весь SQL спрятан внутри метода Position_Create
                    _db.Position_Create(editWin.PositionName);
                    LoadData();
                }
                catch (Exception ex)
                {
                    Views.MissionMessageBox.Show(null, "Ошибка", "Не удалось создать: " + ex.Message);
                }
            }
        }

        private void ExecuteEdit()
        {
            // 1. Проверка на выбор (защита от дурака)
            if (SelectedPosition == null) return;

            // 2. Открываем наше красивое окно редактирования, передаем текущее имя
            var editWin = new Views.EmployeePosition_Edit(SelectedPosition.Name);

            // Привязываем владельца, чтобы не прыгало в Alt+Tab
            editWin.Owner = Application.Current.Windows.Cast<Window>()
                .FirstOrDefault(w => w is Views.EmployeePositions);

            // 3. Если нажали "Сохранить"
            if (editWin.ShowDialog() == true)
            {
                try
                {
                    // 4. Используем твой новый метод из DbService
                    // Больше никакого SQL кода здесь!
                    _db.Position_Update(SelectedPosition.Id, editWin.PositionName);

                    // 5. Обновляем список, чтобы увидеть изменения
                    LoadData();

                    // Опционально: статус внизу (если привязал MainViewModel к статус-бару)
                    // Но в справочниках обычно достаточно простого обновления списка
                }
                catch (Exception ex)
                {
                    Views.MissionMessageBox.Show(null, "Ошибка обновления",
                        $"Не удалось сохранить изменения: {ex.Message}");
                }
            }
        }

        private void ExecuteDelete()
        {
            if (SelectedPosition == null) return;

            if (Views.MissionMessageBox.Show(null, "Удаление", $"Удалить '{SelectedPosition.Name}'?", true) == true)
            {
                try
                {
                    // Чистый вызов без SQL в коде ViewModel
                    _db.Position_Delete(SelectedPosition.Id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    Views.MissionMessageBox.Show(null, "Ошибка", "Нельзя удалить (возможно, должность используется): " + ex.Message);
                }
            }
        }
    }
}