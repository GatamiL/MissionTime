using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MissionTime.Forms
{
    public partial class MainForm : Form
    {
        private readonly DbService _db;
        private bool _isMainLoading;

        private string _quickSearchText = "";
        private Timer _quickSearchTimer;
        public MainForm(string dbPath)
        {
            InitializeComponent();

            _db = new DbService();
            _db.Connect(dbPath);

            _quickSearchTimer = new Timer();
            _quickSearchTimer.Interval = 1500; // 1.5 секунды тишины — и буфер очищается
            _quickSearchTimer.Tick += (s, e) =>
            {
                _quickSearchText = "";
                _quickSearchTimer.Stop();
            };
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _isMainLoading = true;
            try
            {
                FillYears();
                FillMonths();

                LoadProgramsComboForSelectedPeriod(); // внутри сам загрузит департаменты

                BuildMainGridColumns();
                ReloadMainGrid();
            }
            finally
            {
                _isMainLoading = false;
            }

            cbDepartment.SelectedIndexChanged += (s, ea) =>
            {
                if (_isMainLoading) return;
                ReloadMainGrid();
            };

            cbYear.SelectedIndexChanged += (s, ea) => PeriodChanged();
            cbMonth.SelectedIndexChanged += (s, ea) => PeriodChanged();

            cbProgram.SelectedIndexChanged += (s, ea) =>
            {
                if (_isMainLoading) return;
                ReloadMainGrid();
            };
        }
        #region Обработка событий Menu
        private void menuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        private void menuEmployees_Click(object sender, EventArgs e)
        {
            using (var f = new Employee(_db))
            {
                f.Icon = this.Icon;
                f.ShowDialog(this);
            }    

            long? prevDepId = (cbDepartment.SelectedValue == null || cbDepartment.SelectedValue == DBNull.Value)
                ? (long?)null
                : Convert.ToInt64(cbDepartment.SelectedValue);

            int year = Convert.ToInt32(cbYear.SelectedItem);
            int month = Convert.ToInt32(cbMonth.SelectedValue);

            LoadDepartmentsCombo(year, month);

            if (prevDepId.HasValue)
                cbDepartment.SelectedValue = prevDepId.Value;

            ReloadMainGrid();
        }
        private void menuDepartments_Click(object sender, EventArgs e)
        {
            long? prevDepId = (cbDepartment.SelectedValue == null || cbDepartment.SelectedValue == DBNull.Value)
    ? (long?)null
    : Convert.ToInt64(cbDepartment.SelectedValue);

            using (var f = new Departments(_db))
            {
                f.Icon = this.Icon;
                f.ShowDialog(this);
            }

            int year = Convert.ToInt32(cbYear.SelectedItem);
            int month = Convert.ToInt32(cbMonth.SelectedValue);

            LoadDepartmentsCombo(year, month);

            if (prevDepId.HasValue)
                cbDepartment.SelectedValue = prevDepId.Value;

            ReloadMainGrid();
        }
        private void menuEmployeePosition_Click(object sender, EventArgs e)
        {
            EmployeePositions form = new EmployeePositions(_db);
            form.Icon = this.Icon;
            form.ShowDialog(this);
        }
        private void menuListOfWork_Click(object sender, EventArgs e)
        {
            ListOfWork form = new ListOfWork(_db);
            form.Icon = this.Icon;
            form.ShowDialog(this);
        }
        private void menuListOfPrograms_Click(object sender, EventArgs e)
        {
            int prevProgramId = 0;
            if (cbProgram.SelectedValue != null && cbProgram.SelectedValue != DBNull.Value)
                prevProgramId = Convert.ToInt32(cbProgram.SelectedValue);

            using (var f = new ListOfProgram(_db))
            {
                f.Icon = this.Icon;
                f.ShowDialog(this);
            }

            LoadProgramsComboForSelectedPeriod();

            if (prevProgramId > 0)
                cbProgram.SelectedValue = prevProgramId;

            ReloadMainGrid();
        }
        private void menuReportForDpartment_Click(object sender, EventArgs e)
        {
            ReportForDepartment form = new ReportForDepartment(_db);
            form.Icon = this.Icon;
            form.ShowDialog(this);
        }
        private void menuReportForDivision_Click(object sender, EventArgs e)
        {
            using (var f = new ReportForComplex(_db))
            {
                f.Icon = this.Icon;
                f.ShowDialog(this);
            }
        }
        private void menuAbout_Click(object sender, EventArgs e)
        {
            AboutDialog.Show();
        }
        #endregion
        private void BuildMainGridColumns()
        {
            dgvMain.AutoGenerateColumns = false;
            dgvMain.AllowUserToAddRows = false;
            dgvMain.AllowUserToDeleteRows = false;
            dgvMain.AllowUserToResizeColumns = false;
            dgvMain.AllowUserToResizeRows = false;
            dgvMain.MultiSelect = false;
            dgvMain.ReadOnly = true;
            dgvMain.RowHeadersVisible = false;
            dgvMain.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMain.CellDoubleClick += dgvMain_CellDoubleClick;
            dgvMain.KeyPress += DgvMain_KeyPress;
            dgvMain.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvMain.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            EnableDoubleBuffering(dgvMain);

            dgvMain.Columns.Clear();
            dgvMain.Columns.Add(new DataGridViewTextBoxColumn{ Name = "EPHId", DataPropertyName = "EPHId", Visible = false, Frozen = true });
            dgvMain.Columns.Add(new DataGridViewTextBoxColumn { Name = "Department", HeaderText = "Подразделение", DataPropertyName = "Department", Frozen = true, Visible = false });
            dgvMain.Columns.Add(new DataGridViewTextBoxColumn { Name = "Position", HeaderText = "Должность", DataPropertyName = "Position", Frozen = true, Visible = false });
            dgvMain.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeId", DataPropertyName = "EmployeeId", Visible = false, Frozen = true });
            dgvMain.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fio", HeaderText = "Сотрудник", DataPropertyName = "Fio", Frozen = true, Width = 150 });
            for (int i = 1; i <= 31; i++)
            {
                var col = new DataGridViewTextBoxColumn { Name = "D" + i, HeaderText = i.ToString(), Width = 50, DataPropertyName = "D" + i };
                dgvMain.Columns.Add(col);
            }
            dgvMain.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalProgram", HeaderText = "Всего по программе", DataPropertyName = "TotalProgram", Width = 110 });
            dgvMain.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalAll", HeaderText = "Всего", DataPropertyName = "TotalAll", Width = 90 });

            foreach (DataGridViewColumn col in dgvMain.Columns)
            {
                if (col.Name.StartsWith("D") || col.Name.StartsWith("Total"))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
            }
        }
        private void ReloadMainGrid()
        {
            if (cbDepartment.SelectedValue == null) return;
            if (cbYear.SelectedItem == null) return;
            if (cbMonth.SelectedValue == null) return;

            long depId = Convert.ToInt64(cbDepartment.SelectedValue);
            int year = Convert.ToInt32(cbYear.SelectedItem);
            int month = Convert.ToInt32(cbMonth.SelectedValue);

            UpdateColumnsVisibility(year, month);

            if (cbProgram.SelectedValue != null && Convert.ToInt32(cbProgram.SelectedValue) == -1)
            {
                dgvMain.DataSource = null;
                return;
            }

            int programId = 0;
            if (cbProgram.SelectedValue != null && cbProgram.SelectedValue != DBNull.Value)
                programId = Convert.ToInt32(cbProgram.SelectedValue);

            int daysInMonth = DateTime.DaysInMonth(year, month);

            // 1) сегменты назначений в месяце (внутри выбранного подразделения + дочерние)
            // ОБЯЗАТЕЛЬНО: вернуть EPHId + SegStart/SegEnd (обрезанные по месяцу)
            var dtSeg = _db.Employee_AssignmentsForMonth(depId, year, month, includeFired: false);

            // 2) минуты по дням (лучше с ключом EPHId)
            var dtMin = _db.TimesheetMinutes_ByEphDay(depId, year, month, programId);

            // map: EPHId|day -> (all, prog)
            var map = new Dictionary<string, (int all, int prog)>();
            foreach (DataRow r in dtMin.Rows)
            {
                long ephId = Convert.ToInt64(r["EPHId"]);
                DateTime d = DateTime.Parse(r["WorkDate"].ToString()).Date;

                int all = Convert.ToInt32(r["MinAll"]);
                int prog = Convert.ToInt32(r["MinProg"]);

                map[ephId + "|" + d.Day] = (all, prog);
            }

            var outDt = new DataTable();
            outDt.Columns.Add("EmployeeId", typeof(long));
            outDt.Columns.Add("DepartmentId", typeof(long));
            outDt.Columns.Add("PositionId", typeof(long));
            outDt.Columns.Add("EPHId", typeof(long));       // <-- добавили
            outDt.Columns.Add("SegStart", typeof(string));  // yyyy-MM-dd
            outDt.Columns.Add("SegEnd", typeof(string));    // yyyy-MM-dd

            outDt.Columns.Add("Fio", typeof(string));
            outDt.Columns.Add("Department", typeof(string));
            outDt.Columns.Add("Position", typeof(string));

            for (int i = 1; i <= 31; i++) outDt.Columns.Add("D" + i, typeof(string));
            outDt.Columns.Add("TotalProgram", typeof(string));
            outDt.Columns.Add("TotalAll", typeof(string));

            // (опционально) сортировка: ФИО, потом SegStart
            // если dtSeg уже отсортирован в SQL — можно не трогать

            foreach (DataRow s in dtSeg.Rows)
            {
                long ephId = Convert.ToInt64(s["EPHId"]);
                long eid = Convert.ToInt64(s["EmployeeId"]);
                long did = Convert.ToInt64(s["DepartmentId"]);
                long pid = Convert.ToInt64(s["PositionId"]);

                string fio = Convert.ToString(s["Fio"]);
                string depName = Convert.ToString(s["DepartmentName"]);
                string posName = Convert.ToString(s["PositionName"]);

                DateTime segStart = DateTime.Parse(Convert.ToString(s["SegStart"]));
                DateTime segEnd = DateTime.Parse(Convert.ToString(s["SegEnd"]));

                int totalAll = 0;
                int totalProg = 0;

                var row = outDt.NewRow();
                row["EmployeeId"] = eid;
                row["DepartmentId"] = did;
                row["PositionId"] = pid;
                row["EPHId"] = ephId;
                row["SegStart"] = segStart.ToString("yyyy-MM-dd");
                row["SegEnd"] = segEnd.ToString("yyyy-MM-dd");

                row["Fio"] = fio;
                row["Department"] = depName;
                row["Position"] = posName;

                for (int day = 1; day <= 31; day++)
                {
                    if (day > daysInMonth)
                    {
                        row["D" + day] = "";
                        continue;
                    }

                    DateTime cur = new DateTime(year, month, day);

                    // вне отрезка назначения -> "-"
                    if (cur < segStart || cur > segEnd)
                    {
                        row["D" + day] = "-";
                        continue;
                    }

                    // внутри отрезка -> часы
                    string key = ephId + "|" + day;

                    int all = 0, prog = 0;
                    if (map.TryGetValue(key, out var v))
                    {
                        all = v.all;
                        prog = v.prog;
                    }

                    int cellMin = (programId == 0) ? all : prog;
                    row["D" + day] = TimeUtils.MinutesToHHmm(cellMin);

                    totalAll += all;
                    totalProg += prog;
                }

                row["TotalProgram"] = TimeUtils.MinutesToHHmm(totalProg);
                row["TotalAll"] = TimeUtils.MinutesToHHmm(totalAll);

                outDt.Rows.Add(row);
            }

            dgvMain.DataSource = outDt;

            if (dgvMain.Columns["EmployeeId"] != null) dgvMain.Columns["EmployeeId"].Visible = false;
            if (dgvMain.Columns["DepartmentId"] != null) dgvMain.Columns["DepartmentId"].Visible = false;
            if (dgvMain.Columns["PositionId"] != null) dgvMain.Columns["PositionId"].Visible = false;
            if (dgvMain.Columns["EPHId"] != null) dgvMain.Columns["EPHId"].Visible = false;
            if (dgvMain.Columns["SegStart"] != null) dgvMain.Columns["SegStart"].Visible = false;
            if (dgvMain.Columns["SegEnd"] != null) dgvMain.Columns["SegEnd"].Visible = false;
        }
        private static void EnableDoubleBuffering(DataGridView dgv)
        {
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                dgv,
                new object[] { true });
        }
        private void dgvMain_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvMain.CurrentRow == null) return;

            int programId = 0;
            if (cbProgram.SelectedValue != null && cbProgram.SelectedValue != DBNull.Value)
                programId = Convert.ToInt32(cbProgram.SelectedValue);

            if (programId == 0)
            {
                MessageBox.Show("Выберите конкретную программу.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RememberAndRestoreMainGrid(() =>
            {
                long employeeId = Convert.ToInt64(dgvMain.CurrentRow.Cells["EmployeeId"].Value);
                long ephId = Convert.ToInt64(dgvMain.CurrentRow.Cells["EPHId"].Value);   // ← ВОТ ОН

                string fio = Convert.ToString(dgvMain.CurrentRow.Cells["Fio"].Value);

                int year = Convert.ToInt32(cbYear.SelectedItem);
                int month = Convert.ToInt32(cbMonth.SelectedValue);

                using (var f = new MainForm_edit(_db, ephId, year, month, programId))
                {
                    f.Icon = this.Icon;
                    f.ShowDialog(this);
                }

                ReloadMainGrid();
            });
        }
        private void UpdateColumnsVisibility(int year, int month)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);

            for (int i = 1; i <= 31; i++)
            {
                dgvMain.Columns["D" + i].Visible = (i <= daysInMonth);

                if (i <= daysInMonth)
                {
                    DateTime date = new DateTime(year, month, i);
                    dgvMain.Columns["D" + i].HeaderText = i + "\n" + TimeUtils.GetDowRu2(date.DayOfWeek);
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                        dgvMain.Columns["D" + i].DefaultCellStyle.BackColor = Color.LightGray;
                    else
                        dgvMain.Columns["D" + i].DefaultCellStyle.BackColor = Color.White;
                }
            }
        }
        private void RememberAndRestoreMainGrid(Action action)
        {
            long? selectedEphId = null;
            long? selectedEmployeeId = null;
            int firstDisplayedRow = -1;
            int selectedColIndex = -1;

            if (dgvMain.CurrentRow != null)
            {
                if (dgvMain.Columns.Contains("EPHId"))
                {
                    var v = dgvMain.CurrentRow.Cells["EPHId"].Value;
                    if (v != null && v != DBNull.Value)
                        selectedEphId = Convert.ToInt64(v);
                }

                if (!selectedEphId.HasValue && dgvMain.Columns.Contains("EmployeeId"))
                {
                    var v = dgvMain.CurrentRow.Cells["EmployeeId"].Value;
                    if (v != null && v != DBNull.Value)
                        selectedEmployeeId = Convert.ToInt64(v);
                }
            }

            if (dgvMain.FirstDisplayedScrollingRowIndex >= 0)
                firstDisplayedRow = dgvMain.FirstDisplayedScrollingRowIndex;

            if (dgvMain.CurrentCell != null)
                selectedColIndex = dgvMain.CurrentCell.ColumnIndex;

            action();

            // 1) сначала пытаемся вернуть по EPHId (самое точное)
            if (selectedEphId.HasValue)
                SelectRowByEphId(selectedEphId.Value, selectedColIndex);
            // 2) fallback по EmployeeId (если EPHId вдруг нет)
            else if (selectedEmployeeId.HasValue)
                SelectRowByEmployeeId(selectedEmployeeId.Value, selectedColIndex);

            if (firstDisplayedRow >= 0 && firstDisplayedRow < dgvMain.Rows.Count)
            {
                try { dgvMain.FirstDisplayedScrollingRowIndex = firstDisplayedRow; }
                catch { }
            }
        }
        private void SelectRowByEphId(long ephId, int preferredColIndex)
        {
            if (!dgvMain.Columns.Contains("EPHId")) return;

            foreach (DataGridViewRow r in dgvMain.Rows)
            {
                if (r.IsNewRow) continue;

                var v = r.Cells["EPHId"].Value;
                if (v == null || v == DBNull.Value) continue;

                if (Convert.ToInt64(v) == ephId)
                {
                    r.Selected = true;

                    int colIndex = (preferredColIndex >= 0 && preferredColIndex < dgvMain.ColumnCount)
                        ? preferredColIndex
                        : dgvMain.Columns["Fio"]?.Index ?? 0;

                    dgvMain.CurrentCell = r.Cells[colIndex];
                    return;
                }
            }
        }
        private void SelectRowByEmployeeId(long employeeId, int preferredColIndex)
        {
            foreach (DataGridViewRow r in dgvMain.Rows)
            {
                if (r.DataBoundItem == null) continue;

                var v = r.Cells["EmployeeId"].Value;
                if (v == null || v == DBNull.Value) continue;

                if (Convert.ToInt64(v) == employeeId)
                {
                    r.Selected = true;

                    int col = preferredColIndex;
                    if (col < 0 || col >= dgvMain.Columns.Count) col = dgvMain.Columns["Fio"].Index;

                    dgvMain.CurrentCell = r.Cells[col];
                    return;
                }
            }
        }
        private void LoadDepartmentsCombo(int year, int month)
        {
            _isMainLoading = true;
            try
            {
                cbDepartment.DataSource = _db.Departments_ListHierarchicalForCombo_WithEmployeeCountsForMonth(year, month);
                cbDepartment.DisplayMember = "DisplayName";
                cbDepartment.ValueMember = "Id";
            }
            finally
            {
                _isMainLoading = false;
            }
        }
        private void FillYears()
        {
            int startYear = 2025;
            int currentYear = DateTime.Now.Year;

            cbYear.Items.Clear();

            for (int year = startYear; year <= currentYear; year++)
            {
                cbYear.Items.Add(year);
            }

            cbYear.SelectedItem = currentYear;
        }
        private void FillMonths()
        {
            cbMonth.DisplayMember = "Text";
            cbMonth.ValueMember = "Value";

            cbMonth.DataSource = new[]
            {
                new { Text = "Январь", Value = 1 },
                new { Text = "Февраль", Value = 2 },
                new { Text = "Март", Value = 3 },
                new { Text = "Апрель", Value = 4 },
                new { Text = "Май", Value = 5 },
                new { Text = "Июнь", Value = 6 },
                new { Text = "Июль", Value = 7 },
                new { Text = "Август", Value = 8 },
                new { Text = "Сентябрь", Value = 9 },
                new { Text = "Октябрь", Value = 10 },
                new { Text = "Ноябрь", Value = 11 },
                new { Text = "Декабрь", Value = 12 },
            };

            cbMonth.SelectedIndex = DateTime.Now.Month - 1;
        }
        private void LoadProgramsComboForSelectedPeriod()
        {
            long? prevDepId = null;
            if (cbDepartment.SelectedValue != null && cbDepartment.SelectedValue != DBNull.Value)
                prevDepId = Convert.ToInt64(cbDepartment.SelectedValue);

            int prevProgramId = -1;
            if (cbProgram.SelectedValue != null && cbProgram.SelectedValue != DBNull.Value)
                int.TryParse(cbProgram.SelectedValue.ToString(), out prevProgramId);

            if (cbYear.SelectedItem == null || cbMonth.SelectedValue == null)
            {
                cbProgram.DataSource = null;
                cbProgram.Items.Clear();
                return;
            }

            int year = Convert.ToInt32(cbYear.SelectedItem);
            int month = Convert.ToInt32(cbMonth.SelectedValue);

            // 1) департаменты
            cbDepartment.DataSource = _db.Departments_ListHierarchicalForCombo_WithEmployeeCountsForMonth(year, month);
            cbDepartment.DisplayMember = "DisplayName";
            cbDepartment.ValueMember = "Id";

            if (prevDepId.HasValue)
                cbDepartment.SelectedValue = prevDepId.Value;

            // 2) программы
            var dt = _db.Programs_ListForMonth(year, month);

            if (dt == null || dt.Rows.Count == 0)
            {
                var emptyDt = new DataTable();
                emptyDt.Columns.Add("Id");
                emptyDt.Columns.Add("ShortName");
                emptyDt.Rows.Add(-1, "Нет программ");

                cbProgram.DisplayMember = "ShortName";
                cbProgram.ValueMember = "Id";
                cbProgram.DataSource = emptyDt;
                cbProgram.Enabled = false;
                return;
            }

            cbProgram.Enabled = true;

            var list = dt.Copy();
            var allRow = list.NewRow();
            allRow["Id"] = 0;
            allRow["ShortName"] = "Все программы";
            list.Rows.InsertAt(allRow, 0);

            cbProgram.DisplayMember = "ShortName";
            cbProgram.ValueMember = "Id";
            cbProgram.DataSource = list;

            if (prevProgramId > 0)
                cbProgram.SelectedValue = prevProgramId;
            else
                cbProgram.SelectedValue = 0;
        }
        private void PeriodChanged()
        {
            if (_isMainLoading) return;

            _isMainLoading = true;
            try
            {
                LoadProgramsComboForSelectedPeriod();
                ReloadMainGrid();
            }
            finally
            {
                _isMainLoading = false;
            }
        }
        private void DgvMain_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Игнорируем служебные клавиши (Enter, Backspace и т.д.)
            if (char.IsControl(e.KeyChar)) return;

            // Добавляем нажатую букву в наш невидимый буфер
            _quickSearchText += e.KeyChar.ToString().ToLower();

            // Перезапускаем таймер
            _quickSearchTimer.Stop();
            _quickSearchTimer.Start();

            // Ищем совпадение по первым буквам ФИО
            foreach (DataGridViewRow row in dgvMain.Rows)
            {
                if (row.IsNewRow) continue;

                var fioCell = row.Cells["Fio"].Value;

                // StartsWith значит, что если ты напечатал "ива", он найдет "Иванов"
                if (fioCell != null && fioCell.ToString().ToLower().StartsWith(_quickSearchText))
                {
                    dgvMain.ClearSelection();
                    row.Selected = true;
                    dgvMain.FirstDisplayedScrollingRowIndex = row.Index;

                    // Если мы нашли человека, глушим стандартный звук "бдзынь" от Windows
                    e.Handled = true;
                    break;
                }
            }
        }
        private void menuReportFolder_Click(object sender, EventArgs e)
        {
            try
            {
                // Формируем путь к папке с отчетами
                string reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");

                // Если папки еще нет (отчеты ни разу не формировались), 
                // создаем её, чтобы Проводник не выдал ошибку
                if (!Directory.Exists(reportsDir))
                {
                    Directory.CreateDirectory(reportsDir);
                }

                // Открываем папку в стандартном Проводнике Windows
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = reportsDir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть папку с отчетами:\n" + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    public static class AboutDialog
    {
        public static void Show()
        {
            using (Form form = new Form())
            {
                form.Text = "О программе";
                form.Size = new Size(450, 270); // Чуть увеличили высоту под новые строки
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowIcon = false;
                form.BackColor = Color.White;

                // --- Иконка ---
                PictureBox pbLogo = new PictureBox();
                pbLogo.Location = new Point(20, 20);
                pbLogo.Size = new Size(90, 90);
                pbLogo.SizeMode = PictureBoxSizeMode.Zoom;
                // Убрали серый фон, теперь там будет прозрачность или белый фон самой формы
                pbLogo.BackColor = Color.Transparent;

                // ПОДКЛЮЧАЕМ ТВОЙ PNG ИЗ РЕСУРСОВ:
                pbLogo.Image = Properties.Resources.logo; // Убедись, что имя совпадает с тем, как файл добавился в ресурсы

                // --- Заголовок ---
                Label lblTitle = new Label();
                lblTitle.Text = "Mission Time";
                lblTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
                lblTitle.Location = new Point(130, 20);
                lblTitle.AutoSize = true;

                // --- Версия ---
                Label lblVersion = new Label();
                lblVersion.Text = "Версия: " + Application.ProductVersion;
                lblVersion.Font = new Font("Segoe UI", 9, FontStyle.Regular);
                lblVersion.ForeColor = Color.Gray;
                lblVersion.Location = new Point(133, 50);
                lblVersion.AutoSize = true;

                // --- Описание ---
                Label lblDesc = new Label();
                lblDesc.Text = "Автоматизированная система учета рабочего времени\nи генерации табелей трудозатрат.";
                lblDesc.Font = new Font("Segoe UI", 10, FontStyle.Regular);
                lblDesc.Location = new Point(130, 80);
                lblDesc.Size = new Size(290, 45);

                // --- НОВОЕ: Разработчик ---
                Label lblDev = new Label();
                lblDev.Text = "Разработал: Gatami L."; // <-- ВПИШИ СВОЕ ФИО СЮДА
                lblDev.Font = new Font("Segoe UI", 9, FontStyle.Regular);
                lblDev.Location = new Point(130, 130);
                lblDev.AutoSize = true;

                // --- НОВОЕ: Ссылка на GitHub ---
                LinkLabel lnkGithub = new LinkLabel();
                lnkGithub.Text = "Проект на GitHub";
                lnkGithub.Font = new Font("Segoe UI", 9, FontStyle.Regular);
                lnkGithub.Location = new Point(130, 155);
                lnkGithub.AutoSize = true;

                // Обработчик клика по ссылке
                lnkGithub.LinkClicked += (s, e) => {
                    string url = "https://github.com/GatamiL/MissionTime"; // <-- ВПИШИ СВОЙ URL СЮДА
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Мягкий фоллбэк: если заблокировали запуск, копируем в буфер!
                        Clipboard.SetText(url);
                        MessageBox.Show(
                            "Корпоративные политики безопасности заблокировали автоматическое открытие браузера.\n\n" +
                            "Но ничего страшного! Ссылка скопирована в буфер обмена:\n" + url + "\n\n" +
                            "Просто вставьте её в адресную строку (Ctrl+V).",
                            "Mission Time",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                };

                // --- Копирайт ---
                Label lblCopyright = new Label();
                lblCopyright.Text = "© 2026. Все права защищены.";
                lblCopyright.Font = new Font("Segoe UI", 9, FontStyle.Regular);
                lblCopyright.ForeColor = Color.Gray;
                lblCopyright.Location = new Point(130, 190);
                lblCopyright.AutoSize = true;

                // --- Кнопка ОК ---
                Button btnOk = new Button();
                btnOk.Text = "ОК";
                btnOk.Size = new Size(100, 32);
                btnOk.Location = new Point(310, 185); // Опустили кнопку ниже
                btnOk.BackColor = Color.FromArgb(225, 225, 225);
                btnOk.FlatStyle = FlatStyle.Flat;
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Cursor = Cursors.Hand;
                btnOk.Click += (s, e) => form.Close();

                // Добавляем всё на форму
                form.Controls.Add(pbLogo);
                form.Controls.Add(lblTitle);
                form.Controls.Add(lblVersion);
                form.Controls.Add(lblDesc);
                form.Controls.Add(lblDev);
                form.Controls.Add(lnkGithub);
                form.Controls.Add(lblCopyright);
                form.Controls.Add(btnOk);

                form.ShowDialog();
            }
        }
    }
}
