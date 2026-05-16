using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MissionTime.Services;

namespace MissionTime.ViewModels
{
    public class EmployeeSignaturesViewModel : ViewModelBase
    {
        private readonly DbService _db;
        private readonly IDialogService _dialogService;

        public ObservableCollection<EmployeeSignItem> Employees { get; } = new ObservableCollection<EmployeeSignItem>();

        private EmployeeSignItem _selectedEmployee;
        public EmployeeSignItem SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (SetProperty(ref _selectedEmployee, value))
                {
                    LoadSignatureDataForCurrent();
                    OnPropertyChanged(nameof(IsControlsEnabled));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsControlsEnabled => SelectedEmployee != null;

        private ImageSource _imageSource;
        public ImageSource ImageSource
        {
            get => _imageSource;
            set
            {
                if (SetProperty(ref _imageSource, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private byte[] _pendingImageData;

        private double _offsetX;
        public double OffsetX
        {
            get => _offsetX;
            set => SetProperty(ref _offsetX, value);
        }

        private double _offsetY;
        public double OffsetY
        {
            get => _offsetY;
            set => SetProperty(ref _offsetY, value);
        }

        private double _scale = 100.0;
        public double Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, value))
                {
                    OnPropertyChanged(nameof(ScaleRatio));
                }
            }
        }

        // Для прямой привязки в XAML к ScaleTransform
        public double ScaleRatio => Scale / 100.0;

        public ICommand LoadFileCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SaveCommand { get; }

        public EmployeeSignaturesViewModel(DbService db)
        {
            _db = db;
            _dialogService = new WpfDialogService(db);

            LoadFileCommand = new RelayCommand(_ => OnLoadFile(), _ => SelectedEmployee != null);
            ClearCommand = new RelayCommand(_ => OnClear(), _ => SelectedEmployee != null && ImageSource != null);
            SaveCommand = new RelayCommand(_ => OnSave(), _ => SelectedEmployee != null);

            LoadEmployees();
        }

        private void LoadEmployees()
        {
            try
            {
                Employees.Clear();
                var dt = _db.Employees_List_Brief();
                foreach (DataRow r in dt.Rows)
                {
                    Employees.Add(new EmployeeSignItem
                    {
                        Id = Convert.ToInt64(r["Id"]),
                        Fio = r["Fio"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Log("Ошибка загрузки списка сотрудников:", ex);
            }
        }

        private void LoadSignatureDataForCurrent()
        {
            ImageSource = null;
            _pendingImageData = null;
            OffsetX = 0;
            OffsetY = 0;
            Scale = 100.0;

            if (SelectedEmployee == null) return;

            try
            {
                var dt = _db.EmployeeSign_Get(SelectedEmployee.Id);
                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    if (row["ImageData"] != DBNull.Value)
                    {
                        byte[] bytes = (byte[])row["ImageData"];
                        _pendingImageData = bytes;
                        ImageSource = LoadImageFromBytes(bytes);
                    }
                    
                    OffsetX = Convert.ToDouble(row["OffsetX"]);
                    OffsetY = Convert.ToDouble(row["OffsetY"]);
                    Scale = Convert.ToDouble(row["Scale"]) * 100.0;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Ошибка", "Не удалось получить данные подписи: " + ex.Message);
            }
        }

        private void OnLoadFile()
        {
            if (SelectedEmployee == null) return;

            var openFileDialog = new OpenFileDialog
            {
                Filter = "PNG Images (*.png)|*.png|All images (*.bmp, *.jpg, *.png)|*.bmp;*.jpg;*.png",
                Title = "Выберите изображение подписи"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(openFileDialog.FileName);

                    // 🚀 АВТОМАТИЧЕСКАЯ ОБРАБОТКА: Удаление белого фона и обрезка по краям (Auto-Crop)
                    bytes = ImageHelper.ProcessSignatureImage(bytes);

                    var loadedImage = LoadImageFromBytes(bytes);
                    
                    if (loadedImage != null)
                    {
                        ImageSource = loadedImage;
                        _pendingImageData = bytes;
                    }
                    else
                    {
                        _dialogService.ShowMessage("Внимание", "Не удалось декодировать файл как изображение.");
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage("Ошибка", "Не удалось загрузить файл: " + ex.Message);
                }
            }
        }

        private void OnClear()
        {
            if (SelectedEmployee == null) return;
            
            if (_dialogService.ShowConfirmation("Удаление", $"Удалить привязанную подпись для сотрудника '{SelectedEmployee.Fio}'?"))
            {
                try
                {
                    _db.EmployeeSign_Delete(SelectedEmployee.Id);
                    LoadSignatureDataForCurrent();
                }
                catch (Exception ex)
                {
                     _dialogService.ShowMessage("Ошибка удаления", ex.Message);
                }
            }
        }

        private void OnSave()
        {
            if (SelectedEmployee == null) return;

            try
            {
                _db.EmployeeSign_Save(SelectedEmployee.Id, _pendingImageData, OffsetX, OffsetY, Scale / 100.0);
                _dialogService.ShowMessage("Сохранено", $"Настройки подписи для '{SelectedEmployee.Fio}' успешно применены.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Ошибка сохранения", ex.Message);
            }
        }

        private ImageSource LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            try
            {
                var biImg = new BitmapImage();
                using (var ms = new MemoryStream(imageData))
                {
                    biImg.BeginInit();
                    biImg.StreamSource = ms;
                    biImg.CacheOption = BitmapCacheOption.OnLoad;
                    biImg.EndInit();
                }
                biImg.Freeze(); 
                return biImg;
            }
            catch
            {
                return null;
            }
        }
    }

    public class EmployeeSignItem
    {
        public long Id { get; set; }
        public string Fio { get; set; }
    }
}
