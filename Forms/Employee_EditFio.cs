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
    public partial class Employee_EditFio : Form
    {
        public string newFio => tbName.Text.Trim();
        public Employee_EditFio(string fio)
        {
            InitializeComponent();

            tbName.Text = fio;
            this.Text = $"Сотрудник {fio}: Изменить ФИО";
        }

        private void btnChange_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(newFio))
            {
                MessageBox.Show("Введите ФИО.", "Ошибка",
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
