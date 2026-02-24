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
    public partial class EmployeePositions : Form
    {
        private readonly DbService _db;
        public EmployeePositions(DbService db)
        {
            InitializeComponent();

            _db = db;
            dgvPositions.SelectionChanged += (s, e) => UpdateButtons();
        }

        private void EmployeePositions_Load(object sender, EventArgs e)
        {
            BuilddgvPositions();
            ReloadGrid();
            UpdateButtons();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            using (var dlg = new EmployeePosition_Edit("Должности: Создать", "", "Создать"))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    _db.Position_Create(dlg.PositionName);
                    ReloadGrid();
                    UpdateButtons();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось создать:\n" + ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnChange_Click(object sender, EventArgs e)
        {
            if (dgvPositions.CurrentRow == null) return;

            long id = GetSelectedId();
            string curName = GetSelectedName();
            string title = $"Изменить должность: {curName}";

            using (var dlg = new EmployeePosition_Edit(title, curName, "Изменить"))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    _db.Position_Update(id, dlg.PositionName);
                    ReloadGrid();
                    UpdateButtons();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось изменить:\n" + ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvPositions.CurrentRow == null) return;

            long id = GetSelectedId();
            string name = GetSelectedName();

            var res = MessageBox.Show($"Удалить должность \"{name}\"?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res != DialogResult.Yes) return;

            try
            {
                _db.Position_Delete(id);
                ReloadGrid();
                UpdateButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void BuilddgvPositions()
        {
            dgvPositions.BackgroundColor = Color.White;
            dgvPositions.BorderStyle = BorderStyle.None;
            dgvPositions.RowHeadersVisible = false;
            dgvPositions.AllowUserToResizeRows = false;
            dgvPositions.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPositions.MultiSelect = false;

            dgvPositions.Columns.Clear();
            dgvPositions.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                DataPropertyName = "Id",
                Visible = false
            });
            dgvPositions.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Наименование",
                DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 150
            });
        }
        private void ReloadGrid()
        {
            dgvPositions.DataSource = _db.Positions_List();

            if (dgvPositions.Columns["Id"] != null)
                dgvPositions.Columns["Id"].Visible = false;
            foreach (DataGridViewColumn column in dgvPositions.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }
        private void UpdateButtons()
        {
            bool has = dgvPositions.CurrentRow != null;
            btnChange.Enabled = has;
            btnDelete.Enabled = has;
        }
        private long GetSelectedId()
        {
            return Convert.ToInt64(dgvPositions.CurrentRow.Cells["colId"].Value);
        }
        private string GetSelectedName()
        {
            return Convert.ToString(dgvPositions.CurrentRow.Cells["colName"].Value);
        }
    }
}
