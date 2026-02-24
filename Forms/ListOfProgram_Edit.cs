using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MissionTime.Forms
{
    public partial class ListOfProgram_Edit : Form
    {
        public string ProgramName => (tbName.Text ?? "").Trim();
        public string ShortName => (tbShortName.Text ?? "").Trim();
        public DateTime DateStart => dtStart.Value.Date;
        public DateTime? DateEnd => cbNoEnd.Checked ? (DateTime?)null : dtEnd.Value.Date;
        public ListOfProgram_Edit(string title, string name, string shortName, DateTime dateStart, DateTime? dateEnd, string btnTitle)
        {
            InitializeComponent();
            this.Text = title;

            tbName.Text = name ?? "";
            tbShortName.Text = shortName ?? "";

            dtStart.Value = dateStart == default(DateTime) ? DateTime.Today : dateStart.Date;

            if (dateEnd.HasValue)
            {
                cbNoEnd.Checked = false;
                dtEnd.Enabled = true;
                dtEnd.Value = dateEnd.Value.Date;
            }
            else
            {
                cbNoEnd.Checked = true;
                dtEnd.Enabled = false;
                dtEnd.Value = DateTime.Today;
            }
            btnOk.Text = btnTitle;

            cbNoEnd.CheckedChanged += (s, e) => dtEnd.Enabled = !cbNoEnd.Checked;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbName.Text))
            {
                MessageBox.Show("Введите наименование.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(tbShortName.Text))
            {
                MessageBox.Show("Введите краткое наименование.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbShortName.Focus();
                return;
            }

            if (!cbNoEnd.Checked && dtEnd.Value.Date < dtStart.Value.Date)
            {
                MessageBox.Show("Дата конца не может быть меньше даты начала.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
