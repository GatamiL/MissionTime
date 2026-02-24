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
    public partial class Employee_History : Form
    {
        private readonly DbService _db;
        private readonly long _employeeId;
        public Employee_History(DbService db, string fio, long employeeId)
        {
            InitializeComponent();

            _db = db;
            _employeeId = employeeId;
            this.Text = $"Сотрудник {fio}: История переводов";
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
        }

        private void Employee_History_Load(object sender, EventArgs e)
        {
            BuilddgvHistory();
            Reload();
        }

        private void btnCancelLastOperation_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show(
                "Отменить последнюю кадровую операцию?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res != DialogResult.Yes) return;

            try
            {
                _db.Employee_CancelLastOperation(_employeeId);
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void BuilddgvHistory()
        {
            dgvHistory.BackgroundColor = Color.White;
            dgvHistory.BorderStyle = BorderStyle.None;
            dgvHistory.RowHeadersVisible = false; // Убираем пустой столбец слева
            dgvHistory.AllowUserToResizeRows = false;
            dgvHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvHistory.MultiSelect = false;

            dgvHistory.Columns.Clear();
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPosition",
                HeaderText = "Должность",
                DataPropertyName = "Position",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, // Растянется по максимуму
                MinimumWidth = 150
            });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStartDate",
                HeaderText = "На должности с",
                DataPropertyName = "StartDate",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colEndDate",
                HeaderText = "На должности по",
                DataPropertyName = "EndDate",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 120
            });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colAction",
                HeaderText = "Тип операции",
                DataPropertyName = "Action",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 120
            });
        }
        private void Reload()
        {
            dgvHistory.DataSource = _db.Employee_History(_employeeId);
            if (dgvHistory.Columns["Id"] != null)
                dgvHistory.Columns["Id"].Visible = false;
        }
    }
}
