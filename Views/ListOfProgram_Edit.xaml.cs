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
    public partial class ListOfProgram_Edit : Window
    {
        private readonly DbService _db;
        private readonly DataRow _row;

        public ListOfProgram_Edit(DbService db, DataRow row)
        {
            InitializeComponent();
            _db = db;
            _row = row;

            if (_row != null)
            {
                this.Title = "Изменение программы";
                txtName.Text = _row["Name"].ToString();
                txtShortName.Text = _row["ShortName"].ToString();

                if (DateTime.TryParse(_row["DateStart"].ToString(), out DateTime ds)) dpStart.SelectedDate = ds;
                if (DateTime.TryParse(_row["DateEnd"].ToString(), out DateTime de)) dpEnd.SelectedDate = de;
            }
            else
            {
                dpStart.SelectedDate = DateTime.Today;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtShortName.Text) || dpStart.SelectedDate == null)
            {
                MissionMessageBox.Show(this, "Ошибка", "Заполните название и дату начала.");
                return;
            }

            try
            {
                string ds = dpStart.SelectedDate.Value.ToString("yyyy-MM-dd");
                string de = dpEnd.SelectedDate?.ToString("yyyy-MM-dd");

                if (_row == null)
                    _db.Program_Create(txtName.Text, txtShortName.Text, ds, de);
                else
                    _db.Program_Update((long)_row["Id"], txtName.Text, txtShortName.Text, ds, de);

                this.DialogResult = true;
            }
            catch (Exception ex) { MissionMessageBox.Show(this, "Ошибка", ex.Message); }
        }
    }
}
