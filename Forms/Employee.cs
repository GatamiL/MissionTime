using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionTime.Forms
{
    public partial class Employee : Form
    {
        private readonly DbService _db;
        public Employee(DbService db)
        {
            InitializeComponent();

            _db = db;

            cbFired.CheckedChanged += (s, e) => { ReloadGrid(); UpdateButtons(); };
            dgvEmployee.SelectionChanged += (s, e) => UpdateButtons();
            dgvEmployee.RowPrePaint += dgvEmployee_RowPrePaint;
        }

        private void Employee_Load(object sender, EventArgs e)
        {
            BuilddgvEmployee();
            ReloadGrid();
            UpdateButtons();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            using (var dlg = new Employee_Create(_db))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _db.Employee_CreateAndHire(
                        dlg.Fio,
                        dlg.DepartmentId,
                        dlg.PositionId,
                        dlg.HireDate
                    );
                    ReloadGrid();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось создать сотрудника:\n" + ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private void btnChangeFio_Click(object sender, EventArgs e)
        {
            int employeeId = GetSelectedEmployeeId();
            string fio = GetSelectedEmployeeFio();

            using (var dlg = new Employee_EditFio(fio))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _db.Employee_UpdateFio(employeeId, dlg.newFio);
                    ReloadGrid();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось изменить:\n" + ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnTransfer_Click(object sender, EventArgs e)
        {
            int employeeId = GetSelectedEmployeeId();
            string fio = GetSelectedEmployeeFio();

            var cur = _db.Employee_GetCurrentDeptPos(employeeId);
            if (!cur.HasValue)
            {
                MessageBox.Show("Не найдены текущие подразделение/должность сотрудника.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new Employee_Transfer(
                _db,
                fio,
                DateTime.Today,
                cur.Value.DepartmentId,
                cur.Value.PositionId))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _db.Employee_Transfer(employeeId, dlg.DepartmentId, dlg.PositionId, dlg.TransferDate);
                    ReloadGrid();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnFire_Click(object sender, EventArgs e)
        {
            int employeeId = GetSelectedEmployeeId();
            string fio = GetSelectedEmployeeFio();

            using (var dlg = new Employee_Fire(fio, DateTime.Today))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _db.Employee_Fire(employeeId, dlg.FireDate);
                    ReloadGrid();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnPositions_Click(object sender, EventArgs e)
        {
            if (dgvEmployee.CurrentRow == null) return;

            long employeeId = Convert.ToInt64(dgvEmployee.CurrentRow.Cells["colEmployeeId"].Value);
            string fio = GetSelectedEmployeeFio();

            using (var f = new Employee_History(_db, fio, employeeId))
            {
                f.Icon = this.Icon;
                f.ShowDialog(this);
            }

            ReloadGrid();
        }
        private void BuilddgvEmployee()
        {
            dgvEmployee.BackgroundColor = Color.White;
            dgvEmployee.BorderStyle = BorderStyle.None;
            dgvEmployee.RowHeadersVisible = false;
            dgvEmployee.AllowUserToResizeRows = false;
            dgvEmployee.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvEmployee.MultiSelect = false;

            dgvEmployee.Columns.Clear();
            dgvEmployee.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colEmployeeId",
                DataPropertyName = "Id",
                Visible = false
            });
            dgvEmployee.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFio",
                HeaderText = "ФИО сотрудника",
                DataPropertyName = "Fio",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 150
            });
            dgvEmployee.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CurrentDepartment",
                HeaderText = "Подразделение",
                DataPropertyName = "DepartmentName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgvEmployee.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCurrentPosition",
                HeaderText = "Должность",
                DataPropertyName = "PositionName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 120
            });
        }
        private void ReloadGrid()
        {
            dgvEmployee.DataSource = _db.Employee_List(cbFired.Checked);

            if (dgvEmployee.Columns["colEmployeeId"] != null) dgvEmployee.Columns["colEmployeeId"].Visible = false;
            if (dgvEmployee.Columns["colIsFired"] != null) dgvEmployee.Columns["colIsFired"].Visible = false;
        }
        private void UpdateButtons()
        {
            bool has = dgvEmployee.CurrentRow != null;
            btnTransfer.Enabled = has && !IsSelectedFired();
            btnFire.Enabled = has && !IsSelectedFired();
        }
        private string GetSelectedEmployeeFio()
        {
            return Convert.ToString(dgvEmployee.CurrentRow.Cells["colFio"].Value);
        }
        private int GetSelectedEmployeeId()
        {
            return Convert.ToInt32(dgvEmployee.CurrentRow.Cells["colEmployeeId"].Value);
        }
        private bool IsSelectedFired()
        {
            if (dgvEmployee.CurrentRow == null) return false;

            var v = dgvEmployee.CurrentRow.Cells["colCurrentPosition"].Value;
            return v == null || v == DBNull.Value || string.IsNullOrWhiteSpace(v.ToString());
        }
        private void dgvEmployee_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var row = dgvEmployee.Rows[e.RowIndex];
            if (row == null || row.IsNewRow) return;

            var pos = row.Cells["colCurrentPosition"].Value;
            bool isFired = pos == null || pos == DBNull.Value || string.IsNullOrWhiteSpace(pos.ToString());

            // курсив для уволенных
            row.DefaultCellStyle.Font = isFired
                ? new Font(dgvEmployee.Font, FontStyle.Italic)
                : new Font(dgvEmployee.Font, FontStyle.Regular);

            // можно ещё “приглушить” цвет
            row.DefaultCellStyle.ForeColor = isFired ? Color.Gray : Color.Black;
        }
    }
}
