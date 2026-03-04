using System;
using System.Data;
using System.Windows.Forms;
using static MissionTime.DbSchema;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace MissionTime.Forms
{
    public partial class Departments_Create : Form
    {
        private readonly DbService _db; // <-- Добавили для работы с БД

        public string DepartmentName { get; private set; }
        public DepartmentLevel NewLevel { get; private set; }
        public long? ParentId { get; private set; }
        public long? ResponsibleId { get; private set; } // <-- Новое свойство для ответственного

        // Обрати внимание, я добавил DbService db в конструктор!
        public Departments_Create(DbService db, long? parentId, DepartmentLevel? parentLevel, string parentDisplayName)
        {
            InitializeComponent();
            _db = db ?? throw new ArgumentNullException(nameof(db));

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

            // --- НАСТРОЙКА ВЫПАДАЮЩЕГО СПИСКА ---
            LoadEmployees();
            cbResp.Enabled = checkBox1.Checked; // Замени checkBox1 на имя твоего чекбокса, если оно другое

            // Подписываемся на клик по чекбоксу
            checkBox1.CheckedChanged += (s, e) => { cbResp.Enabled = checkBox1.Checked; };

            tbName.Focus();
        }

        private void LoadEmployees()
        {
            // Берем список всех сотрудников (пишу прямой SQL, чтобы точно сработало)
            var dt = _db.Query("SELECT Id, Fio FROM Employees ORDER BY Fio");
            cbResp.DisplayMember = "Fio";
            cbResp.ValueMember = "Id";
            cbResp.DataSource = dt;
            cbResp.SelectedIndex = -1; // Чтобы изначально никто не был выбран
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            var name = (tbName.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbName.Focus();
                return;
            }

            // --- ПРОВЕРЯЕМ ОТВЕТСТВЕННОГО ---
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
                ResponsibleId = null;
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