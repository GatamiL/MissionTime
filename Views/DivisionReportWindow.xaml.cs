using MissionTime.Services;
using System.Windows;

namespace MissionTime.Views
{
    public partial class DivisionReportWindow : Window
    {
        private readonly DbService _db;
        public DivisionReportWindow(DbService db, int currentYear, int currentMonth)
        {
            InitializeComponent();
            _db = db;
            
            int baseYear = System.DateTime.Now.Year;
            for (int y = 2025; y <= baseYear + 1; y++)
            {
                cbYear.Items.Add(y);
            }
            cbYear.SelectedItem = currentYear;
            cbMonth.SelectedIndex = currentMonth;

            LoadDepartments();
            LoadPrograms();
        }

        private void OnDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                LoadDepartments();
                LoadPrograms();
            }
        }

        private class ActiveDepItem
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public long? ResponsibleId { get; set; }
        }

        private class DepItem
        {
            public long Id { get; set; }
            public long? ParentId { get; set; }
            public string ShortName { get; set; }
            public int Level { get; set; }
            public int EmpCount { get; set; }
            public long? ResponsibleId { get; set; }
            public System.Collections.Generic.List<DepItem> Children { get; set; } = new System.Collections.Generic.List<DepItem>();
        }

        private class ProgItem
        {
            public long Id { get; set; }
            public string ShortName { get; set; }
            public string DateStart { get; set; }
            public string DateEnd { get; set; }
        }

        private void LoadDepartments()
        {
            if (cbYear.SelectedItem == null || cbMonth.SelectedIndex == -1) return;

            int year = (int)cbYear.SelectedItem;
            int month = cbMonth.SelectedIndex + 1;

            var dtDeps = _db.GetDepartmentsTable();
            var empCounts = _db.GetDepartmentsEmployeeCounts(year, month);

            var allItems = new System.Collections.Generic.List<DepItem>();
            foreach (System.Data.DataRow r in dtDeps.Rows)
            {
                long id = System.Convert.ToInt64(r["Id"]);
                string shortName = r["ShortName"] != System.DBNull.Value && !string.IsNullOrWhiteSpace(r["ShortName"].ToString()) 
                    ? r["ShortName"].ToString() 
                    : r["Name"].ToString();

                allItems.Add(new DepItem
                {
                    Id = id,
                    ParentId = r["ParentId"] != System.DBNull.Value ? System.Convert.ToInt64(r["ParentId"]) : (long?)null,
                    ShortName = shortName,
                    Level = System.Convert.ToInt32(r["Level"]),
                    EmpCount = empCounts.ContainsKey(id) ? empCounts[id] : 0,
                    ResponsibleId = r["ResponsibleId"] != System.DBNull.Value ? System.Convert.ToInt64(r["ResponsibleId"]) : (long?)null
                });
            }

            var tree = new System.Collections.Generic.List<DepItem>();
            foreach (var item in allItems)
            {
                if (item.ParentId == null) tree.Add(item);
                else allItems.Find(i => i.Id == item.ParentId)?.Children.Add(item);
            }

            int CalcSum(DepItem item)
            {
                int total = item.EmpCount;
                foreach (var child in item.Children) total += CalcSum(child);
                item.EmpCount = total;
                return total;
            }

            foreach (var root in tree) CalcSum(root);

            // Используем Level == 2 (комплексы/дирекции)
            var activeDeps = new System.Collections.Generic.List<ActiveDepItem>();
            foreach (var d in allItems)
            {
                if (d.Level == 2 && d.EmpCount > 0)
                {
                    activeDeps.Add(new ActiveDepItem { Id = d.Id, Name = d.ShortName, ResponsibleId = d.ResponsibleId });
                }
            }

            cbDepartment.ItemsSource = activeDeps;
            if (activeDeps.Count > 0)
            {
                cbDepartment.SelectedIndex = 0;
            }
        }

        private void LoadPrograms()
        {
            if (cbYear.SelectedItem == null || cbMonth.SelectedIndex == -1) return;

            int year = (int)cbYear.SelectedItem;
            int month = cbMonth.SelectedIndex + 1;

            var dtPrograms = _db.Programs_List_Actual(year, month);
            
            var programs = new System.Collections.Generic.List<ProgItem>();
            foreach (System.Data.DataRow r in dtPrograms.Rows)
            {
                string shortName = r["ShortName"] != System.DBNull.Value && !string.IsNullOrWhiteSpace(r["ShortName"].ToString()) 
                    ? r["ShortName"].ToString() 
                    : r["Name"].ToString();

                programs.Add(new ProgItem 
                { 
                    Id = System.Convert.ToInt64(r["Id"]), 
                    ShortName = shortName,
                    DateStart = r["DateStart"] != System.DBNull.Value ? r["DateStart"].ToString() : "",
                    DateEnd = r["DateEnd"] != System.DBNull.Value ? r["DateEnd"].ToString() : ""
                });
            }

            if (programs.Count > 0)
            {
                cbProgram.ItemsSource = programs;
                cbProgram.IsEnabled = true;
                cbProgram.SelectedIndex = 0;
            }
            else
            {
                cbProgram.ItemsSource = new System.Collections.Generic.List<ProgItem> { new ProgItem { Id = -1, ShortName = "Нет программ" } };
                cbProgram.IsEnabled = false;
                cbProgram.SelectedIndex = 0;
            }
            LoadPeriods();
        }

        private void cbDepartment_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            chkSign.IsChecked = false;
            chkSign.IsEnabled = false;

            if (cbDepartment.SelectedItem is ActiveDepItem item)
            {
                bool hasSig = ExcelImageHelper.HasSignature(_db, item.ResponsibleId);
                chkSign.IsEnabled = hasSig;
                chkSign.IsChecked = hasSig;
            }
        }

        private void cbProgram_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                LoadPeriods();
            }
        }

        private void LoadPeriods()
        {
            cbPeriod.ItemsSource = null;
            if (cbProgram.SelectedItem is ProgItem prog && prog.Id != -1 && cbYear.SelectedItem != null && cbMonth.SelectedIndex != -1)
            {
                int year = (int)cbYear.SelectedItem;
                int month = cbMonth.SelectedIndex + 1;
                long progId = prog.Id;

                if (!System.DateTime.TryParse(prog.DateStart, out System.DateTime dateStart))
                {
                    return;
                }

                System.DateTime monthStart = new System.DateTime(year, month, 1);
                System.DateTime monthEnd = new System.DateTime(year, month, System.DateTime.DaysInMonth(year, month));

                var loggedDates = _db.GetLoggedDatesForProgram(progId);
                var loggedDatesSet = new System.Collections.Generic.HashSet<System.DateTime>();
                foreach (var d in loggedDates) loggedDatesSet.Add(d.Date);

                var periods = new System.Collections.Generic.List<string>();

                int diff = (7 + (dateStart.DayOfWeek - System.DayOfWeek.Monday)) % 7;
                System.DateTime currentWeekStart = dateStart.AddDays(-diff).Date;
                int weekIndex = 1;

                System.DateTime progEnd = System.DateTime.MaxValue;
                if (!string.IsNullOrEmpty(prog.DateEnd) && System.DateTime.TryParse(prog.DateEnd, out System.DateTime parsedEnd))
                {
                    progEnd = parsedEnd;
                }

                while (currentWeekStart <= monthEnd)
                {
                    System.DateTime currentWeekEnd = currentWeekStart.AddDays(6);

                    bool hasHours = false;
                    for (System.DateTime d = currentWeekStart; d <= currentWeekEnd; d = d.AddDays(1))
                    {
                        if (loggedDatesSet.Contains(d) && d >= dateStart && d <= progEnd)
                        {
                            hasHours = true;
                            break;
                        }
                    }

                    if (hasHours)
                    {
                        if (currentWeekEnd >= monthStart && currentWeekStart <= monthEnd)
                        {
                            System.DateTime displayEnd = currentWeekEnd > monthEnd ? monthEnd : currentWeekEnd;
                            periods.Add($"{weekIndex}) {currentWeekStart:dd.MM}-{displayEnd:dd.MM}");
                        }
                        weekIndex++;
                    }
                    currentWeekStart = currentWeekStart.AddDays(7);
                }

                if (periods.Count > 0)
                {
                    cbPeriod.ItemsSource = periods;
                    cbPeriod.IsEnabled = true;
                    cbPeriod.SelectedIndex = 0;
                }
                else
                {
                    cbPeriod.ItemsSource = new System.Collections.Generic.List<string> { "Нет периодов" };
                    cbPeriod.IsEnabled = false;
                    cbPeriod.SelectedIndex = 0;
                }
            }
            else
            {
                cbPeriod.ItemsSource = new System.Collections.Generic.List<string> { "Нет периодов" };
                cbPeriod.IsEnabled = false;
                cbPeriod.SelectedIndex = 0;
            }
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var mainVm = System.Windows.Application.Current.MainWindow.DataContext as MissionTime.ViewModels.MainViewModel;

            try
            {
                if (cbDepartment.SelectedItem == null || cbProgram.SelectedItem == null || cbPeriod.SelectedItem == null || cbPeriod.SelectedItem.ToString() == "Нет периодов")
                {
                    MissionMessageBox.Show(this, "Внимание", "Пожалуйста, выберите комплекс, программу и период.");
                    return;
                }

                // Блокировка UI
                var btn = sender as System.Windows.Controls.Button;
                if (btn != null) btn.IsEnabled = false;
                this.Cursor = System.Windows.Input.Cursors.Wait;
                if (mainVm != null)
                {
                    mainVm.IsBusy = true;
                    mainVm.StatusText = "Формирование отчета по КОМПЛЕКСУ...";
                }

                long depId = ((ActiveDepItem)cbDepartment.SelectedItem).Id;
                long progId = ((ProgItem)cbProgram.SelectedItem).Id;
                
                string pStr = cbPeriod.SelectedItem.ToString();
                string[] parts = pStr.Split(new[] { ") " }, System.StringSplitOptions.None);
                if (parts.Length < 2) return;

                string[] dates = parts[1].Split('-');
                int year = (int)cbYear.SelectedItem;

                System.DateTime periodStart = System.DateTime.ParseExact(dates[0] + "." + year, "dd.MM.yyyy", null);
                System.DateTime periodEnd = System.DateTime.ParseExact(dates[1] + "." + year, "dd.MM.yyyy", null);

                string templatesDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Templates");
                string reportsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Reports");
                
                string templatePath = System.IO.Path.Combine(templatesDir, "ComplexReportTemplate.xlsx");
                
                string depName = MissionTime.Services.ExcelComplexUtils.SanitizeFileName(((ActiveDepItem)cbDepartment.SelectedItem).Name);
                string progName = MissionTime.Services.ExcelComplexUtils.SanitizeFileName(((ProgItem)cbProgram.SelectedItem).ShortName);
                string periodStr = $"{periodStart:dd.MM.yyyy}-{periodEnd:dd.MM.yyyy}";
                string outPath = System.IO.Path.Combine(reportsDir, $"{depName}_{progName}_{periodStr}.xlsx");

                long? responsibleId = (chkSign.IsChecked == true) 
                    ? ((ActiveDepItem)cbDepartment.SelectedItem).ResponsibleId 
                    : null;

                // ГЕНЕРИРУЕМ В ФОНЕ!
                await System.Threading.Tasks.Task.Run(() => 
                {
                    MissionTime.Services.ExcelComplexUtils.GenerateReport(
                        templatePath, 
                        outPath, 
                        _db, 
                        progId, 
                        depId, 
                        periodStart, 
                        periodEnd,
                        responsibleId);
                });

                if (mainVm != null) mainVm.StatusText = "Отчет по комплексу готов";

                if (MissionMessageBox.Show(this, "Успех", $"Отчет за комплекс успешно сформирован!\nОткрыть файл?\n\n{outPath}", true) == true)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outPath) { UseShellExecute = true });
                }
            }
            catch (System.Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", $"Ошибка при формировании отчета:\n{ex.Message}");
                if (mainVm != null) mainVm.StatusText = "Ошибка формирования отчета";
            }
            finally
            {
                this.Cursor = null;
                var btn = sender as System.Windows.Controls.Button;
                if (btn != null) btn.IsEnabled = true;
                if (mainVm != null) mainVm.IsBusy = false;
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
