using MissionTime.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MissionTime.Views
{
    public partial class ListOfProgram : Window
    {
        private readonly DbService _db;

        public ListOfProgram(DbService db)
        {
            InitializeComponent();
            _db = db;
            LoadData();
        }

        private void LoadData()
        {
            dgPrograms.ItemsSource = _db.Programs_List().DefaultView;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var win = new ListOfProgram_Edit(_db, null);
            win.Owner = this;
            if (win.ShowDialog() == true) LoadData();
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgPrograms.SelectedItem is DataRowView row)
            {
                var win = new ListOfProgram_Edit(_db, row.Row);
                win.Owner = this;
                if (win.ShowDialog() == true) LoadData();
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgPrograms.SelectedItem is DataRowView row)
            {
                string name = row["Name"].ToString();
                if (MissionMessageBox.Show(this, "Удаление", $"Удалить программу '{name}'?", true) == true)
                {
                    _db.Program_Delete((long)row["Id"]);
                    LoadData();
                }
            }
        }
    }
}
