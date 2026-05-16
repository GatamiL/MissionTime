using System.Windows;
using MissionTime.Services;
using MissionTime.ViewModels;

namespace MissionTime.Views
{
    public partial class EmployeeSignatures : Window
    {
        public EmployeeSignatures(DbService db)
        {
            InitializeComponent();
            this.DataContext = new EmployeeSignaturesViewModel(db);
        }
    }
}
