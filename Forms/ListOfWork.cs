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
    public partial class ListOfWork : Form
    {
        private readonly DbService _db;
        public ListOfWork(DbService db)
        {
            InitializeComponent();
            _db = db;

            dgvLOW.SelectionChanged += (s, e) => UpdateButtons();
        }

        private void ListOfWork_Load(object sender, EventArgs e)
        {
            BuilddgvLOW();
            ReloadGrid();
            UpdateButtons();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new ListOfWork_Edit("Добавить работу", "", "", "Создать"))
                {
                    dlg.Icon = this.Icon;
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    _db.LOW_Create(null, dlg.WorkName, dlg.SpecialCode);

                    ReloadGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnChange_Click(object sender, EventArgs e)
        {
            if (dgvLOW.CurrentRow == null) return;

            long id = GetSelectedId();
            string name = GetSelectedName();
            string code = GetSelectedCode();

            try
            {
                using (var dlg = new ListOfWork_Edit("Изменить работу", name, code, "Изменить"))
                {
                    dlg.Icon = this.Icon;
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    _db.LOW_Update(id, null, dlg.WorkName, dlg.SpecialCode);

                    ReloadGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvLOW.CurrentRow == null) return;

            long id = GetSelectedId();
            string name = GetSelectedName();

            var res = MessageBox.Show($"Удалить \"{name}\"?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res != DialogResult.Yes) return;

            try
            {
                _db.LOW_Delete(id);
                ReloadGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить.\nВозможно, работа уже используется в табеле.\n\n" + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void BuilddgvLOW()
        {
            dgvLOW.BackgroundColor = Color.White;
            dgvLOW.BorderStyle = BorderStyle.None;
            dgvLOW.RowHeadersVisible = false;
            dgvLOW.AllowUserToResizeRows = false;
            dgvLOW.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvLOW.MultiSelect = false;

            dgvLOW.Columns.Clear();
            dgvLOW.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                DataPropertyName = "Id",
                Visible = false
            });
            dgvLOW.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Наименование",
                DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgvLOW.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCode",
                HeaderText = "Код по классификатору",
                DataPropertyName = "SpecialCode",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 120
            });
        }
        private void ReloadGrid()
        {
            dgvLOW.DataSource = _db.LOW_List();
            UpdateButtons();
        }
        private void UpdateButtons()
        {
            bool has = dgvLOW.CurrentRow != null;
            btnChange.Enabled = has;
            btnDelete.Enabled = has;
        }
        private long GetSelectedId()
        {
            return Convert.ToInt64(dgvLOW.CurrentRow.Cells["colId"].Value);
        }
        private string GetSelectedName()
        {
            return Convert.ToString(dgvLOW.CurrentRow.Cells["colName"].Value);
        }
        private string GetSelectedCode()
        {
            return Convert.ToString(dgvLOW.CurrentRow.Cells["colCode"].Value);
        }
    }
}
