using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MissionTime.Forms
{
    public partial class Departments_Edit : Form
    {
        public string NewName => (tbName.Text ?? "").Trim();
        public Departments_Edit(string currentName)
        {
            InitializeComponent();

            tbName.Text = currentName ?? "";
            tbName.SelectAll();
            tbName.Focus();
        }

        private void btnChange_Click(object sender, EventArgs e)
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
