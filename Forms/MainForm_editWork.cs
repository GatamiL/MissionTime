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
    public partial class MainForm_editWork : Form
    {
        private readonly DbService _db;

        public long WorkId { get; private set; }
        public string WorkDisplayName { get; private set; }
        public MainForm_editWork(DbService db)
        {
            InitializeComponent();

            _db = db;
        }
        private void MainForm_editWork_Load(object sender, EventArgs e)
        {
            var dt = _db.ListOfWork_AllForCombo();

            cbWork.DisplayMember = "DisplayName";
            cbWork.ValueMember = "Id";
            cbWork.DataSource = dt;

            cbWork.SelectedIndex = dt.Rows.Count > 0 ? 0 : -1;
        }
        private void btnOk_Click(object sender, EventArgs e)
        {
            if (cbWork.SelectedItem == null)
            {
                MessageBox.Show("Выберите вид работ.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Надёжно вытаскиваем выбранное
            if (cbWork.SelectedItem is DataRowView drv)
            {
                WorkId = Convert.ToInt64(drv.Row["Id"]);
                WorkDisplayName = Convert.ToString(drv.Row["DisplayName"]);
            }
            else
            {
                // fallback
                WorkId = Convert.ToInt64(cbWork.SelectedValue);
                WorkDisplayName = Convert.ToString(cbWork.Text);
            }

            if (WorkId <= 0)
            {
                MessageBox.Show("Некорректный вид работ.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
