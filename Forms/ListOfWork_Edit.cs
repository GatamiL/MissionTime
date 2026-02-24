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
    public partial class ListOfWork_Edit : Form
    {
        public string WorkName => tbName.Text.Trim();
        public string SpecialCode => tbCode.Text.Trim();
        public ListOfWork_Edit(string title, string name, string code, string btnTitle)
        {
            InitializeComponent();

            this.Text = title;
            tbName.Text = name ?? "";
            tbCode.Text = code ?? "";
            btnOk.Text = btnTitle;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(WorkName))
            {
                MessageBox.Show("Введите наименование работы.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbName.Focus();
                this.DialogResult = DialogResult.None;
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
