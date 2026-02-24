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
    public partial class EmployeePosition_Edit : Form
    {
        public string PositionName => (tbName.Text ?? "").Trim();
        public EmployeePosition_Edit(string title, string initialValue, string btnTitle)
        {
            InitializeComponent();

            tbName.Text = initialValue ?? "";
            tbName.SelectAll();
            tbName.Focus();

            this.Text = title;
            btnOK.Text = btnTitle;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbName.Text))
            {
                MessageBox.Show("Введите название.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbName.Focus();
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
