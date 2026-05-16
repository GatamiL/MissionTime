using MissionTime.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace MissionTime.Views
{
    public partial class DbSelect_create : Window
    {
        public string FileName { get; set; }
        public string CreatedFilePath { get; private set; }

        public DbSelect_create()
        {
            InitializeComponent();
            this.DataContext = this;
            txtDbName.Focus();
        }

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                // Используем твой красивый месседжбокс
                MissionMessageBox.Show(this, "Внимание", "Введите название файла базы данных.");
                return;
            }

            string cleanName = FileName.Trim();
            if (!cleanName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                cleanName += ".db";

            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cleanName);

            if (File.Exists(fullPath))
            {
                MissionMessageBox.Show(this, "Файл существует", "База с таким именем уже есть в папке программы.");
                return;
            }

            try
            {
                DbSchema.CreateNew(fullPath);
                CreatedFilePath = fullPath;
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", $"Не удалось создать файл: {ex.Message}");
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;

        private void txtDbName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnCreate_Click(null, null);
        }
    }
}