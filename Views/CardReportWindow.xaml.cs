using MissionTime.Services;
using System.Windows;

namespace MissionTime.Views
{
    public partial class CardReportWindow : Window
    {
        private readonly DbService _db;
        public CardReportWindow(DbService db, int currentYear, int currentMonth)
        {
            InitializeComponent();
            _db = db;
            
            // Populate years as a basic placeholder
            int baseYear = System.DateTime.Now.Year;
            for (int y = 2025; y <= baseYear + 1; y++)
            {
                cbYear.Items.Add(y);
            }
            cbYear.SelectedItem = currentYear;
            cbMonth.SelectedIndex = currentMonth;

            LoadEmployees();
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

        private void LoadEmployees()
        {
            var dtEmps = _db.Employees_List_Brief();
            var emps = new System.Collections.Generic.List<EmpItem>();
            foreach (System.Data.DataRow r in dtEmps.Rows)
            {
                emps.Add(new EmpItem
                {
                    Id = System.Convert.ToInt64(r["Id"]),
                    Fio = r["Fio"].ToString()
                });
            }
            cbExecutor.ItemsSource = emps;
            cbReviewer.ItemsSource = emps;
        }

        private class EmpItem
        {
            public long Id { get; set; }
            public string Fio { get; set; }
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
                // Если ShortName пустой, возьмем Name, чтобы не было пустых строк
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

            // Строим дерево для правильного подсчета сотрудников
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

            // Берем только Level == 3, где суммарное количество сотрудников > 0
            var activeDeps = new System.Collections.Generic.List<ActiveDepItem>();
            foreach (var d in allItems)
            {
                if (d.Level == 3 && d.EmpCount > 0)
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
            if (cbDepartment.SelectedItem is ActiveDepItem selectedDep && selectedDep.ResponsibleId.HasValue)
            {
                cbReviewer.SelectedValue = selectedDep.ResponsibleId.Value;
            }
        }

        private void cbProgram_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                LoadPeriods();
            }
        }

        private void cbExecutor_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasPeriods = cbPeriod.IsEnabled && cbPeriod.SelectedItem != null && cbPeriod.SelectedItem.ToString() != "Нет периодов";
            if (!hasPeriods)
            {
                chkExecutorSign.IsEnabled = false;
                return;
            }

            long? empId = cbExecutor.SelectedValue as long?;
            bool hasSig = ExcelImageHelper.HasSignature(_db, empId);
            chkExecutorSign.IsEnabled = hasSig;
            chkExecutorSign.IsChecked = hasSig; // По умолчанию ставим галочку, если подпись есть
        }

        private void cbReviewer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasPeriods = cbPeriod.IsEnabled && cbPeriod.SelectedItem != null && cbPeriod.SelectedItem.ToString() != "Нет периодов";
            if (!hasPeriods)
            {
                chkReviewerSign.IsEnabled = false;
                return;
            }

            long? empId = cbReviewer.SelectedValue as long?;
            bool hasSig = ExcelImageHelper.HasSignature(_db, empId);
            chkReviewerSign.IsEnabled = hasSig;
            chkReviewerSign.IsChecked = hasSig;
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

                // Первая неделя (начинается в понедельник)
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
                        // Проверяем, пересекается ли неделя с текущим месяцем
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

            // Заблокировать поля исполнителя/проверяющего, если периода не существует
            bool hasPeriods = cbPeriod.IsEnabled && cbPeriod.SelectedItem != null && cbPeriod.SelectedItem.ToString() != "Нет периодов";
            cbExecutor.IsEnabled = hasPeriods;
            cbReviewer.IsEnabled = hasPeriods;

            if (!hasPeriods)
            {
                chkExecutorSign.IsEnabled = false;
                chkReviewerSign.IsEnabled = false;
            }
            else
            {
                // Если период появился — проставляем актуальное состояние галочек подписи
                long? execId = cbExecutor.SelectedValue as long?;
                bool hasExecSig = ExcelImageHelper.HasSignature(_db, execId);
                chkExecutorSign.IsEnabled = hasExecSig;
                chkExecutorSign.IsChecked = hasExecSig;

                long? revId = cbReviewer.SelectedValue as long?;
                bool hasRevSig = ExcelImageHelper.HasSignature(_db, revId);
                chkReviewerSign.IsEnabled = hasRevSig;
                chkReviewerSign.IsChecked = hasRevSig;
            }
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var mainVm = System.Windows.Application.Current.MainWindow.DataContext as MissionTime.ViewModels.MainViewModel;
            try
            {
                if (cbDepartment.SelectedItem == null || cbProgram.SelectedItem == null || cbPeriod.SelectedItem == null || cbPeriod.SelectedItem.ToString() == "Нет периодов")
                {
                    MissionMessageBox.Show(this, "Внимание", "Пожалуйста, выберите подразделение, программу и период.");
                    return;
                }

                // Блокировка UI
                var btn = sender as System.Windows.Controls.Button;
                if (btn != null) btn.IsEnabled = false;
                this.Cursor = System.Windows.Input.Cursors.Wait;
                if (mainVm != null)
                {
                    mainVm.IsBusy = true;
                    mainVm.StatusText = "Формирование карточек учета...";
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

                string doneFio = cbExecutor.Text;
                string checkedFio = cbReviewer.Text;
                long? executorId = (chkExecutorSign.IsChecked == true) ? (cbExecutor.SelectedValue as long?) : null;
                long? reviewerId = (chkReviewerSign.IsChecked == true) ? (cbReviewer.SelectedValue as long?) : null;

                string templatesDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Templates");
                string reportsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Reports");
                
                string templatePath = System.IO.Path.Combine(templatesDir, "LaborCardTemplate.xlsx");
                
                string depName = ExcelEmployeeUtils.SanitizeFileName(((ActiveDepItem)cbDepartment.SelectedItem).Name);
                string progName = ExcelEmployeeUtils.SanitizeFileName(((ProgItem)cbProgram.SelectedItem).ShortName);
                string periodStr = $"{periodStart:dd.MM.yyyy}-{periodEnd:dd.MM.yyyy}";
                string outPath = System.IO.Path.Combine(reportsDir, $"Карточки_{depName}_{progName}_{periodStr}.xlsx");

                // ВЫПОЛНЯЕМ В ФОНЕ!
                await System.Threading.Tasks.Task.Run(() =>
                {
                    ExcelEmployeeUtils.GenerateReport(
                        templatePath, 
                        outPath, 
                        _db, 
                        progId, 
                        depId, 
                        periodStart, 
                        periodEnd, 
                        doneFio, 
                        checkedFio,
                        executorId,
                        reviewerId);
                });

                if (mainVm != null) mainVm.StatusText = "Карточки сформированы";

                if (MissionMessageBox.Show(this, "Успех", $"Отчет успешно сформирован!\nОткрыть файл?\n\n{outPath}", true) == true)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outPath) { UseShellExecute = true });
                }
            }
            catch (System.Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", $"Ошибка при формировании отчета:\n{ex.Message}");
                if (mainVm != null) mainVm.StatusText = "Ошибка формирования карточек";
            }
            finally
            {
                // Разблокировка UI
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
