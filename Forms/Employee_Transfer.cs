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
    public partial class Employee_Transfer : Form
    {
        private readonly DbService _db;
        private readonly int _curDeptId;
        private readonly int _curPosId;
        private bool _loading;

        public int DepartmentId => Convert.ToInt32(cbDepartment.SelectedValue);
        public int PositionId => Convert.ToInt32(cbPosition.SelectedValue);
        public DateTime TransferDate => dtTransfer.Value.Date;
        public Employee_Transfer(DbService db, string fio, DateTime defaultDate, int currentDeptId, int currentPosId)
        {
            InitializeComponent();
            _db = db;
            _curDeptId = currentDeptId;
            _curPosId = currentPosId;

            dtTransfer.Value = defaultDate;

            this.Text = $"Сотрудник {fio}: Перевод на должность";
        }
        private void Employee_Transfer_Load(object sender, EventArgs e)
        {
            _loading = true;
            try
            {
                var dtDep = _db.Departments_ListForComboAnyLevel();
                cbDepartment.DisplayMember = "Name";
                cbDepartment.ValueMember = "Id";
                cbDepartment.DataSource = dtDep;

                var dtPos = _db.Positions_List();
                cbPosition.DisplayMember = "Name";
                cbPosition.ValueMember = "Id";
                cbPosition.DataSource = dtPos;

                cbDepartment.SelectedValue = _curDeptId;
                if (cbDepartment.SelectedValue == null || cbDepartment.SelectedValue == DBNull.Value)
                    cbDepartment.SelectedIndex = (cbDepartment.Items.Count > 0) ? 0 : -1;

                cbPosition.SelectedValue = _curPosId;
                if (cbPosition.SelectedValue == null || cbPosition.SelectedValue == DBNull.Value)
                    cbPosition.SelectedIndex = (cbPosition.Items.Count > 0) ? 0 : -1;
            }
            finally
            {
                _loading = false;
            }
        }
        private void btnTransfer_Click(object sender, EventArgs e)
        {
            if (cbDepartment.SelectedValue == null || cbPosition.SelectedValue == null)
            {
                MessageBox.Show("Выберите подразделение и должность.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            long newDep = Convert.ToInt64(cbDepartment.SelectedValue);
            long newPos = Convert.ToInt64(cbPosition.SelectedValue);

            if (newDep == _curDeptId && newPos == _curPosId)
            {
                MessageBox.Show("Вы не изменили подразделение и должность.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            this.DialogResult = DialogResult.OK;
            Close();
        }
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
