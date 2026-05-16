using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MissionTime.Models
{
    public class WorkItem : INotifyPropertyChanged
    {
        public long Id { get; set; }
        public long? ParentId { get; set; }
        public string Name { get; set; }
        public string SpecialCode { get; set; } // Код для табеля (например, "01", "Р", "ОТ")

        public List<WorkItem> Children { get; set; } = new List<WorkItem>();

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}