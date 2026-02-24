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
    public partial class Employee_Create : Form
    {
        private readonly DbService _db;
        public string Fio => (tbFio.Text ?? "").Trim();
        public int PositionId => Convert.ToInt32(((DataRowView)cbPosition.SelectedItem)["Id"]);
        public int DepartmentId => Convert.ToInt32(((DataRowView)cbDepartment.SelectedItem)["Id"]);
        public DateTime HireDate => dtHire.Value.Date;
        public Employee_Create(DbService db)
        {
            InitializeComponent();

            _db = db;
        }
        private void Employee_Create_Load(object sender, EventArgs e)
        {
            cbPosition.DisplayMember = "Name";
            cbPosition.ValueMember = "Id";
            cbPosition.DataSource = _db.Positions_List();

            cbDepartment.DisplayMember = "Name";
            cbDepartment.ValueMember = "Id";
            cbDepartment.DataSource = _db.Departments_ListForComboAnyLevel();

            dtHire.Value = DateTime.Today;
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbFio.Text))
            {
                MessageBox.Show("Введите ФИО.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbFio.Focus();
                return;
            }

            if (cbDepartment.SelectedItem == null)
            {
                MessageBox.Show("Выберите подразделение.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cbPosition.SelectedItem == null)
            {
                MessageBox.Show("Выберите должность.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
