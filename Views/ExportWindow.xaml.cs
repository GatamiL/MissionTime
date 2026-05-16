using Microsoft.Win32;
using MissionTime.Services;
using System;
using System.Data;
using System.Windows;

namespace MissionTime.Views
{
    public partial class ExportWindow : Window
    {
        private readonly DbService _db;

        public ExportWindow(DbService db, int currentYear, int currentMonth)
        {
            InitializeComponent();
            _db = db;

            // Наполняем годами
            int baseYear = DateTime.Now.Year;
            for (int y = 2025; y <= baseYear + 1; y++)
            {
                cbYear.Items.Add(y);
            }
            
            cbYear.SelectedItem = currentYear;
            cbMonth.SelectedIndex = currentMonth;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (cbYear.SelectedItem == null || cbMonth.SelectedIndex == -1)
            {
                MissionMessageBox.Show(this, "Внимание", "Пожалуйста, выберите год и месяц.");
                return;
            }

            int year = (int)cbYear.SelectedItem;
            int month = cbMonth.SelectedIndex + 1;

            try
            {
                this.Cursor = System.Windows.Input.Cursors.Wait;
                
                // 1. Извлекаем плоскую таблицу данных
                DataTable dt = _db.GetExportDataForPeriod(year, month);
                
                this.Cursor = null;

                if (dt.Rows.Count == 0)
                {
                    MissionMessageBox.Show(this, "Данные отсутствуют", "За выбранный период в базе данных нет ни одной записи отработанных часов.");
                    return;
                }

                // 2. Настройка сохранения файла
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "MissionTime Export (*.mte)|*.mte",
                    FileName = $"MT_Export_{year}_{month:D2}_{DateTime.Now:yyyyMMdd}",
                    Title = "Сохранить файл экспорта"
                };

                if (sfd.ShowDialog() == true)
                {
                    // Оборачиваем в DataSet для корректной XML сериализации
                    DataSet ds = new DataSet("MissionTimeExport");
                    
                    // Добавим метаинформацию о периоде
                    DataTable meta = new DataTable("MetaData");
                    meta.Columns.Add("ExportDate", typeof(string));
                    meta.Columns.Add("TargetYear", typeof(int));
                    meta.Columns.Add("TargetMonth", typeof(int));
                    meta.Rows.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), year, month);
                    
                    dt.TableName = "TimesheetEntries";
                    
                    ds.Tables.Add(meta);
                    ds.Tables.Add(dt.Copy());

                    // Пишем XML файл
                    ds.WriteXml(sfd.FileName, XmlWriteMode.WriteSchema);

                    MissionMessageBox.Show(this, "Успех", $"Данные успешно экспортированы ({dt.Rows.Count} записей).\nФайл: {sfd.FileName}");
                    this.DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                this.Cursor = null;
                MissionMessageBox.Show(this, "Ошибка", $"Ошибка при экспорте:\n{ex.Message}");
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
