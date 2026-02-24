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
    public partial class ReportForComplex : Form
    {
        private readonly DbService _db;
        private bool _loading;
        public ReportForComplex(DbService db)
        {
            InitializeComponent();
            _db = db ?? throw new ArgumentNullException(nameof(db));

            // Подписываемся на события изменения фильтров
            cbYear.SelectedIndexChanged += (s, e) => OnYearMonthChanged();
            cbMonth.SelectedIndexChanged += (s, e) => OnYearMonthChanged();
            cbProgram.SelectedIndexChanged += (s, e) => OnProgramChanged();
        }

        private void ReportForComplex_Load(object sender, EventArgs e)
        {
            _loading = true;
            try
            {
                LoadComplexes(); // Загружаем список комплексов
                FillYears();
                FillMonths();

                LoadProgramsForSelectedMonth(); // Загрузит программы и обновит периоды
                UpdateControlsState();          // Включит/выключит кнопки
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
                btnGenerate.Enabled = false;
                this.Cursor = Cursors.WaitCursor;

                long complexId = Convert.ToInt64(cbComplex.SelectedValue);
                long programId = Convert.ToInt64(cbProgram.SelectedValue);
                string complexName = cbComplex.Text;

                var periodObj = cbPeriod.SelectedItem;
                DateTime periodStart = (DateTime)periodObj.GetType().GetProperty("Start").GetValue(periodObj);
                DateTime periodEnd = (DateTime)periodObj.GetType().GetProperty("End").GetValue(periodObj);

                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string templatePath = Path.Combine(exeDir, "Templates", "GeneralReportTemplate.xlsx");

                var depNames = _db.Department_GetComplexAndDepartmentNames(complexId);
                string periodPart = $"{periodStart:dd.MM}-{periodEnd:dd.MM}.{cbMonth.SelectedValue:D2}.{cbYear.SelectedItem}";
                string fileName = ExcelComplexUtils.SanitizeFileName($"{depNames.ComplexName} - {cbProgram.Text} - {periodPart}.xlsx");
                string outPath = Path.Combine(exeDir, "Reports", fileName);

                ExcelComplexUtils.GenerateReport(
                    templatePath,
                    outPath,
                    _db,
                    programId,
                    complexId,
                    complexName,
                    periodStart,
                    periodEnd
                );

                // Тот самый наш красивый диалог!
                ReportResultDialog.Show(outPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка генерации", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGenerate.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        #region Загрузка данных (Справочники)

        private void LoadComplexes()
        {
            cbComplex.DisplayMember = "DisplayName";
            cbComplex.ValueMember = "Id";

            // Вызываем наш новый метод для Level = 2
            cbComplex.DataSource = _db.Departments_ListOnlyLevel2();

            if (cbComplex.Items.Count > 0)
                cbComplex.SelectedIndex = 0;
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

        #endregion

        #region Логика каскадного обновления фильтров

        private void OnYearMonthChanged()
        {
            if (_loading) return;

            _loading = true;
            try
            {
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
            cbPeriod.Enabled = false;

            btnGenerate.Enabled = false;
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

            // Пересечение программы с месяцем
            DateTime start = programStart > monthStart ? programStart : monthStart;
            DateTime end = programEnd < monthEnd ? programEnd : monthEnd;
            if (start > end) return;

            // --- 1) Получаем недели, где реально есть данные в TimesheetEntry ---
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

            if (weekNo.Count == 0) return;

            // --- 2) Строим ровные периоды по 7 дней ---
            var list = new List<object>();

            DateTime firstMonday = start.AddDays(-(((int)start.DayOfWeek + 6) % 7)).Date;
            DateTime currentStart = firstMonday;

            while (currentStart <= end)
            {
                DateTime currentEnd = currentStart.AddDays(6);

                if (weekNo.TryGetValue(currentStart, out int weekNumber))
                {
                    list.Add(new
                    {
                        Start = currentStart,
                        End = currentEnd,
                        Display = $"{weekNumber}) {currentStart:dd.MM} - {currentEnd:dd.MM}"
                    });
                }
                currentStart = currentEnd.AddDays(1);
            }

            if (list.Count == 0) return;

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
            btnGenerate.Enabled = cbPeriod.Enabled;
        }

        #endregion
    }
}
