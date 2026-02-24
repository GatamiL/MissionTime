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
    public partial class Employee_Fire : Form
    {
        public DateTime FireDate => dtFire.Value.Date;
        public Employee_Fire(string fio, DateTime defaultDate)
        {
            InitializeComponent();

            this.Text = $"Сотрудник {fio}: Уволить";
            dtFire.Value = defaultDate == default(DateTime) ? DateTime.Today : defaultDate.Date;
        }

        private void btnFire_Click(object sender, EventArgs e)
        {
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
