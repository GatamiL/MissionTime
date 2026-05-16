using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MissionTime.Models
{
    public class Department : INotifyPropertyChanged
    {
        // Поля из базы данных
        public long Id { get; set; }
        public long? ParentId { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public int Level { get; set; }
        public long? ResponsibleId { get; set; }
        public string ResponsibleFio { get; set; } // Для отображения ФИО ответственного

        // Для работы TreeView
        public List<Department> Children { get; set; } = new List<Department>();

        // Свойство для выделения в интерфейсе
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        // Реализация интерфейса для обновления UI
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}