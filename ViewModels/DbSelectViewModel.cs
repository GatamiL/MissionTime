using Microsoft.Win32;
using MissionTime.Services;
using MissionTime.Views;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MissionTime.ViewModels
{
    public class DbSelectViewModel : ObservableObject
    {
        private readonly string _appDir = AppDomain.CurrentDomain.BaseDirectory;

        // Список файлов для ListBox
        public ObservableCollection<string> DatabaseFiles { get; set; }

        private string _selectedFile;
        public string SelectedFile
        {
            get => _selectedFile;
            set { _selectedFile = value; OnPropertyChanged(); }
        }

        // Команды для кнопок
        public ICommand SelectCommand { get; }
        public ICommand CreateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand BrowseCommand { get; }
        public ICommand ExitCommand { get; }

        public DbSelectViewModel()
        {
            DatabaseFiles = new ObservableCollection<string>();

            // Инициализация команд
            SelectCommand = new RelayCommand(p => ExecuteSelect(), p => !string.IsNullOrEmpty(SelectedFile));
            CreateCommand = new RelayCommand(p => ExecuteCreate());
            DeleteCommand = new RelayCommand(p => ExecuteDelete(), p => !string.IsNullOrEmpty(SelectedFile));
            BrowseCommand = new RelayCommand(p => ExecuteBrowse());
            ExitCommand = new RelayCommand(p => Application.Current.Shutdown());

            LoadDatabases();
        }

        private void LoadDatabases()
        {
            DatabaseFiles.Clear();
            var files = Directory.GetFiles(_appDir, "*.db")
                                 .Select(Path.GetFileName)
                                 .OrderBy(f => f);

            foreach (var file in files)
                DatabaseFiles.Add(file);
        }

        private void ExecuteSelect()
        {
            string dbPath = Path.Combine(_appDir, SelectedFile);

            if (DbSchema.Validate(dbPath, out string error))
            {
                // 1. Создаем сервис базы данных
                var dbService = new Services.DbService(dbPath);

                // 2. Создаем главное окно и передаем туда сервис
                var mainWin = new Views.MainWindow(dbService);

                // 3. Делаем его главным окном приложения (чтобы при закрытии всё тухло)
                Application.Current.MainWindow = mainWin;

                // 4. Показываем главное окно
                mainWin.Show();

                // 5. Закрываем текущее окно выбора (DbSelect)
                // Нам нужно найти само окно через Application.Current.Windows
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Views.DbSelect)
                    {
                        window.Close();
                        break;
                    }
                }
            }
            else
            {
                MissionMessageBox.Show(
                    Application.Current.MainWindow,
                    "Ошибка базы данных",
                    $"Файл поврежден.\nДетали: {error}"
                );
            }
        }

        private void ExecuteCreate()
        {
            var createWin = new Views.DbSelect_create();

            // Привязываем владельца. В окне выбора это само окно выбора.
            // Находим его через Application.Current
            createWin.Owner = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w is Views.DbSelect);
            createWin.ShowInTaskbar = false; // Чтобы не плодить иконки в Alt+Tab

            if (createWin.ShowDialog() == true)
            {
                LoadDatabases();
                SelectedFile = Path.GetFileName(createWin.CreatedFilePath);
            }
        }

        private void ExecuteDelete()
        {
            // Используем наш кастомный MessageBox с флагом вопроса (isQuestion = true)
            bool? result = MissionMessageBox.Show(
                Application.Current.MainWindow,
                "Удаление базы",
                $"Вы уверены, что хотите навсегда удалить базу данных '{SelectedFile}'?",
                isQuestion: true
            );

            if (result == true)
            {
                try
                {
                    string fullPath = Path.Combine(_appDir, SelectedFile);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        LoadDatabases(); // Обновляем список после удаления
                    }
                }
                catch (Exception ex)
                {
                    MissionMessageBox.Show(
                        Application.Current.MainWindow,
                        "Ошибка удаления",
                        $"Не удалось удалить файл:\n{ex.Message}"
                    );
                }
            }
        }

        private void ExecuteBrowse()
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "SQLite DB (*.db)|*.db" };
            if (ofd.ShowDialog() == true && DbSchema.Validate(ofd.FileName, out string error))
            {
                MessageBox.Show("Внешняя база подключена!");
            }
        }
    }
}