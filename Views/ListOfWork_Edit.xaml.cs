using System;
using System.Windows;
using MissionTime.Models;
using MissionTime.Services;

namespace MissionTime.Views
{
    public partial class ListOfWork_Edit : Window
    {
        private readonly DbService _db;
        private readonly WorkItem _existing;
        private readonly long? _parentId;

        public ListOfWork_Edit(DbService db, WorkItem existing, long? parentId)
        {
            InitializeComponent();
            _db = db;
            _existing = existing;
            _parentId = parentId;

            if (_existing != null)
            {
                this.Title = "Изменение работы";
                txtName.Text = _existing.Name;
                txtCode.Text = _existing.SpecialCode;
            }
            else
            {
                this.Title = "Добавление работы";
            }
            txtName.Focus();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            string code = txtCode.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MissionMessageBox.Show(this, "Внимание", "Пожалуйста, введите наименование работы.");
                return;
            }

            try
            {
                if (_existing != null)
                {
                    _db.Work_Update(_existing.Id, name, code);
                }
                else
                {
                    _db.Work_Create(name, code, _parentId);
                }

                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка сохранения", ex.Message);
            }
        }
    }
}