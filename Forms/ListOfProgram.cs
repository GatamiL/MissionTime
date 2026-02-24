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
    public partial class ListOfProgram : Form
    {
        private readonly DbService _db;
        public ListOfProgram(DbService db)
        {
            InitializeComponent();

            _db = db;
        }
        private void ListOfProgram_Load(object sender, EventArgs e)
        {
            BuilddgvLOP();
            ReloadGrid();
            UpdateButtons();
        }
        private void btnCreate_Click(object sender, EventArgs e)
        {
            using (var dlg = new ListOfProgram_Edit("Добавить программу", "", "", DateTime.Today, null, "Создать"))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _db.Program_Create(dlg.ProgramName, dlg.ShortName, dlg.DateStart, dlg.DateEnd);
                    ReloadGrid();
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
            if (dgvLOP.CurrentRow == null) return;

            long id = GetSelectedId();
            string name = GetSelectedString("colName");
            string shortName = GetSelectedString("colShortName");
            DateTime ds = GetSelectedDate("colDateStart");
            DateTime? de = GetSelectedDateNullable("colDateEnd");

            using (var dlg = new ListOfProgram_Edit("Изменить программу", name, shortName, ds, de, "Изменить"))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _db.Program_Update(id, dlg.ProgramName, dlg.ShortName, dlg.DateStart, dlg.DateEnd);
                    ReloadGrid();
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
            if (dgvLOP.CurrentRow == null) return;

            long id = GetSelectedId();
            string name = GetSelectedString("colName");

            var res = MessageBox.Show($"Удалить программу \"{name}\"?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res != DialogResult.Yes) return;

            try
            {
                _db.Program_Delete(id);
                ReloadGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void BuilddgvLOP()
        {
            dgvLOP.BackgroundColor = Color.White;
            dgvLOP.BorderStyle = BorderStyle.None;
            dgvLOP.RowHeadersVisible = false; // Убираем пустой столбец слева
            dgvLOP.AllowUserToResizeRows = false;
            dgvLOP.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvLOP.MultiSelect = false;

            dgvLOP.Columns.Clear();
            dgvLOP.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                DataPropertyName = "Id",
                Visible = false
            });
            dgvLOP.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Наименование",
                DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, // Растянется по максимуму
                MinimumWidth = 150
            });
            dgvLOP.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colShortName",
                HeaderText = "Краткое наименование",
                DataPropertyName = "ShortName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });
            dgvLOP.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDateStart",
                HeaderText = "Начало программы",
                DataPropertyName = "DateStart",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 120
            });
            dgvLOP.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDateEnd",
                HeaderText = "Окончание программы",
                DataPropertyName = "DateEnd",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 120
            });
        }
        private void ReloadGrid()
        {
            dgvLOP.DataSource = _db.Programs_List();
        }
        private void UpdateButtons()
        {
            bool has = dgvLOP.CurrentRow != null;
            btnChange.Enabled = has;
            btnDelete.Enabled = has;
        }
        private long GetSelectedId()
        {
            return Convert.ToInt64(dgvLOP.CurrentRow.Cells["colId"].Value);
        }
        private string GetSelectedString(string col)
        {
            return Convert.ToString(dgvLOP.CurrentRow.Cells[col].Value);
        }
        private DateTime GetSelectedDate(string col)
        {
            var s = Convert.ToString(dgvLOP.CurrentRow.Cells[col].Value);
            return DateTime.Parse(s).Date;
        }
        private DateTime? GetSelectedDateNullable(string col)
        {
            var v = dgvLOP.CurrentRow.Cells[col].Value;
            if (v == null || v == DBNull.Value) return null;
            var s = Convert.ToString(v);
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.Parse(s).Date;
        }
    }
}
