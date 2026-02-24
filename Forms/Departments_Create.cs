using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MissionTime.DbSchema;

namespace MissionTime.Forms
{
    public partial class Departments_Create : Form
    {
        public string DepartmentName { get; private set; }
        public DepartmentLevel NewLevel { get; private set; }
        public long? ParentId { get; private set; }
        public Departments_Create(long? parentId, DepartmentLevel? parentLevel, string parentDisplayName)
        {
            InitializeComponent();

            if (!parentLevel.HasValue)
            {
                NewLevel = DepartmentLevel.Center;
                ParentId = null;
                lblParent.Text = "Родитель: (нет)";
            }
            else
            {
                NewLevel = (DepartmentLevel)((int)parentLevel.Value + 1);
                ParentId = parentId;
                lblParent.Text = "Родитель: " + (parentDisplayName ?? "");
            }

            lblType.Text = "Добавляем: " + LevelToCaption(NewLevel);
            tbName.Text = "";
            tbName.Focus();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            var name = (tbName.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbName.Focus();
                return;
            }

            DepartmentName = name;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
        private string LevelToCaption(DepartmentLevel level)
        {
            switch (level)
            {
                case DepartmentLevel.Center: return "Центр";
                case DepartmentLevel.Complex: return "Комплекс";
                case DepartmentLevel.Department: return "Отдел";
                case DepartmentLevel.Group: return "Группа";
                default: return "Подразделение";
            }
        }
    }
}
