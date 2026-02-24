using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionTime.Forms
{
    public partial class ReportForDepartment : Form
    {
        private readonly DbService _db;
        private bool _loading;
        public ReportForDepartment(DbService db)
        {
            InitializeComponent();
            _db = db;

            cbYear.SelectedIndexChanged += (s, e) => OnYearMonthChanged();
            cbMonth.SelectedIndexChanged += (s, e) => OnYearMonthChanged();
            cbProgram.SelectedIndexChanged += (s, e) => OnProgramChanged();
            cbDone.MaxDropDownItems = 15;
            cbChecked.MaxDropDownItems = 15;
        }

        private void ReportForDepartment_Load(object sender, EventArgs e)
        {
            _loading = true;
            try
            {
                LoadDepartments(); // отделы
                FillYears();
                FillMonths();

                LoadProgramsForSelectedMonth();   // выставит состояния
                LoadPeopleDoneChecked();          // заполнить списки, но активируем позже
                UpdateControlsState();          // включить/выключить по текущим условиям
            }
            finally
            {
                _loading = false;
            }
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Собираем параметры с формы
                if (!cbProgram.Enabled || cbProgram.SelectedValue == null)
                    throw new InvalidOperationException("Не выбрана программа.");

                long deptId = Convert.ToInt64(cbDepartment.SelectedValue);
                long programId = Convert.ToInt64(cbProgram.SelectedValue);

                var periodObj = cbPeriod.SelectedItem;
                DateTime periodStart = (DateTime)periodObj.GetType().GetProperty("Start").GetValue(periodObj);
                DateTime periodEnd = (DateTime)periodObj.GetType().GetProperty("End").GetValue(periodObj);

                // 2. Формируем путь к шаблону и результату
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string templatePath = Path.Combine(exeDir, "Templates", "LaborCardTemplate.xlsx");

                var depNames = _db.Department_GetComplexAndDepartmentNames(deptId);
                string periodPart = $"{periodStart:dd.MM}-{periodEnd:dd.MM}.{cbMonth.SelectedValue:D2}.{cbYear.SelectedItem}";
                string fileName = ExcelDepartmentUtils.SanitizeFileName($"{depNames.DepartmentName} - {cbProgram.Text} - {periodPart}.xlsx");
                string outPath = Path.Combine(exeDir, "Reports", fileName);

                // 3. Вызываем наш мощный утилитный класс
                ExcelDepartmentUtils.GenerateReport(
                    templatePath,
                    outPath,
                    _db,
                    programId,
                    deptId,
                    periodStart,
                    periodEnd,
                    cbDone.Text?.Trim(),
                    cbChecked.Text?.Trim()
                );

                // Показываем красивое окно с выбором действий
                ReportResultDialog.Show(outPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void LoadDepartments()
        {
            cbDepartment.DisplayMember = "DisplayName";
            cbDepartment.ValueMember = "Id";

            // Тут подставь свой метод "только отделы"
            // например: _db.Departments_ListOnlyLevel3()
            cbDepartment.DataSource = _db.Departments_ListOnlyLevel3();

            if (cbDepartment.Items.Count > 0)
                cbDepartment.SelectedIndex = 0;
        }
        private void FillYears()
        {
            int startYear = 2025;
            int cur = DateTime.Now.Year;

            cbYear.Items.Clear();
            for (int y = startYear; y <= cur; y++)
                cbYear.Items.Add(y);

            cbYear.SelectedItem = cur;
        }
        private void FillMonths()
        {
            cbMonth.DisplayMember = "Text";
            cbMonth.ValueMember = "Value";
            cbMonth.DataSource = new[]
            {
                new { Text="Январь", Value=1 }, new { Text="Февраль", Value=2 }, new { Text="Март", Value=3 },
                new { Text="Апрель", Value=4 }, new { Text="Май", Value=5 }, new { Text="Июнь", Value=6 },
                new { Text="Июль", Value=7 }, new { Text="Август", Value=8 }, new { Text="Сентябрь", Value=9 },
                new { Text="Октябрь", Value=10 }, new { Text="Ноябрь", Value=11 }, new { Text="Декабрь", Value=12 },
            };
            cbMonth.SelectedValue = DateTime.Now.Month;
        }
        private void OnYearMonthChanged()
        {
            if (_loading) return;

            _loading = true;
            try
            {
                LoadProgramsForSelectedMonth(); // это же обновит периоды и активность
                UpdateControlsState();
            }
            finally
            {
                _loading = false;
            }
        }
        private void LoadProgramsForSelectedMonth()
        {
            cbProgram.DataSource = null;
            cbProgram.Items.Clear();

            cbPeriod.DataSource = null;
            cbPeriod.Items.Clear();

            if (cbYear.SelectedItem == null || cbMonth.SelectedValue == null)
            {
                SetNoProgramsState();
                return;
            }

            int year = Convert.ToInt32(cbYear.SelectedItem);
            int month = Convert.ToInt32(cbMonth.SelectedValue);

            var dt = _db.Programs_ListForMonth(year, month); // Id, ShortName

            if (dt == null || dt.Rows.Count == 0)
            {
                SetNoProgramsState();
                return;
            }

            cbProgram.DisplayMember = "ShortName";
            cbProgram.ValueMember = "Id";
            cbProgram.DataSource = dt;
            cbProgram.Enabled = true;
            cbProgram.SelectedIndex = 0;

            RebuildPeriods();
            UpdateControlsState();
        }
        private void OnProgramChanged()
        {
            if (_loading) return;

            _loading = true;
            try
            {
                RebuildPeriods();
                UpdateControlsState();
            }
            finally
            {
                _loading = false;
            }
        }
        private void SetNoProgramsState()
        {
            cbProgram.Items.Clear();
            cbProgram.Items.Add("Нет программ");
            cbProgram.SelectedIndex = 0;
            cbProgram.Enabled = false;

            cbPeriod.DataSource = null;
            cbPeriod.Items.Clear();

            cbDone.Enabled = false;
            cbChecked.Enabled = false;
            btnGenerate.Enabled = false;
        }
        private void LoadPeopleDoneChecked()
        {
            // подставь свой метод: Id/Fio
            var dt = _db.Employee_List(false);

            cbDone.DisplayMember = "Fio";
            cbDone.ValueMember = "EmployeeId";
            cbDone.DataSource = dt.Copy();
            if (cbDone.Items.Count > 0) cbDone.SelectedIndex = 0;

            cbChecked.DisplayMember = "Fio";
            cbChecked.ValueMember = "EmployeeId";
            cbChecked.DataSource = dt.Copy();
            if (cbChecked.Items.Count > 0) cbChecked.SelectedIndex = 0;

            ConfigureSearchableCombo(cbDone);
            ConfigureSearchableCombo(cbChecked);
        }
        private void RebuildPeriods()
        {
            cbPeriod.DataSource = null;
            cbPeriod.Items.Clear();
            cbPeriod.Enabled = false;

            if (!cbProgram.Enabled) return;
            if (cbYear.SelectedItem == null || cbMonth.SelectedValue == null) return;
            if (cbProgram.SelectedValue == null || cbProgram.SelectedValue == DBNull.Value) return;

            int programId = Convert.ToInt32(cbProgram.SelectedValue);
            if (programId <= 0) return;

            int year = Convert.ToInt32(cbYear.SelectedItem);
            int month = Convert.ToInt32(cbMonth.SelectedValue);

            var prog = _db.Programs_GetById(programId);
            if (!prog.HasValue) return;

            DateTime monthStart = new DateTime(year, month, 1);
            DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);

            DateTime programStart = prog.Value.StartDate.Date;
            DateTime programEnd = prog.Value.EndDate.Date;

            // пересечение программа × месяц
            DateTime start = programStart > monthStart ? programStart : monthStart;
            DateTime end = programEnd < monthEnd ? programEnd : monthEnd;
            if (start > end) return;

            // --- 1) Получаем все недели программы, где реально есть часы ---
            var allWeeksDt = _db.TimesheetEntry_WeeksWithHoursForProgram(programId);

            var weekNo = new Dictionary<DateTime, int>();
            int n = 0;

            foreach (DataRow r in allWeeksDt.Rows)
            {
                var s = Convert.ToString(r["WeekMonday"]);
                if (string.IsNullOrWhiteSpace(s)) continue;

                DateTime monday = DateTime.Parse(s).Date;
                n++;
                weekNo[monday] = n;
            }

            if (weekNo.Count == 0)
                return;

            // --- 2) Строим периоды строго по 7 дней (Понедельник - Воскресенье) ---
            var list = new List<object>();

            // Вычисляем понедельник для самой первой даты (start)
            DateTime firstMonday = start.AddDays(-(((int)start.DayOfWeek + 6) % 7)).Date;

            DateTime currentStart = firstMonday;

            // Шагаем полными неделями, пока начало недели не перешагнет конец месяца
            while (currentStart <= end)
            {
                DateTime currentEnd = currentStart.AddDays(6); // Всегда воскресенье (+6 дней)

                // Если эта неделя есть в базе (по её понедельнику), добавляем в список
                if (weekNo.TryGetValue(currentStart, out int weekNumber))
                {
                    list.Add(new
                    {
                        Start = currentStart,
                        End = currentEnd,
                        Display = $"{weekNumber}) {currentStart:dd.MM} - {currentEnd:dd.MM}"
                    });
                }

                // Переходим к следующему понедельнику
                currentStart = currentEnd.AddDays(1);
            }

            if (list.Count == 0)
                return;

            cbPeriod.DisplayMember = "Display";
            cbPeriod.ValueMember = "Start";
            cbPeriod.DataSource = list;
            cbPeriod.SelectedIndex = 0;
            cbPeriod.Enabled = true;
        }
        private void UpdateControlsState()
        {
            bool hasProgram = cbProgram.Enabled &&
                              cbProgram.SelectedValue != null &&
                              cbProgram.SelectedValue != DBNull.Value;

            cbPeriod.Enabled = hasProgram && cbPeriod.Items.Count > 0;
            cbDone.Enabled = cbPeriod.Enabled;
            cbChecked.Enabled = cbPeriod.Enabled;
            btnGenerate.Enabled = cbPeriod.Enabled;
        }
        private void ConfigureSearchableCombo(ComboBox cb)
        {
            cb.DropDownStyle = ComboBoxStyle.DropDown; // обязательно не DropDownList
            cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cb.AutoCompleteSource = AutoCompleteSource.ListItems;
        }
    }
    public static class ReportResultDialog
    {
        public static void Show(string filePath)
        {
            // Создаем новую форму (окошко) "на лету"
            Form form = new Form();
            form.Text = "Mission Time";
            form.Size = new System.Drawing.Size(420, 160);
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowIcon = false;

            // Текст сообщения
            Label label = new Label();
            label.Text = $"Отчет успешно сформирован!\n\nФайл: {System.IO.Path.GetFileName(filePath)}";
            label.Location = new System.Drawing.Point(20, 20);
            label.AutoSize = true;
            label.MaximumSize = new System.Drawing.Size(360, 0); // Чтобы длинное имя переносилось

            // Кнопка 1: Открыть отчет
            Button btnOpenReport = new Button();
            btnOpenReport.Text = "Открыть отчет";
            btnOpenReport.Size = new System.Drawing.Size(110, 35);
            btnOpenReport.Location = new System.Drawing.Point(20, 75);
            btnOpenReport.Click += (s, e) => {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                    form.Close();
                }
                catch { MessageBox.Show("Не удалось открыть файл Excel."); }
            };

            // Кнопка 2: Открыть папку
            Button btnOpenFolder = new Button();
            btnOpenFolder.Text = "Открыть папку";
            btnOpenFolder.Size = new System.Drawing.Size(110, 35);
            btnOpenFolder.Location = new System.Drawing.Point(140, 75);
            btnOpenFolder.Click += (s, e) => {
                try
                {
                    string folder = System.IO.Path.GetDirectoryName(filePath);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true, Verb = "open" });
                    form.Close();
                }
                catch { MessageBox.Show("Не удалось открыть папку."); }
            };

            // Кнопка 3: Закрыть
            Button btnClose = new Button();
            btnClose.Text = "Закрыть";
            btnClose.Size = new System.Drawing.Size(110, 35);
            btnClose.Location = new System.Drawing.Point(260, 75);
            btnClose.Click += (s, e) => form.Close();

            // Добавляем всё на форму
            form.Controls.Add(label);
            form.Controls.Add(btnOpenReport);
            form.Controls.Add(btnOpenFolder);
            form.Controls.Add(btnClose);

            // Показываем окно
            form.ShowDialog();
        }
    }
}
