using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionTime.Forms
{
    public partial class MainForm_edit : Form
    {
        private readonly DbService _db;
        private readonly long _ephId;
        private readonly int _year;
        private readonly int _month;
        private readonly int _programId;

        private readonly HashSet<long> _manualWorkIds = new HashSet<long>();
        private TextBox _activeEditBox;
        private bool _inFormat;
        private bool _isLoading;
        private long _timesheetId = 0;
        private long _departmentId = 0;
        private DateTime _segStart;
        private DateTime _segEnd;

        public MainForm_edit(DbService db, long ephId, int year, int month, int programId)
        {
            InitializeComponent();

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ephId = ephId;
            _year = year;
            _month = month;
            _programId = programId;
        }
        private void MainForm_edit_Load(object sender, EventArgs e)
        {
            _isLoading = true;
            try
            {
                // 1) гарантируем одиночную подписку
                cbProgram.SelectedValueChanged -= cbProgram_SelectedValueChanged;
                cbProgram.SelectedValueChanged += cbProgram_SelectedValueChanged;

                dgvTimeSheet.CellBeginEdit -= dgvTimeSheet_CellBeginEdit;
                dgvTimeSheet.CellBeginEdit += dgvTimeSheet_CellBeginEdit;

                dgvTimeSheet.EditingControlShowing -= dgvTimeSheet_EditingControlShowing;
                dgvTimeSheet.EditingControlShowing += dgvTimeSheet_EditingControlShowing;

                dgvTimeSheet.CellEndEdit -= dgvTimeSheet_CellEndEdit;
                dgvTimeSheet.CellEndEdit += dgvTimeSheet_CellEndEdit;

                dgvTimeSheet.KeyDown -= dgvTimeSheet_KeyDown;
                dgvTimeSheet.KeyDown += dgvTimeSheet_KeyDown;

                // 2) шапка
                lblYear.Text = _year.ToString();
                lblMonth.Text = TimeUtils.GetMonthRu(_month);

                var r = _db.Employee_GetByEphId(_ephId);
                if (r == null)
                {
                    lblFio.Text = "(не найдено)";
                    lblDepartment.Text = "(не найдено)";
                    return;
                }

                lblFio.Text = Convert.ToString(r["Fio"]);
                lblDepartment.Text = Convert.ToString(r["DepartmentName"]);

                // 3) сегмент для "-" (ОБЯЗАТЕЛЬНО ДО ReloadTimeSheetGrid)
                (_segStart, _segEnd) = _db.EPH_GetSegmentForMonth(_ephId, _year, _month);

                // 4) программы
                var dt = _db.Programs_ListForMonth(_year, _month);
                cbProgram.DisplayMember = "ShortName";
                cbProgram.ValueMember = "Id";
                cbProgram.DataSource = dt;

                if (_programId > 0)
                    cbProgram.SelectedValue = _programId;

                // 5) грид
                BuildTimeSheetGridColumns();
                ReloadTimeSheetGrid();
            }
            finally
            {
                _isLoading = false;
            }
        }
        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new MainForm_editWork(_db))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (dlg.WorkId <= 0) return;

                _manualWorkIds.Add(dlg.WorkId);

                AddWorkRow(dlg.WorkId, dlg.WorkDisplayName);
            }
        }
        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (_isLoading) return;
            if (dgvTimeSheet.CurrentRow == null) return;

            // выбранная программа
            if (cbProgram.SelectedValue == null || cbProgram.SelectedValue == DBNull.Value)
            {
                MessageBox.Show("Выберите программу.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int programId = Convert.ToInt32(cbProgram.SelectedValue);
            if (programId <= 0)
            {
                MessageBox.Show("Выберите программу.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var gridRow = dgvTimeSheet.CurrentRow;
            if (gridRow.Cells["WorkId"].Value == null || gridRow.Cells["WorkId"].Value == DBNull.Value)
                return;

            long workId = Convert.ToInt64(gridRow.Cells["WorkId"].Value);
            string workName = Convert.ToString(gridRow.Cells["WorkName"].Value);

            // есть ли записи в БД по этой работе
            int cnt = 0;
            try
            {
                cnt = _db.TimesheetEntry_CountForEphProgramWorkMonth(_ephId, _year, _month, programId, workId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось проверить записи:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (cnt > 0)
            {
                var res = MessageBox.Show(
                    $"По работе \"{workName}\" есть {cnt} запис(ей) за месяц.\n\n" +
                    "Да — удалить записи из БД и убрать строку.\n" +
                    "Нет — только убрать строку (записи останутся).\n" +
                    "Отмена — ничего не делать.",
                    "Удаление",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (res == DialogResult.Cancel) return;

                if (res == DialogResult.Yes)
                {
                    try
                    {
                        _db.TimesheetEntry_DeleteForEphProgramWorkMonth(_ephId, _year, _month, programId, workId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Не удалось удалить записи:\n" + ex.Message, "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                // если No — удаляем только строку из грида
            }
            else
            {
                // если записей нет — можно просто спросить подтверждение (опционально)
                var res = MessageBox.Show(
                    $"Убрать работу \"{workName}\" из таблицы?",
                    "Удаление",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res != DialogResult.Yes) return;
            }

            // 1) убрать из списка “ручных” (если была добавлена вручную)
            _manualWorkIds.Remove(workId);

            // 2) убрать строку из DataTable (а не из dgv.Rows, чтобы биндинг не сломался)
            if (dgvTimeSheet.DataSource is DataTable dt)
            {
                foreach (DataRow r in dt.Rows)
                {
                    if (Convert.ToInt64(r["WorkId"]) == workId)
                    {
                        r.Delete();
                        break;
                    }
                }
                dt.AcceptChanges();
            }

            // 3) если всё стало пусто — очистить грид
            if (dgvTimeSheet.Rows.Count == 0)
                dgvTimeSheet.DataSource = null;
        }
        private void btnClose_Click(object sender, EventArgs e)
        {

        }
        private void BuildTimeSheetGridColumns()
        {
            dgvTimeSheet.AutoGenerateColumns = false;
            dgvTimeSheet.AllowUserToAddRows = false;
            dgvTimeSheet.AllowUserToDeleteRows = false;
            dgvTimeSheet.AllowUserToResizeColumns = false;
            dgvTimeSheet.AllowUserToResizeRows = false;
            dgvTimeSheet.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgvTimeSheet.MultiSelect = false;
            dgvTimeSheet.ReadOnly = false;
            dgvTimeSheet.RowHeadersVisible = false;
            //dgvTimeSheet.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvTimeSheet.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvTimeSheet.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvTimeSheet.Columns.Clear();

            // скрытый ключ вида работ
            dgvTimeSheet.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "WorkId",
                DataPropertyName = "WorkId",
                Frozen = true,
                Visible = false
            });

            dgvTimeSheet.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "WorkName",
                HeaderText = "Вид работы",
                DataPropertyName = "WorkName",
                Frozen = true,
                Width = 220,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    WrapMode = DataGridViewTriState.True
                }
            });

            for (int i = 1; i <= 31; i++)
            {
                dgvTimeSheet.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "D" + i,
                    HeaderText = i.ToString(),
                    DataPropertyName = "D" + i,
                    Width = 55
                });
            }

            dgvTimeSheet.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Total",
                HeaderText = "Итого",
                DataPropertyName = "Total",
                Width = 80
            });

            foreach (DataGridViewColumn col in dgvTimeSheet.Columns)
            {
                if (col.Name.StartsWith("D") || col.Name == "Total")
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
            }
            UpdateTimeSheetColumnsVisibility(_year, _month);
        }
        private void UpdateTimeSheetColumnsVisibility(int year, int month)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);

            for (int i = 1; i <= 31; i++)
            {
                dgvTimeSheet.Columns["D" + i].Visible = (i <= daysInMonth);

                if (i <= daysInMonth)
                {
                    DateTime date = new DateTime(year, month, i);
                    dgvTimeSheet.Columns["D" + i].HeaderText = i + "\n" + TimeUtils.GetDowRu2(date.DayOfWeek);
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                        dgvTimeSheet.Columns["D" + i].DefaultCellStyle.BackColor = Color.LightGray;
                    else
                        dgvTimeSheet.Columns["D" + i].DefaultCellStyle.BackColor = Color.White;
                }
            }
        }
        private void ReloadTimeSheetGrid()
        {
            if (cbProgram.SelectedValue == null || cbProgram.SelectedValue == DBNull.Value)
            {
                dgvTimeSheet.DataSource = null;
                return;
            }

            int programId = Convert.ToInt32(cbProgram.SelectedValue);
            if (programId <= 0)
            {
                dgvTimeSheet.DataSource = null;
                return;
            }

            UpdateTimeSheetColumnsVisibility(_year, _month);
            int daysInMonth = DateTime.DaysInMonth(_year, _month);

            var dtWorks = _db.ListOfWork_UsedForEphProgramMonth(_ephId, _year, _month, programId);

            // добавить “ручные”
            if (_manualWorkIds.Count > 0)
            {
                var existing = new HashSet<long>();
                foreach (DataRow r in dtWorks.Rows)
                    existing.Add(Convert.ToInt64(r["WorkId"]));

                var missing = _manualWorkIds.Where(id => !existing.Contains(id)).ToList();
                if (missing.Count > 0)
                {
                    var dtMissing = _db.ListOfWork_ByIds(missing);
                    foreach (DataRow r in dtMissing.Rows)
                        dtWorks.ImportRow(r);
                }
            }

            if (dtWorks.Rows.Count == 0)
            {
                dgvTimeSheet.DataSource = null;
                return;
            }

            // минуты по дням
            var dtMin = _db.TimesheetMinutes_ByEphProgramWorkDay(_ephId, _year, _month, programId);

            // map: WorkId|day -> minutes
            var map = new Dictionary<string, int>();
            foreach (DataRow r in dtMin.Rows)
            {
                long workId = Convert.ToInt64(r["WorkId"]);
                DateTime d = DateTime.Parse(r["WorkDate"].ToString()).Date;
                int mins = Convert.ToInt32(r["MinSum"]);
                map[workId + "|" + d.Day] = mins;
            }

            // output
            var outDt = new DataTable();
            outDt.Columns.Add("WorkId", typeof(long));
            outDt.Columns.Add("WorkName", typeof(string));
            for (int i = 1; i <= 31; i++) outDt.Columns.Add("D" + i, typeof(string));
            outDt.Columns.Add("Total", typeof(string));

            var works = dtWorks.AsEnumerable()
                .OrderBy(r => Convert.ToString(r["WorkName"]))
                .ToList();

            foreach (DataRow w in works)
            {
                long workId = Convert.ToInt64(w["WorkId"]);
                string workName = Convert.ToString(w["WorkName"]);

                int total = 0;

                var row = outDt.NewRow();
                row["WorkId"] = workId;
                row["WorkName"] = workName;

                for (int day = 1; day <= 31; day++)
                {
                    if (day > daysInMonth)
                    {
                        row["D" + day] = "";
                        continue;
                    }

                    DateTime cur = new DateTime(_year, _month, day);

                    // ВНЕ сегмента -> "-"
                    if (cur < _segStart || cur > _segEnd)
                    {
                        row["D" + day] = "-";
                        continue;
                    }

                    // ВНУТРИ сегмента -> время
                    map.TryGetValue(workId + "|" + day, out int mins);

                    row["D" + day] = mins == 0 ? "" : TimeUtils.MinutesToHHmm(mins);
                    total += mins;
                }

                row["Total"] = total == 0 ? "" : TimeUtils.MinutesToHHmm(total);
                outDt.Rows.Add(row);
            }

            dgvTimeSheet.DataSource = outDt;

            if (dgvTimeSheet.Columns["WorkId"] != null)
                dgvTimeSheet.Columns["WorkId"].Visible = false;
        }
        private void AddWorkRow(long workId, string workName)
        {
            // если таблицы ещё нет — создаём
            if (dgvTimeSheet.DataSource == null)
            {
                var outDt = new DataTable();
                outDt.Columns.Add("WorkId", typeof(long));
                outDt.Columns.Add("WorkName", typeof(string));
                for (int i = 1; i <= 31; i++) outDt.Columns.Add("D" + i, typeof(string));
                outDt.Columns.Add("Total", typeof(string));
                dgvTimeSheet.DataSource = outDt;
            }

            var dt = dgvTimeSheet.DataSource as DataTable;
            if (dt == null) return;

            // не добавлять дубликаты
            foreach (DataRow r in dt.Rows)
                if (Convert.ToInt64(r["WorkId"]) == workId)
                    return;

            int daysInMonth = DateTime.DaysInMonth(_year, _month);

            var row = dt.NewRow();
            row["WorkId"] = workId;
            row["WorkName"] = workName;

            int total = 0;

            for (int day = 1; day <= 31; day++)
            {
                if (day > daysInMonth)
                {
                    row["D" + day] = "";
                    continue;
                }

                DateTime cur = new DateTime(_year, _month, day);

                // вне сегмента EPH -> "-"
                if (cur < _segStart || cur > _segEnd)
                {
                    row["D" + day] = "-";
                    continue;
                }

                // внутри сегмента -> пусто (пока нет данных)
                row["D" + day] = "";
            }

            row["Total"] = total == 0 ? "" : TimeUtils.MinutesToHHmm(total);

            dt.Rows.Add(row);

            // (опционально) выделить добавленную строку
            dgvTimeSheet.ClearSelection();
            dgvTimeSheet.Rows[dgvTimeSheet.Rows.Count - 1].Selected = true;
        }
        private void dgvTimeSheet_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            var col = dgvTimeSheet.Columns[e.ColumnIndex];
            if (!col.Name.StartsWith("D")) return;

            var row = dgvTimeSheet.Rows[e.RowIndex];

            if (row.Cells["WorkId"].Value == null || row.Cells["WorkId"].Value == DBNull.Value)
            {
                e.Cancel = true;
                MessageBox.Show("Сначала выберите вид работ.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var cellVal = Convert.ToString(row.Cells[col.Name].Value);
            if (cellVal == "-")
            {
                e.Cancel = true;
                return;
            }
        }
        private void dgvTimeSheet_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_activeEditBox != null)
            {
                _activeEditBox.KeyPress -= EditBox_KeyPress;
                _activeEditBox.TextChanged -= EditBox_TextChanged;
            }

            _activeEditBox = e.Control as TextBox;
            if (_activeEditBox == null) return;

            _activeEditBox.KeyPress += EditBox_KeyPress;
            _activeEditBox.TextChanged += EditBox_TextChanged;
        }
        private void EditBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            if (!char.IsDigit(e.KeyChar)) e.Handled = true;
        }
        private void EditBox_TextChanged(object sender, EventArgs e)
        {
            if (_inFormat) return;

            _inFormat = true;
            try
            {
                var tb = _activeEditBox;
                if (tb == null) return;

                string digits = new string(tb.Text.Where(char.IsDigit).ToArray());

                if (digits.Length == 0)
                {
                    tb.Text = "";
                    return;
                }

                if (digits.Length <= 2)
                {
                    tb.Text = digits; // часы
                }
                else
                {
                    string h = digits.Substring(0, 2);
                    string m = digits.Substring(2, Math.Min(2, digits.Length - 2));
                    tb.Text = h + ":" + m;
                }

                tb.SelectionStart = tb.Text.Length;
            }
            finally
            {
                _inFormat = false;
            }
        }
        private void dgvTimeSheet_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_isLoading) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = dgvTimeSheet.Columns[e.ColumnIndex];
            if (!col.Name.StartsWith("D")) return;

            var gridRow = dgvTimeSheet.Rows[e.RowIndex];
            if (gridRow.Cells["WorkId"].Value == null || gridRow.Cells["WorkId"].Value == DBNull.Value)
                return;

            if (cbProgram.SelectedValue == null || cbProgram.SelectedValue == DBNull.Value)
                return;

            int programId = Convert.ToInt32(cbProgram.SelectedValue);
            if (programId <= 0) return;

            long workId = Convert.ToInt64(gridRow.Cells["WorkId"].Value);

            if (!int.TryParse(col.Name.Substring(1), out int day)) return;

            string text = Convert.ToString(gridRow.Cells[col.Name].Value) ?? "";

            int minutes;
            if (text.Trim() == "")
            {
                minutes = 0;
            }
            else if (!NormalizeTimeText(text, out minutes, out string normalized))
            {
                gridRow.Cells[col.Name].Value = "";
                return;
            }
            else
            {
                gridRow.Cells[col.Name].Value = normalized;
            }

            DateTime date = new DateTime(_year, _month, day);

            try
            {
                long timesheetId = EnsureTimesheetId();
                _db.TimesheetEntry_UpsertByEph(timesheetId, date, _ephId, programId, workId, minutes);

                RecalcRowTotals(e.RowIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private bool NormalizeTimeText(string input, out int minutes, out string normalized)
        {
            minutes = 0;
            normalized = "";

            string digits = new string((input ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return true;

            int h, m = 0;

            if (digits.Length <= 2)
            {
                if (!int.TryParse(digits, out h)) return false;
            }
            else
            {
                if (!int.TryParse(digits.Substring(0, 2), out h)) return false;
                int.TryParse(digits.Substring(2).PadRight(2, '0').Substring(0, 2), out m);
            }

            if (h < 0 || h > 24) return false;
            if (m < 0 || m > 59) return false;

            minutes = h * 60 + m;
            normalized = string.Format("{0}:{1:00}", h, m);
            return true;
        }
        private void RecalcRowTotals(int rowIndex)
        {
            var row = dgvTimeSheet.Rows[rowIndex];
            int days = DateTime.DaysInMonth(_year, _month);

            int total = 0;
            for (int day = 1; day <= days; day++)
            {
                string v = Convert.ToString(row.Cells["D" + day].Value) ?? "";
                if (!TryParseHHmmToMinutes(v, out int minutes)) minutes = 0;
                total += minutes;
            }

            row.Cells["Total"].Value = total == 0 ? "" : TimeUtils.MinutesToHHmm(total);
        }
        private bool TryParseHHmmToMinutes(string s, out int minutes)
        {
            minutes = 0;
            s = (s ?? "").Trim();
            if (s == "") return true;

            var parts = s.Split(':');
            if (parts.Length != 2) return false;

            int h, m;
            if (!int.TryParse(parts[0], out h)) return false;
            if (!int.TryParse(parts[1], out m)) return false;
            if (h < 0 || m < 0 || m > 59) return false;

            minutes = h * 60 + m;
            return true;
        }
        private void dgvTimeSheet_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;
            if (_isLoading) return;

            if (cbProgram.SelectedValue == null || cbProgram.SelectedValue == DBNull.Value) return;
            int programId = Convert.ToInt32(cbProgram.SelectedValue);
            if (programId <= 0) return;

            if (dgvTimeSheet.CurrentCell == null) return;

            var cell = dgvTimeSheet.CurrentCell;
            var col = dgvTimeSheet.Columns[cell.ColumnIndex];
            if (col == null || !col.Name.StartsWith("D")) return;

            var row = dgvTimeSheet.Rows[cell.RowIndex];
            if (row.Cells["WorkId"].Value == null || row.Cells["WorkId"].Value == DBNull.Value)
                return;

            long workId = Convert.ToInt64(row.Cells["WorkId"].Value);

            if (!int.TryParse(col.Name.Substring(1), out int day)) return;

            cell.Value = "";

            DateTime date = new DateTime(_year, _month, day);

            try
            {
                long timesheetId = EnsureTimesheetId();
                _db.TimesheetEntry_UpsertByEph(timesheetId, date, _ephId, programId, workId, 0);

                RecalcRowTotals(cell.RowIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось очистить:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            e.Handled = true;
        }
        private long EnsureTimesheetId()
        {
            if (_timesheetId > 0) return _timesheetId;

            // 1) вытащить DepartmentId по ephId один раз
            if (_departmentId <= 0)
                _departmentId = _db.EPH_GetDepartmentId(_ephId);

            // 2) получить/создать timesheet на этот месяц/департамент
            _timesheetId = _db.Timesheet_GetOrCreate(_year, _month, _departmentId);
            return _timesheetId;
        }
        private void cbProgram_SelectedValueChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;
            ReloadTimeSheetGrid();
        }
    }
}
