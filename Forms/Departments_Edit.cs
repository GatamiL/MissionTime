using System;
using System.Data;
using System.Windows.Forms;

namespace MissionTime.Forms
{
    public partial class Departments_Edit : Form
    {
        private readonly DbService _db;
        private readonly long _departmentId;

        public string NewName => (tbName.Text ?? "").Trim();
        public long? ResponsibleId { get; private set; } // Свойство для возврата результата

        // Конструктор теперь принимает БД и ID отдела, а не строку!
        public Departments_Edit(DbService db, long departmentId)
        {
            InitializeComponent();

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _departmentId = departmentId;

            LoadEmployees();
            LoadCurrentDepartmentData();

            // Подписываемся на чекбокс
            checkBox1.CheckedChanged += (s, e) => { cbResp.Enabled = checkBox1.Checked; };

            tbName.SelectAll();
            tbName.Focus();
        }

        private void LoadEmployees()
        {
            var dt = _db.Query("SELECT Id, Fio FROM Employees ORDER BY Fio");
            cbResp.DisplayMember = "Fio";
            cbResp.ValueMember = "Id";
            cbResp.DataSource = dt;
            cbResp.SelectedIndex = -1;
        }

        private void LoadCurrentDepartmentData()
        {
            // Вытаскиваем ЧИСТОЕ имя и текущего ответственного напрямую из базы
            var dt = _db.Query("SELECT Name, ResponsibleId FROM Departments WHERE Id = @id",
                new System.Data.SQLite.SQLiteParameter("@id", _departmentId));

            if (dt.Rows.Count > 0)
            {
                tbName.Text = Convert.ToString(dt.Rows[0]["Name"]);

                if (dt.Rows[0]["ResponsibleId"] != DBNull.Value)
                {
                    checkBox1.Checked = true;
                    cbResp.Enabled = true;
                    cbResp.SelectedValue = Convert.ToInt64(dt.Rows[0]["ResponsibleId"]);
                }
                else
                {
                    checkBox1.Checked = false;
                    cbResp.Enabled = false;
                }
            }
        }

        private void btnChange_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbName.Text))
            {
                MessageBox.Show("Введите название.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbName.Focus();
                return;
            }

            // Проверяем ответственного
            if (checkBox1.Checked)
            {
                if (cbResp.SelectedValue == null)
                {
                    MessageBox.Show("Выберите ответственного из списка или снимите галочку.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ResponsibleId = Convert.ToInt64(cbResp.SelectedValue);
            }
            else
            {
                ResponsibleId = null; // Если галочка снята, затираем ответственного (NULL)
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