using MissionTime.Models;
using MissionTime.Services;
using MissionTime.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.ComponentModel; // Добавь это
using System.Windows.Data;    // И это
using System.Threading.Tasks;

namespace MissionTime.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DbService _db;
        private readonly IDialogService _dialogService;

        // --- СВОЙСТВА ФИЛЬТРОВ ---
        public ObservableCollection<DepFilterItem> Departments { get; set; } = new ObservableCollection<DepFilterItem>();
        public DataView Programs { get; set; }
        private bool _isCalculating = false;
        public ObservableCollection<int> Years { get; set; } = new ObservableCollection<int>();

        private string _statusText = "Готово";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _grandTotal = "00:00";
        public string GrandTotal
        {
            get => _grandTotal;
            set => SetProperty(ref _grandTotal, value);
        }

        private DepFilterItem _selectedDepartment;
        private bool _isAllExpanded = true; // Сразу ставим true
        public DepFilterItem SelectedDepartment
        {
            get => _selectedDepartment;
            set { if (SetProperty(ref _selectedDepartment, value)) RefreshData(); }
        }

        private bool _isProgramEnabled;
        public bool IsProgramEnabled
        {
            get => _isProgramEnabled;
            set => SetProperty(ref _isProgramEnabled, value);
        }

        private DataView _reportData;
        public DataView ReportData
        {
            get => _reportData;
            set
            {
                // Защита: если пытаемся присвоить ту же самую таблицу, просто выходим
                if (_reportData == value) return;

                // 1. Отписываемся от старой таблицы
                if (_reportData != null)
                    _reportData.Table.ColumnChanged -= OnTableColumnChanged;

                _reportData = value;

                // 2. Подписываемся на новую
                if (_reportData != null)
                    _reportData.Table.ColumnChanged += OnTableColumnChanged;

                OnPropertyChanged(nameof(ReportData));
            }
        }

        private void CalculateTotals(DataRow changedRow, string changedCol, string changedVal)
        {
            if (ReportData == null || ReportData.Table == null) return;

            int rowSum = 0;
            for (int i = 1; i <= 31; i++)
            {
                string colName = $"Day{i}";
                // Если колонка совпадает с измененной, берем новое значение, иначе старое
                string val = (colName == changedCol) ? changedVal : changedRow[colName]?.ToString();
                if (val == "-") continue;
                rowSum += TimeUtils.HHmmToMinutes(val);
            }

            changedRow["Total"] = TimeUtils.MinutesToHHmm(rowSum);

            if (changedRow["RowType"].ToString() == "Work")
            {
                long ephId = Convert.ToInt64(changedRow["EPH_Id"]);
                UpdateEmployeeTotal(ephId, ReportData.Table);
            }
        }

        public void LoadData()
        {
            // Просто вызываем RefreshData, чтобы не плодить одинаковый код
            RefreshData();
        }

        // Выносим обработчик в отдельный метод для чистоты
        private void OnTableColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_isCalculating) return;
            if (e.Row.RowState == DataRowState.Detached) return;

            if (e.Column.ColumnName.StartsWith("Day") && e.Row["RowType"].ToString() == "Work")
            {
                string val = e.ProposedValue?.ToString() ?? "";

                try
                {
                    _isCalculating = true;
                    // ПЕРЕДАЕМ ИМЯ КОЛОНКИ И НОВОЕ ЗНАЧЕНИЕ НАПРЯМУЮ
                    CalculateTotals(e.Row, e.Column.ColumnName, val);
                    SaveCellToDb(e.Row, e.Column.ColumnName, val);
                }
                finally
                {
                    _isCalculating = false;
                }
            }
        }

        private void SaveCellToDb(DataRow row, string columnName, string value)
        {
            try
            {
                // 1. Базовая валидация даты
                int day = int.Parse(columnName.Replace("Day", ""));
                int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth + 1);
                if (day > daysInMonth) return;
                string dateStr = new DateTime(SelectedYear, SelectedMonth + 1, day).ToString("yyyy-MM-dd");

                // 2. Проверка ключей (EPH_Id должен быть обязательно!)
                if (row["EPH_Id"] == DBNull.Value || row["WorkId"] == DBNull.Value) return;

                long ephId = Convert.ToInt64(row["EPH_Id"]);
                long workId = Convert.ToInt64(row["WorkId"]);
                long progId = Convert.ToInt64(row["ProgramId"]);

                // 3. ПАРСИНГ МИНУТ (с жесткой проверкой)
                int minutes = TimeUtils.HHmmToMinutes(value);

                // --- ЛОГ ДЛЯ ТЕБЯ (посмотри его в Output!) ---
                System.Diagnostics.Debug.WriteLine($"ПОПЫТКА СОХРАНЕНИЯ: День={day}, Ввод='{value}', Минуты={minutes}");

                // 4. Если ввели строку, но минуты всё равно 0 (и это не пустая строка), 
                // возможно TimeStringToDouble не справился. 
                // Давай сохранять только если реально есть что сохранять или если надо удалить.

                long tsId = _db.SaveTimesheetEntry(ephId, workId, progId, dateStr, minutes, SelectedYear, SelectedMonth + 1);

                if (row.Table.Columns.Contains("TimesheetId"))
                    row["TimesheetId"] = tsId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ОШИБКА SaveCellToDb: {ex.Message}");
            }
        }

        private void UpdateEmployeeTotal(long ephId, DataTable table)
        {
            var workRows = table.Rows.Cast<DataRow>()
                .Where(r => r.RowState != DataRowState.Deleted && r["RowType"].ToString() == "Work" && Convert.ToInt64(r["EPH_Id"]) == ephId)
                .ToList();

            var employeeRow = table.Rows.Cast<DataRow>()
                .FirstOrDefault(r => r.RowState != DataRowState.Deleted && r["RowType"].ToString() == "Employee" && Convert.ToInt64(r["EPH_Id"]) == ephId);

            if (employeeRow == null) return;

            int year = SelectedYear;
            int month = SelectedMonth + 1;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            DateTime monthStart = new DateTime(year, month, 1);
            DateTime monthEnd = new DateTime(year, month, daysInMonth);

            DateTime activeStart = monthStart;
            if (employeeRow.Table.Columns.Contains("StartDate") && employeeRow["StartDate"] != DBNull.Value && DateTime.TryParse(employeeRow["StartDate"].ToString(), out DateTime sd))
            {
                if (sd > monthStart) activeStart = sd;
            }

            DateTime activeEnd = monthEnd;
            if (employeeRow.Table.Columns.Contains("NextStartDate") && employeeRow["NextStartDate"] != DBNull.Value && DateTime.TryParse(employeeRow["NextStartDate"].ToString(), out DateTime nsd))
            {
                DateTime prevDay = nsd.AddDays(-1);
                if (prevDay < monthEnd) activeEnd = prevDay;
            }

            int empTotal = 0;
            for (int i = 1; i <= 31; i++)
            {
                string colName = $"Day{i}";
                if (i <= daysInMonth)
                {
                    DateTime currentDate = new DateTime(year, month, i);
                    if (currentDate >= activeStart && currentDate <= activeEnd)
                    {
                        int daySum = 0;
                        foreach (var row in workRows)
                        {
                            string val = row[colName]?.ToString();
                            if (val != "-") daySum += TimeUtils.HHmmToMinutes(val);
                        }
                        employeeRow[colName] = TimeUtils.MinutesToHHmm(daySum);
                        empTotal += daySum;
                    }
                    else
                    {
                        employeeRow[colName] = "-";
                    }
                }
                else
                {
                    employeeRow[colName] = "-";
                }
            }
            employeeRow["Total"] = TimeUtils.MinutesToHHmm(empTotal);

            // Дополнительно пересчитаем гранд-итог по всему отделу
            UpdateGrandTotal(table);
        }

        public async System.Threading.Tasks.Task PasteDataAsync(List<Tuple<DataRow, string, string>> changes)
        {
            if (changes == null || changes.Count == 0) return;
            if (ReportData == null || ReportData.Table == null) return;

            try
            {
                IsBusy = true;
                StatusText = $"Вставка ячеек: {changes.Count}...";

                int currentYear = SelectedYear;
                int currentMonth = SelectedMonth + 1;
                int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

                // 1. Готовим DTOшки для бд в UI-потоке (быстро)
                var dtos = new List<TimesheetEntryDto>();
                foreach (var change in changes)
                {
                    DataRow row = change.Item1;
                    string colName = change.Item2;
                    string val = change.Item3;

                    int day = int.Parse(colName.Replace("Day", ""));
                    if (day > daysInMonth) continue;

                    if (row["EPH_Id"] == DBNull.Value || row["WorkId"] == DBNull.Value) continue;

                    dtos.Add(new TimesheetEntryDto
                    {
                        EphId = Convert.ToInt64(row["EPH_Id"]),
                        WorkId = Convert.ToInt64(row["WorkId"]),
                        ProgramId = Convert.ToInt64(row["ProgramId"]),
                        DateStr = new DateTime(currentYear, currentMonth, day).ToString("yyyy-MM-dd"),
                        Minutes = TimeUtils.HHmmToMinutes(val)
                    });
                }

                _isCalculating = true; // Глушим все внутренние события изменений

                // 2. АСИНХРОННО сохраняем всё одной транзакцией
                await System.Threading.Tasks.Task.Run(() => 
                    _db.SaveTimesheetEntriesBulk(dtos, currentYear, currentMonth));

                // 3. В UI-потоке ПРИСВАИВАЕМ значения в DataTable. 
                // Из-за _isCalculating = true события OnTableColumnChanged НЕ сохранят в БД повторно!
                foreach (var change in changes)
                {
                    change.Item1[change.Item2] = change.Item3;
                }

                // 4. АСИНХРОННО пересчитываем ВСЕ итоги
                var table = ReportData.Table;
                await System.Threading.Tasks.Task.Run(() => RecalculateAllTotals(table));

                StatusText = "Вставка успешно завершена";
            }
            catch (Exception ex)
            {
                StatusText = "Ошибка вставки";
                System.Diagnostics.Debug.WriteLine($"Paste error: {ex.Message}");
            }
            finally
            {
                _isCalculating = false;
                IsBusy = false;
            }
        }

        private void UpdateGrandTotal(DataTable table)
        {
            if (table == null)
            {
                GrandTotal = "00:00";
                return;
            }
            var employeeRows = table.Rows.Cast<DataRow>()
                .Where(r => r.RowState != DataRowState.Deleted && r["RowType"].ToString() == "Employee")
                .ToList();

            int grandMins = 0;
            foreach (var empRow in employeeRows)
            {
                grandMins += TimeUtils.HHmmToMinutes(empRow["Total"]?.ToString());
            }
            GrandTotal = TimeUtils.MinutesToHHmm(grandMins);
        }

        public void LoadDepartmentsPreservingSelection()
        {
            // 1. Запоминаем ID текущего выбора
            long? lastId = SelectedDepartment?.Id;

            // 2. Обнуляем выбор, чтобы LoadDepartments понял, что нужно подставить что-то, 
            // если вдруг старый отдел исчезнет из базы
            var oldSelection = SelectedDepartment;

            // 3. Загружаем заново
            LoadDepartments();

            // 4. Пытаемся восстановить выбор по ID
            if (lastId != null)
            {
                var restored = Departments.FirstOrDefault(d => d.Id == lastId);
                if (restored != null)
                {
                    // Используем поле, чтобы не вызвать RefreshData дважды, 
                    // так как в сеттерах Year/Month мы и так вызываем RefreshData() следом
                    _selectedDepartment = restored;
                    OnPropertyChanged(nameof(SelectedDepartment));
                }
            }
        }

        private DataRowView _selectedProgram;
        public DataRowView SelectedProgram
        {
            get => _selectedProgram;
            set
            {
                if (SetProperty(ref _selectedProgram, value))
                {
                    // Как только выбрали другой проект — перекачиваем таблицу
                    RefreshData();
                }
            }
        }

        private int _selectedYear;
        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value))
                {
                    // Сначала пересчитываем счетчики в отделах на новую дату
                    LoadDepartmentsPreservingSelection();
                    LoadActualPrograms();
                    // Затем обновляем саму таблицу
                    RefreshData();
                }
            }
        }

        private int _selectedMonth;
        public int SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetProperty(ref _selectedMonth, value))
                {
                    LoadDepartmentsPreservingSelection();
                    LoadActualPrograms();
                    RefreshData();
                }
            }
        }

        public bool IsAllExpanded
        {
            get => _isAllExpanded;
            set
            {
                if (SetProperty(ref _isAllExpanded, value))
                {
                    if (ReportData != null && ReportData.Table != null)
                    {
                        _isCalculating = true;
                        try
                        {
                            foreach (DataRow row in ReportData.Table.Rows)
                            {
                                if (row.RowState != DataRowState.Deleted)
                                {
                                    if (row["RowType"].ToString() == "Employee" && ReportData.Table.Columns.Contains("IsExpanded"))
                                    {
                                        row["IsExpanded"] = value;
                                    }
                                    else if (row["RowType"].ToString() == "Work" && ReportData.Table.Columns.Contains("IsRowVisible"))
                                    {
                                        row["IsRowVisible"] = value;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            _isCalculating = false;
                        }
                    }
                    ApplyFilter();
                }
            }
        }

        private void ApplyFilter()
        {
            if (ReportData == null) return;

            // Фильтруем на основе флага IsRowVisible. 
            // Это позволяет точечно раскрывать сотрудников, даже если общая кнопка внизу выключена!
            if (ReportData.Table.Columns.Contains("IsRowVisible"))
            {
                ReportData.RowFilter = "IsRowVisible = true";
            }
            else
            {
                ReportData.RowFilter = "";
            }
        }

        private void Table_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_isCalculating) return;

            if (e.Column.ColumnName == "IsExpanded" && e.Row.RowState != DataRowState.Deleted)
            {
                if (e.Row["RowType"].ToString() != "Employee") return;
                if (e.Row["EPH_Id"] == DBNull.Value) return;

                long ephId = Convert.ToInt64(e.Row["EPH_Id"]);
                bool isExpanded = Convert.ToBoolean(e.ProposedValue != DBNull.Value ? e.ProposedValue : true);

                _isCalculating = true;
                try
                {
                    var dt = e.Row.Table;
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row.RowState != DataRowState.Deleted && 
                            row["RowType"].ToString() == "Work" && 
                            row["EPH_Id"] != DBNull.Value && 
                            Convert.ToInt64(row["EPH_Id"]) == ephId)
                        {
                            row["IsRowVisible"] = isExpanded;
                        }
                    }
                }
                finally
                {
                    _isCalculating = false;
                }
                ApplyFilter();
            }
        }

        // Событие для обновления колонок в MainWindow.xaml.cs
        public event Action<int, int> OnMonthChanged;
        public event Action OnDataNeedsRefresh;

        public MainViewModel(DbService db, IDialogService dialogService)
        {
            _db = db;
            _dialogService = dialogService;
            Initialize();
        }



        private void Initialize()
        {
            // 1. Годы
            int currYear = DateTime.Now.Year;
            for (int y = 2025; y <= currYear + 1; y++) Years.Add(y);
            _selectedYear = currYear;
            _selectedMonth = DateTime.Now.Month - 1;

            // 2. Подразделения (сначала грузим структуру)
            LoadDepartments();

            // 3. Актуальные программы (этот метод сам выберет первую и вызовет RefreshData)
            LoadActualPrograms();
        }

        public async System.Threading.Tasks.Task RefreshData()
        {
            if (SelectedDepartment == null || SelectedProgram == null || !IsProgramEnabled)
            {
                ReportData = null;
                GrandTotal = "00:00";
                return;
            }

            try
            {
                StatusText = "Загрузка данных...";
                IsBusy = true;

                var familyIds = GetDepartmentFamilyIds(SelectedDepartment);
                string idsString = string.Join(",", familyIds);
                long progId = Convert.ToInt64(SelectedProgram["Id"]);
                int year = SelectedYear;
                int month = SelectedMonth + 1;

                // Шаг 1: Грузим сырые данные из БД в фоне
                DataTable dt = await Task.Run(() => _db.GetFullTimesheetData(idsString, year, month, progId));

                // Подготавливаем колонки для свертки
                if (!dt.Columns.Contains("IsExpanded")) dt.Columns.Add("IsExpanded", typeof(bool));
                if (!dt.Columns.Contains("IsRowVisible")) dt.Columns.Add("IsRowVisible", typeof(bool));

                foreach (DataRow row in dt.Rows)
                {
                    if (row["RowType"].ToString() == "Employee")
                    {
                        row["IsExpanded"] = IsAllExpanded;
                        row["IsRowVisible"] = true;
                    }
                    else
                    {
                        row["IsExpanded"] = IsAllExpanded;
                        row["IsRowVisible"] = IsAllExpanded;
                    }
                }

                // Подписываемся на изменение состояния раскрытия
                dt.ColumnChanged += Table_ColumnChanged;

                // Шаг 2: Считаем итоги в фоне
                await Task.Run(() => RecalculateAllTotals(dt));

                // Шаг 3: Вернулись в UI, присваиваем
                ReportData = dt.DefaultView;
                ApplyFilter();
                OnMonthChanged?.Invoke(year, month);
                
                StatusText = "Готово";
            }
            catch (Exception ex)
            {
                StatusText = "Ошибка при загрузке данных";
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки данных: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RecalculateAllTotals(DataTable table)
        {
            if (table == null) return;

            _isCalculating = true; // Глушим события БД
            try
            {
                var workRows = table.Rows.Cast<DataRow>().Where(r => r["RowType"].ToString() == "Work").ToList();
                var empRows = table.Rows.Cast<DataRow>().Where(r => r["RowType"].ToString() == "Employee").ToList();

                int year = SelectedYear;
                int month = SelectedMonth + 1;
                int daysInMonth = DateTime.DaysInMonth(year, month);
                DateTime monthStart = new DateTime(year, month, 1);
                DateTime monthEnd = new DateTime(year, month, daysInMonth);

                // Итоги для каждой работы (Итого справа)
                foreach (var workRow in workRows)
                {
                    DateTime activeStart = monthStart;
                    if (workRow.Table.Columns.Contains("StartDate") && workRow["StartDate"] != DBNull.Value && DateTime.TryParse(workRow["StartDate"].ToString(), out DateTime sd))
                    {
                        if (sd > monthStart) activeStart = sd;
                    }

                    DateTime activeEnd = monthEnd;
                    if (workRow.Table.Columns.Contains("NextStartDate") && workRow["NextStartDate"] != DBNull.Value && DateTime.TryParse(workRow["NextStartDate"].ToString(), out DateTime nsd))
                    {
                        DateTime prevDay = nsd.AddDays(-1);
                        if (prevDay < monthEnd) activeEnd = prevDay;
                    }

                    int rowSum = 0;
                    for (int i = 1; i <= 31; i++)
                    {
                        string colName = $"Day{i}";
                        if (i <= daysInMonth)
                        {
                            DateTime currentDate = new DateTime(year, month, i);
                            if (currentDate >= activeStart && currentDate <= activeEnd)
                            {
                                // Активный день. Если значение равно "-", очищаем его
                                if (workRow[colName]?.ToString() == "-")
                                {
                                    workRow[colName] = "";
                                }
                                rowSum += TimeUtils.HHmmToMinutes(workRow[colName]?.ToString());
                            }
                            else
                            {
                                // Неактивный день - ставим прочерк
                                workRow[colName] = "-";
                            }
                        }
                        else
                        {
                            workRow[colName] = "-";
                        }
                    }
                    workRow["Total"] = TimeUtils.MinutesToHHmm(rowSum);
                }

                // Итоги для каждого сотрудника (сверху в его строке)
                foreach (var empRow in empRows)
                {
                    long ephId = Convert.ToInt64(empRow["EPH_Id"]);
                    var myWorkRows = workRows.Where(r => Convert.ToInt64(r["EPH_Id"]) == ephId).ToList();

                    DateTime activeStart = monthStart;
                    if (empRow.Table.Columns.Contains("StartDate") && empRow["StartDate"] != DBNull.Value && DateTime.TryParse(empRow["StartDate"].ToString(), out DateTime sd))
                    {
                        if (sd > monthStart) activeStart = sd;
                    }

                    DateTime activeEnd = monthEnd;
                    if (empRow.Table.Columns.Contains("NextStartDate") && empRow["NextStartDate"] != DBNull.Value && DateTime.TryParse(empRow["NextStartDate"].ToString(), out DateTime nsd))
                    {
                        DateTime prevDay = nsd.AddDays(-1);
                        if (prevDay < monthEnd) activeEnd = prevDay;
                    }

                    int empTotal = 0;
                    for (int i = 1; i <= 31; i++)
                    {
                        string colName = $"Day{i}";
                        if (i <= daysInMonth)
                        {
                            DateTime currentDate = new DateTime(year, month, i);
                            if (currentDate >= activeStart && currentDate <= activeEnd)
                            {
                                int daySum = 0;
                                foreach (var wRow in myWorkRows)
                                {
                                    string val = wRow[colName]?.ToString();
                                    if (val != "-") daySum += TimeUtils.HHmmToMinutes(val);
                                }
                                empRow[colName] = TimeUtils.MinutesToHHmm(daySum);
                                empTotal += daySum;
                            }
                            else
                            {
                                empRow[colName] = "-";
                            }
                        }
                        else
                        {
                            empRow[colName] = "-";
                        }
                    }
                    empRow["Total"] = TimeUtils.MinutesToHHmm(empTotal);
                }

                // Посчитаем гранд-итог
                UpdateGrandTotal(table);
            }
            finally
            {
                _isCalculating = false;
            }
        }

        public void LoadActualPrograms()
        {
            long? lastProgId = SelectedProgram != null ? (long?)Convert.ToInt64(SelectedProgram["Id"]) : null;

            DataTable dt = _db.Programs_List_Actual(SelectedYear, SelectedMonth + 1);
            Programs = dt.DefaultView;
            OnPropertyChanged(nameof(Programs));

            if (dt.Rows.Count > 0)
            {
                IsProgramEnabled = true;

                // Пытаемся восстановить выбор
                DataRowView restored = null;
                if (lastProgId != null)
                {
                    restored = Programs.Cast<DataRowView>().FirstOrDefault(p => Convert.ToInt64(p["Id"]) == lastProgId);
                }

                SelectedProgram = restored ?? Programs[0];
            }
            else
            {
                // ПРОГРАММ НЕТ
                IsProgramEnabled = false;
                _selectedProgram = null;
                OnPropertyChanged(nameof(SelectedProgram));

                // Очищаем таблицу, раз программ нет - часы показывать не для чего
                ReportData = null;
            }
        }

        private List<long> GetDepartmentFamilyIds(DepFilterItem parent)
        {
            var ids = new List<long> { parent.Id };
            foreach (var child in parent.Children)
            {
                ids.AddRange(GetDepartmentFamilyIds(child));
            }
            return ids;
        }

        private void LoadDepartments()
        {
            try
            {
                // 1. Получаем данные через сервис
                var dtDeps = _db.GetDepartmentsTable();
                var empCounts = _db.GetDepartmentsEmployeeCounts(SelectedYear, SelectedMonth + 1);

                // 2. Преобразуем DataTable в список объектов
                var allItems = dtDeps.AsEnumerable().Select(r => new DepFilterItem
                {
                    Id = r.Field<long>("Id"),
                    Name = r.Field<string>("Name"),
                    ParentId = r.Field<long?>("ParentId"),
                    EmpCount = empCounts.ContainsKey(r.Field<long>("Id")) ? empCounts[r.Field<long>("Id")] : 0
                }).ToList();

                // 3. Строим дерево (ОДИН РАЗ!)
                var tree = new List<DepFilterItem>();
                foreach (var item in allItems)
                {
                    if (item.ParentId == null) tree.Add(item);
                    else allItems.FirstOrDefault(i => i.Id == item.ParentId)?.Children.Add(item);
                }

                // --- ПЕРЕСЧЕТ: Суммируем людей по иерархии ---
                foreach (var rootItem in tree)
                {
                    CalculateTotalEmpCount(rootItem);
                }

                // 4. Делаем дерево плоским для ComboBox (используем уже существующее tree)
                var flat = new List<DepFilterItem>();
                FillFlat(tree, flat, "");

                // 5. Заполняем коллекцию для ComboBox
                Departments.Clear();
                foreach (var f in flat) Departments.Add(f);

                // --- ВЫБОР ЭЛЕМЕНТА ---
                var root = Departments.FirstOrDefault(d => d.ParentId == null);

                // Если НИЧЕГО еще не выбрано (первый запуск), выбираем корень
                if (SelectedDepartment == null)
                {
                    SelectedDepartment = root ?? Departments.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(System.Windows.Application.Current.MainWindow, "Ошибка", ex.Message);
            }
        }
        private int CalculateTotalEmpCount(DepFilterItem item)
        {
            // Суммируем: люди в текущем отделе + люди во всех дочерних
            int total = item.EmpCount;
            foreach (var child in item.Children)
            {
                total += CalculateTotalEmpCount(child);
            }

            item.EmpCount = total; // Перезаписываем значение для отображения в скобках
            return total;
        }

        private void FillFlat(List<DepFilterItem> source, List<DepFilterItem> dest, string indent)
        {
            foreach (var item in source)
            {
                if (item.EmpCount > 0)
                {
                    item.DisplayName = indent + item.Name + $" ({item.EmpCount})";
                    dest.Add(item);
                    FillFlat(item.Children, dest, indent + "    ");
                }
            }
        }

        // --- Управление окнами ---
        public void ShowEmployees()
        {
            _dialogService.ShowEmployeesWindow();
            LoadDepartmentsPreservingSelection();
            LoadData();
            OnDataNeedsRefresh?.Invoke();
        }

        public void ShowDepartments()
        {
            _dialogService.ShowDepartmentsWindow();
            LoadDepartmentsPreservingSelection();
        }

        public void ShowPositions()
        {
            _dialogService.ShowPositionsWindow();
        }

        public void ShowWorkTypes()
        {
            _dialogService.ShowWorkTypesWindow();
            LoadData();
            OnDataNeedsRefresh?.Invoke();
        }

        public void ShowPrograms()
        {
            _dialogService.ShowProgramsWindow();
            LoadActualPrograms();
            LoadData();
        }

        public void ShowSignatures()
        {
            _dialogService.ShowEmployeeSignaturesWindow();
        }

        public void ShowReportDept()
        {
            _dialogService.ShowMessage("Отчеты", "В разработке...");
        }

        public void ShowCardReport()
        {
            _dialogService.ShowCardReportWindow(SelectedYear, SelectedMonth);
        }

        public void ShowReportForDepartment()
        {
            _dialogService.ShowDepartmentReportWindow(SelectedYear, SelectedMonth);
        }
        public void ShowReportForDivision()
        {
            _dialogService.ShowDivisionReportWindow(SelectedYear, SelectedMonth);
        }
        public void ShowReportDirectory()
        {
            try
            {
                string reportsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                if (!System.IO.Directory.Exists(reportsDir))
                {
                    System.IO.Directory.CreateDirectory(reportsDir);
                }
                System.Diagnostics.Process.Start("explorer.exe", reportsDir);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Ошибка", $"Не удалось открыть папку: {ex.Message}");
            }
        }

        public async void ConvertReportsToPdf()
        {
            try
            {
                string reportsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                if (!System.IO.Directory.Exists(reportsDir))
                {
                    _dialogService.ShowMessage("Внимание", "Папка с отчетами не найдена.");
                    return;
                }

                var files = System.IO.Directory.GetFiles(reportsDir, "*.xlsx", System.IO.SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    _dialogService.ShowMessage("Внимание", "Excel файлы не найдены в папке Reports.");
                    return;
                }

                IsBusy = true;
                StatusText = $"Начинаю конвертацию {files.Length} файлов...";

                await System.Threading.Tasks.Task.Run(() =>
                {
                    Type excelType = Type.GetTypeFromProgID("Excel.Application");
                    if (excelType == null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            _dialogService.ShowMessage("Ошибка", "Microsoft Excel не установлен на этом компьютере."));
                        return;
                    }

                    dynamic excel = Activator.CreateInstance(excelType);
                    try
                    {
                        excel.Visible = false;
                        excel.DisplayAlerts = false;
                        excel.ScreenUpdating = false;
                        excel.Interactive = false;

                        int count = 0;
                        int converted = 0;
                        foreach (var file in files)
                        {
                            count++;
                            string pdfPath = System.IO.Path.ChangeExtension(file, ".pdf");
                            if (!System.IO.File.Exists(pdfPath))
                            {
                                converted++;
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                    StatusText = $"Конвертация ({count}/{files.Length}): {System.IO.Path.GetFileName(file)}");

                                var workbook = excel.Workbooks.Open(file);
                                try
                                {
                                    // 0 = xlTypePDF, 8-й параметр (false) = OpenAfterPublish
                                    workbook.ExportAsFixedFormat(0, pdfPath, 0, true, false, System.Reflection.Missing.Value, System.Reflection.Missing.Value, false);
                                }
                                finally
                                {
                                    workbook.Close(false);
                                }
                            }
                        }
                    }
                    finally
                    {
                        excel.Interactive = true;
                        excel.ScreenUpdating = true;
                        excel.Quit();
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(excel);
                    }
                });

                StatusText = "Конвертация завершена";
                _dialogService.ShowMessage("Успех", "Все недостающие PDF файлы успешно созданы рядом с оригиналами.");
            }
            catch (Exception ex)
            {
                StatusText = "Ошибка конвертации";
                _dialogService.ShowMessage("Ошибка", $"Не удалось выполнить конвертацию: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ShowAbout() => _dialogService.ShowAboutWindow();

        public void AddWork(DataRowView selectedRow)
        {
            if (selectedRow == null)
            {
                _dialogService.ShowMessage("Внимание", "Сначала выделите сотрудника или его работу!");
                return;
            }
            if (SelectedProgram == null)
            {
                _dialogService.ShowMessage("Внимание", "Не выбрана активная программа!");
                return;
            }

            try
            {
                long ephId = Convert.ToInt64(selectedRow["EPH_Id"]);
                object tsId = selectedRow["TimesheetId"];

                string empName = selectedRow.Row.Table.AsEnumerable()
                    .First(r => r["RowType"].ToString() == "Employee" && Convert.ToInt64(r["EPH_Id"]) == ephId)
                    .Field<string>("FullName");
                string currentProgName = SelectedProgram["ShortName"].ToString();

                var result = _dialogService.ShowAddWorkWindow(empName, currentProgName);
                if (result.success)
                {
                    DataTable dt = ReportData.Table;

                    // Проверка: не добавлена ли уже такая работа этому сотруднику?
                    bool alreadyExists = dt.AsEnumerable().Any(r => 
                        r.RowState != DataRowState.Deleted && 
                        r["RowType"].ToString() == "Work" && 
                        Convert.ToInt64(r["EPH_Id"]) == ephId && 
                        r["WorkId"] != DBNull.Value && 
                        Convert.ToInt64(r["WorkId"]) == result.workId);

                    if (alreadyExists)
                    {
                        _dialogService.ShowMessage("Внимание", "Этот вид работы уже добавлен данному сотруднику!");
                        return;
                    }

                    // Найти WorkCode (нам нужен доступ к ListOfWork)
                    var dtWorks = _db.ListOfWork_List();
                    string wCode = "";
                    foreach (DataRow r in dtWorks.Rows)
                    {
                        if (Convert.ToInt64(r["Id"]) == result.workId)
                        {
                            wCode = r["SpecialCode"].ToString();
                            break;
                        }
                    }


                    // Узнаем, развернут ли родительский сотрудник сейчас
                    bool isParentExpanded = true;
                    var parentEmp = dt.Rows.Cast<DataRow>().FirstOrDefault(r => 
                        r.RowState != DataRowState.Deleted && 
                        r["RowType"].ToString() == "Employee" && 
                        r["EPH_Id"] != DBNull.Value &&
                        Convert.ToInt64(r["EPH_Id"]) == ephId);
                    
                    if (parentEmp != null && dt.Columns.Contains("IsExpanded") && parentEmp["IsExpanded"] != DBNull.Value)
                    {
                        isParentExpanded = Convert.ToBoolean(parentEmp["IsExpanded"]);
                    }

                    DataRow newRow = dt.NewRow();

                    newRow["EPH_Id"] = ephId;
                    newRow["TimesheetId"] = tsId;
                    newRow["RowType"] = "Work";
                    newRow["WorkId"] = result.workId;
                    newRow["ProgramId"] = Convert.ToInt64(SelectedProgram["Id"]);
                    newRow["WorkCode"] = wCode;
                    newRow["FullName"] = "  • " + result.workName;

                    if (dt.Columns.Contains("IsExpanded")) newRow["IsExpanded"] = true;
                    if (dt.Columns.Contains("IsRowVisible")) newRow["IsRowVisible"] = isParentExpanded;

                    if (parentEmp != null)
                    {
                        if (dt.Columns.Contains("StartDate")) newRow["StartDate"] = parentEmp["StartDate"];
                        if (dt.Columns.Contains("NextStartDate")) newRow["NextStartDate"] = parentEmp["NextStartDate"];
                    }

                    int year = SelectedYear;
                    int month = SelectedMonth + 1;
                    int daysInMonth = DateTime.DaysInMonth(year, month);
                    DateTime monthStart = new DateTime(year, month, 1);
                    DateTime monthEnd = new DateTime(year, month, daysInMonth);

                    DateTime activeStart = monthStart;
                    if (parentEmp != null && dt.Columns.Contains("StartDate") && parentEmp["StartDate"] != DBNull.Value && DateTime.TryParse(parentEmp["StartDate"].ToString(), out DateTime sd))
                    {
                        if (sd > monthStart) activeStart = sd;
                    }

                    DateTime activeEnd = monthEnd;
                    if (parentEmp != null && dt.Columns.Contains("NextStartDate") && parentEmp["NextStartDate"] != DBNull.Value && DateTime.TryParse(parentEmp["NextStartDate"].ToString(), out DateTime nsd))
                    {
                        DateTime prevDay = nsd.AddDays(-1);
                        if (prevDay < monthEnd) activeEnd = prevDay;
                    }

                    for (int i = 1; i <= 31; i++)
                    {
                        string colName = $"Day{i}";
                        if (i <= daysInMonth)
                        {
                            DateTime currentDate = new DateTime(year, month, i);
                            if (currentDate >= activeStart && currentDate <= activeEnd)
                            {
                                newRow[colName] = "";
                            }
                            else
                            {
                                newRow[colName] = "-";
                            }
                        }
                        else
                        {
                            newRow[colName] = "-";
                        }
                    }

                    int lastIndex = -1;
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        if (Convert.ToInt64(dt.Rows[i]["EPH_Id"]) == ephId)
                            lastIndex = i;
                    }

                    dt.Rows.InsertAt(newRow, lastIndex + 1);
                    
                    LogService.Log($"РАБОТНИКУ ДОБАВЛЕНА РАБОТА БЕЗ ЧАСОВ: Сотрудник='{empName}', Работа='{result.workName}', Программа='{currentProgName}'");

                    OnDataNeedsRefresh?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Ошибка", ex.Message);
            }
        }

        public void EditWork(DataRowView selectedRow)
        {
            if (selectedRow == null || selectedRow["RowType"].ToString() != "Work")
            {
                _dialogService.ShowMessage("Внимание", "Сначала выделите строку с работой сотрудника!");
                return;
            }

            try
            {
                long tsId = selectedRow["TimesheetId"] != DBNull.Value ? Convert.ToInt64(selectedRow["TimesheetId"]) : 0;
                long oldWorkId = Convert.ToInt64(selectedRow["WorkId"]);
                string oldWorkName = selectedRow["FullName"].ToString().Replace("  • ", "").Trim();

                string empName = selectedRow.Row.Table.AsEnumerable()
                    .First(r => r["RowType"].ToString() == "Employee" && Convert.ToInt64(r["EPH_Id"]) == Convert.ToInt64(selectedRow["EPH_Id"]))
                    .Field<string>("FullName");
                
                string currentProgName = SelectedProgram["ShortName"].ToString();

                var result = _dialogService.ShowAddWorkWindow(empName, currentProgName, oldWorkId);
                if (result.success && result.workId != oldWorkId)
                {
                    if (tsId != 0)
                    {
                        _db.Work_Timesheet_Edit(tsId, oldWorkId, result.workId);
                    }
                    
                    var dtWorks = _db.ListOfWork_List();
                    string wCode = "";
                    foreach (DataRow r in dtWorks.Rows)
                    {
                        if (Convert.ToInt64(r["Id"]) == result.workId)
                        {
                            wCode = r["SpecialCode"].ToString();
                            break;
                        }
                    }

                    selectedRow["WorkId"] = result.workId;
                    selectedRow["WorkCode"] = wCode;
                    selectedRow["FullName"] = "  • " + result.workName;

                    LogService.Log($"ИЗМЕНЕНА РАБОТА СОТРУДНИКА: Сотрудник='{empName}', Программа='{currentProgName}', Было='{oldWorkName}', Стало='{result.workName}'");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Ошибка", ex.Message);
            }
        }

        public void DeleteWork(DataRowView selectedRow)
        {
            if (selectedRow == null || selectedRow["RowType"].ToString() != "Work")
            {
                _dialogService.ShowMessage("Внимание", "Сначала выделите строку с работой сотрудника!");
                return;
            }

            string workName = selectedRow["FullName"].ToString().Replace("  • ", "").Trim();
            bool confirm = _dialogService.ShowConfirmation("Удаление работы", $"Вы уверены, что хотите удалить работу '{workName}' и все часы по ней?");
            if (!confirm) return;

            try
            {
                long tsId = selectedRow["TimesheetId"] != DBNull.Value ? Convert.ToInt64(selectedRow["TimesheetId"]) : 0;
                long workId = Convert.ToInt64(selectedRow["WorkId"]);

                if (tsId != 0)
                {
                    _db.Work_Timesheet_Delete(tsId, workId);
                }

                long ephId = Convert.ToInt64(selectedRow["EPH_Id"]);
                selectedRow.Row.Delete();
                
                // Recalculate employee total
                UpdateEmployeeTotal(ephId, ReportData.Table);

                OnDataNeedsRefresh?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Ошибка", ex.Message);
            }
        }

        public async System.Threading.Tasks.Task ImportDataAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            IsBusy = true;
            StatusText = "Подготовка импорта...";

            try
            {
                DataSet ds = await System.Threading.Tasks.Task.Run(() => 
                {
                    DataSet loadedDs = new DataSet();
                    loadedDs.ReadXml(filePath);
                    return loadedDs;
                });

                if (!ds.Tables.Contains("TimesheetEntries") || ds.Tables["TimesheetEntries"].Rows.Count == 0)
                {
                    Views.MissionMessageBox.Show(System.Windows.Application.Current.MainWindow, "Ошибка импорта", "Файл импорта пуст или имеет неверный формат.");
                    return;
                }

                DataTable entriesTable = ds.Tables["TimesheetEntries"];
                int totalRows = entriesTable.Rows.Count;

                int importYear = DateTime.Now.Year;
                int importMonth = DateTime.Now.Month;
                if (ds.Tables.Contains("MetaData") && ds.Tables["MetaData"].Rows.Count > 0)
                {
                    var meta = ds.Tables["MetaData"].Rows[0];
                    if (meta["TargetYear"] != DBNull.Value) importYear = Convert.ToInt32(meta["TargetYear"]);
                    if (meta["TargetMonth"] != DBNull.Value) importMonth = Convert.ToInt32(meta["TargetMonth"]);
                }

                StatusText = $"Импорт данных ({totalRows} записей)...";

                // 2. ВЫЗЫВАЕМ АТОМАРНЫЙ МЕРДЖ ИЗ DbService! (Никаких locks больше не будет)
                var importResult = await System.Threading.Tasks.Task.Run(() => 
                {
                    return _db.ImportTimesheetDataFlat(entriesTable, importYear, importMonth);
                });

                StatusText = "Импорт завершен.";

                // 3. Формируем красивый отчет
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"Импорт данных за {importMonth:D2}.{importYear} завершен!");
                sb.AppendLine($"Обработано строк: {totalRows}");
                sb.AppendLine($"Успешно внесено: {importResult.ImportedCount}\n");

                bool hasIssues = false;
                if (importResult.MissingEmployees.Count > 0) 
                { 
                    hasIssues = true; 
                    sb.AppendLine("--- НЕ НАЙДЕНЫ СОТРУДНИКИ: ---"); 
                    foreach (var n in importResult.MissingEmployees) sb.AppendLine("• " + n); 
                    sb.AppendLine(); 
                }
                if (importResult.MissingPrograms.Count > 0) 
                { 
                    hasIssues = true; 
                    sb.AppendLine("--- НЕ НАЙДЕНЫ ПРОГРАММЫ: ---"); 
                    foreach (var p in importResult.MissingPrograms) sb.AppendLine("• " + p); 
                    sb.AppendLine(); 
                }
                if (importResult.MissingWorks.Count > 0) 
                { 
                    hasIssues = true; 
                    sb.AppendLine("--- НЕ НАЙДЕНЫ РАБОТЫ: ---"); 
                    foreach (var w in importResult.MissingWorks) sb.AppendLine("• " + w); 
                }

                if (hasIssues) Views.MissionMessageBox.Show(System.Windows.Application.Current.MainWindow, "Результаты импорта с замечаниями", sb.ToString());
                else Views.MissionMessageBox.Show(System.Windows.Application.Current.MainWindow, "Успешный импорт", sb.ToString());

                if (SelectedYear == importYear && SelectedMonth == importMonth)
                {
                    await RefreshData();
                }
            }
            catch (Exception ex)
            {
                Services.LogService.Log("ОШИБКА ИМПОРТА ДАННЫХ:", ex);
                Views.MissionMessageBox.Show(System.Windows.Application.Current.MainWindow, "Ошибка", $"Ошибка при импорте:\n{ex.Message}");
                StatusText = "Ошибка при импорте.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class DepFilterItem
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public long? ParentId { get; set; }
        public int EmpCount { get; set; }
        public List<DepFilterItem> Children { get; set; } = new List<DepFilterItem>();
    }
}