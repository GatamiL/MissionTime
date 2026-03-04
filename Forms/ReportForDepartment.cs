using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MissionTime.Forms
{
    public partial class ReportForDepartment : Form
    {
        private readonly DbService _db;
        private bool _loading;

        // Переменные для хранения данных с главной формы
        private readonly int _initYear;
        private readonly int _initMonth;
        private readonly int _initProgramId;

        // Обновленный конструктор принимает Год, Месяц и Программу
        public ReportForDepartment(DbService db, int year, int month, int programId)
        {
            InitializeComponent();
            _db = db ?? throw new ArgumentNullException(nameof(db));

            _initYear = year;
            _initMonth = month;
            _initProgramId = programId;

            cbYear.SelectedIndexChanged += (s, e) => OnYearMonthChanged();
            cbMonth.SelectedIndexChanged += (s, e) => OnYearMonthChanged();
            cbProgram.SelectedIndexChanged += (s, e) => OnProgramChanged();
            cbDepartment.SelectedIndexChanged += (s, e) => OnDepartmentChanged();

            cbDone.MaxDropDownItems = 15;
            cbChecked.MaxDropDownItems = 15;
        }

        private void ReportForDepartment_Load(object sender, EventArgs e)
        {
            _loading = true;
            try
            {
                LoadDepartments();
                FillYears();
                FillMonths();

                LoadProgramsForSelectedMonth();
                LoadPeopleDoneChecked(); // Загрузили списки и сбросили в пустоту
            }
            finally
            {
                // СНИМАЕМ БЛОКИРОВКУ ДО ФИНАЛЬНОГО ОБНОВЛЕНИЯ
                _loading = false;
            }

            // Теперь форма "официально" загружена.
            // Дергаем смену отдела, чтобы он проверил людей и автоподставил начальника в "Проверил"
            OnDepartmentChanged();
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Собираем параметры с формы
                if (!cbProgram.Enabled || cbProgram.SelectedValue == null)
                    throw new InvalidOperationException("Не выбрана программа.");

                if (!cbPeriod.Enabled || cbPeriod.SelectedItem == null)
                    throw new InvalidOperationException("Не выбран период.");

                long deptId = Convert.ToInt64(cbDepartment.SelectedValue);
                long programId = Convert.ToInt64(cbProgram.SelectedValue);

                // Достаем даты из анонимного объекта через dynamic
                dynamic periodObj = cbPeriod.SelectedItem;
                DateTime periodStart = periodObj.Start;
                DateTime periodEnd = periodObj.End;

                // 2. Формируем пути
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string templatePath = Path.Combine(exeDir, "Templates", "LaborCardTemplate.xlsx");

                var depNames = _db.Department_GetComplexAndDepartmentNames(deptId);
                string periodPart = $"{periodStart:dd.MM}-{periodEnd:dd.MM}.{cbMonth.SelectedValue:D2}.{cbYear.SelectedItem}";

                // --- НОВАЯ УМНАЯ ЛОГИКА КОРОТКИХ ИМЕН ---
                // 1. Вытаскиваем "Отд 10" из длинной каши
                string shortDept = ExcelDepartmentUtils.GetShortDepartmentName(depNames.DepartmentName);

                // 2. Страхуемся от слишком длинного названия программы (режем до 30 символов)
                string progName = cbProgram.Text;
                string shortProg = progName.Length > 30 ? progName.Substring(0, 30).Trim() + ".." : progName;

                // 3. Собираем аккуратное, пуленепробиваемое имя файла
                string fileName = ExcelDepartmentUtils.SanitizeFileName($"{shortDept} - {shortProg} - {periodPart}.xlsx");
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
            // Берем текущий год и месяц с формы (если форма только грузится - берем из конструктора)
            int year = _initYear;
            int month = _initMonth;
            if (cbYear.SelectedItem != null) year = Convert.ToInt32(cbYear.SelectedItem);
            if (cbMonth.SelectedValue != null) month = Convert.ToInt32(cbMonth.SelectedValue);

            var dt = _db.Departments_ListActiveForMonth(year, month);

            cbDepartment.DisplayMember = "DisplayName";
            cbDepartment.ValueMember = "Id";

            if (dt.Rows.Count > 0)
            {
                cbDepartment.DataSource = dt;
                cbDepartment.SelectedIndex = 0;
            }
            else
            {
                // Если активных отделов вообще нет, показываем заглушку
                cbDepartment.DataSource = null;
                cbDepartment.Items.Clear();
                cbDepartment.Items.Add("Нет активных отделов");
                cbDepartment.SelectedIndex = 0;
            }
        }

       private void OnDepartmentChanged()
        {
            if (_loading || cbDepartment.SelectedValue == null) return;

            // Защита: если выбрана текстовая заглушка "Нет активных отделов" (у нее нет Id)
            if (!(cbDepartment.SelectedValue is long deptId)) return;

            btnGenerate.Text = "Сформировать";

            // АВТОПОДСТАНОВКА ОТВЕТСТВЕННОГО (теперь в "Проверил")
            var dtDept = _db.Query("SELECT ResponsibleId FROM Departments WHERE Id = @d",
                new System.Data.SQLite.SQLiteParameter("@d", deptId));

            if (dtDept.Rows.Count > 0 && dtDept.Rows[0]["ResponsibleId"] != DBNull.Value)
            {
                long respId = Convert.ToInt64(dtDept.Rows[0]["ResponsibleId"]);
                cbChecked.SelectedValue = respId; // <-- Заменили cbDone на cbChecked!
            }
            else
            {
                cbChecked.SelectedIndex = -1; // <-- И здесь тоже очищаем cbChecked
            }

            UpdateControlsState();
        }

        private void FillYears()
        {
            int startYear = 2025;
            int cur = DateTime.Now.Year;

            cbYear.Items.Clear();
            for (int y = startYear; y <= cur + 1; y++)
                cbYear.Items.Add(y);

            // Выставляем год, пришедший из главной формы
            cbYear.SelectedItem = cbYear.Items.Contains(_initYear) ? _initYear : cur;
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

            // Выставляем месяц, пришедший из главной формы
            cbMonth.SelectedValue = _initMonth;
        }

        private void OnYearMonthChanged()
        {
            if (_loading) return;

            _loading = true;
            try
            {
                // ВАЖНО: При смене месяца состав "живых" отделов мог поменяться, загружаем заново!
                LoadDepartments();

                LoadProgramsForSelectedMonth();
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

            var dt = _db.Programs_ListForMonth(year, month);

            if (dt == null || dt.Rows.Count == 0)
            {
                SetNoProgramsState();
                return;
            }

            cbProgram.DisplayMember = "ShortName";
            cbProgram.ValueMember = "Id";
            cbProgram.DataSource = dt;
            cbProgram.Enabled = true;

            // Если передана конкретная программа с MainForm - ищем её
            bool foundInitProgram = false;
            if (_initProgramId > 0)
            {
                foreach (DataRowView item in cbProgram.Items)
                {
                    if (Convert.ToInt32(item["Id"]) == _initProgramId)
                    {
                        cbProgram.SelectedItem = item;
                        foundInitProgram = true;
                        break;
                    }
                }
            }

            // Если это "Все программы" (или нужной нет в этом месяце) - берем первую
            if (!foundInitProgram && cbProgram.Items.Count > 0)
            {
                cbProgram.SelectedIndex = 0;
            }

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
            var dt = _db.Employee_List(false);

            cbDone.DisplayMember = "Fio";
            cbDone.ValueMember = "Id"; // Обрати внимание: убедись, что колонка называется Id или EmployeeId
            cbDone.DataSource = dt.Copy();
            cbDone.SelectedIndex = -1;

            cbChecked.DisplayMember = "Fio";
            cbChecked.ValueMember = "Id";
            cbChecked.DataSource = dt.Copy();
            cbChecked.SelectedIndex = -1;

            ConfigureSearchableCombo(cbDone);
            ConfigureSearchableCombo(cbChecked);
        }

        private void RebuildPeriods()
        {
            cbPeriod.DataSource = null;
            cbPeriod.Items.Clear();
            cbPeriod.Enabled = false;

            if (!cbProgram.Enabled || cbYear.SelectedItem == null || cbMonth.SelectedValue == null || cbProgram.SelectedValue == null)
                return;

            int programId = Convert.ToInt32(cbProgram.SelectedValue);
            if (programId <= 0) return;

            int year = Convert.ToInt32(cbYear.SelectedItem);
            int month = Convert.ToInt32(cbMonth.SelectedValue);

            var prog = _db.Program_GetInfo(programId);
            if (!prog.HasValue) return;

            DateTime startDate = prog.Value.Start;

            // --- ИСПОЛЬЗУЕМ НАШ НОВЫЙ КЛАСС PERIOD CALCULATOR ---
            var activeDates = _db.Timesheet_GetActiveDatesForProgram(programId);
            var activeWeeks = PeriodCalculator.GetActiveWeeks(startDate, year, month, activeDates);

            if (activeWeeks.Count == 0) return;

            // Превращаем объекты WorkPeriod в анонимные объекты с полем Display
            var list = activeWeeks.Select(w => new
            {
                Start = w.Start,
                End = w.End,
                Display = $"{w.WeekNum}) {w.Start:dd.MM} - {w.End:dd.MM}"
            }).ToList();

            cbPeriod.DisplayMember = "Display";
            cbPeriod.ValueMember = "Start";
            cbPeriod.DataSource = list;
            cbPeriod.SelectedIndex = 0;
            cbPeriod.Enabled = true;
        }

        private void UpdateControlsState()
        {
            bool hasProgram = cbProgram.Enabled && cbProgram.SelectedValue != null;
            bool hasPeriods = cbPeriod.Items.Count > 0;

            // Кнопка активна только если выбран реальный Id отдела (long)
            bool hasDepartment = cbDepartment.SelectedValue is long;

            cbPeriod.Enabled = hasProgram && hasPeriods;
            cbDone.Enabled = cbPeriod.Enabled;
            cbChecked.Enabled = cbPeriod.Enabled;

            btnGenerate.Enabled = cbPeriod.Enabled && hasDepartment;
        }

        private void ConfigureSearchableCombo(ComboBox cb)
        {
            cb.DropDownStyle = ComboBoxStyle.DropDown;
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