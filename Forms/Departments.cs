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
    public partial class Departments : Form
    {
        private readonly DbService _db;
        public Departments(DbService db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            InitializeComponent();
            _db = db;
        }

        private void Departments_Load(object sender, EventArgs e)
        {
            LoadDepartmentsTree();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            var selected = tvDepartments.SelectedNode;

            if (selected != null)
            {
                int level = GetNodeLevel(selected);
                if (level >= 4)
                {
                    MessageBox.Show("Внутрь 'Группа' добавлять нельзя.", "Ограничение",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            long? parentId = null;
            DepartmentLevel? parentLevel = null;
            string parentDisplay = null;

            if (selected != null)
            {
                parentId = (long)selected.Tag;
                parentLevel = (DepartmentLevel)GetNodeLevel(selected);
                parentDisplay = selected.Text;
            }

            using (var dlg = new Departments_Create(parentId, parentLevel, parentDisplay))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                _db.Department_Create(dlg.DepartmentName, dlg.NewLevel, dlg.ParentId);
                LoadDepartmentsTree();
            }
        }

        private void btnChange_Click(object sender, EventArgs e)
        {
            var node = tvDepartments.SelectedNode;
            if (node == null)
            {
                MessageBox.Show("Выберите подразделение в дереве.", "Нет выбора",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long id = (long)node.Tag;
            string currentName = node.Text;

            using (var dlg = new Departments_Edit(currentName))
            {
                dlg.Icon = this.Icon;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                string newName = dlg.NewName;

                try
                {
                    int affected = _db.Department_Update(id, newName);
                    if (affected <= 0)
                    {
                        MessageBox.Show("Запись не была обновлена (возможно, её уже удалили).", "Предупреждение",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        LoadDepartmentsTree();
                        return;
                    }

                    LoadDepartmentsTree();
                    SelectNodeById(id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось изменить название:\n" + ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var node = tvDepartments.SelectedNode;
            if (node == null)
            {
                MessageBox.Show("Выберите подразделение в дереве.", "Нет выбора",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (node.Nodes != null && node.Nodes.Count > 0)
            {
                MessageBox.Show("Нельзя удалить элемент, у которого есть вложенные подразделения.\n" +
                                "Сначала удалите дочерние элементы.",
                    "Удаление запрещено",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            long id = (long)node.Tag;
            string name = node.Text;

            var res = MessageBox.Show(
                $"Удалить \"{name}\"?",
                "Подтверждение удаления",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res != DialogResult.Yes)
                return;

            try
            {
                long? parentId = null;
                if (node.Parent != null && node.Parent.Tag is long)
                    parentId = (long)node.Parent.Tag;

                int affected = _db.Department_Delete(id);

                if (affected <= 0)
                {
                    MessageBox.Show("Запись не была удалена (возможно, её уже удалили).", "Предупреждение",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                LoadDepartmentsTree();

                if (parentId.HasValue)
                {
                    SelectNodeById(parentId.Value);
                }
                else if (tvDepartments.Nodes.Count > 0)
                {
                    tvDepartments.SelectedNode = tvDepartments.Nodes[0];
                    tvDepartments.Nodes[0].EnsureVisible();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void LoadDepartmentsTree()
        {
            tvDepartments.BeginUpdate();
            try
            {
                tvDepartments.Nodes.Clear();

                DataTable dt = _db.Departments_List();

                var nodesById = new Dictionary<long, TreeNode>();

                foreach (DataRow row in dt.Rows)
                {
                    long id = Convert.ToInt64(row["Id"]);
                    string name = Convert.ToString(row["Name"]);
                    int level = Convert.ToInt32(row["Level"]);

                    string text = name;

                    var node = new TreeNode(text);
                    node.Tag = id;
                    nodesById[id] = node;
                }

                foreach (DataRow row in dt.Rows)
                {
                    long id = Convert.ToInt64(row["Id"]);
                    object parentObj = row["ParentId"];

                    TreeNode node = nodesById[id];

                    if (parentObj == DBNull.Value)
                    {
                        tvDepartments.Nodes.Add(node);
                    }
                    else
                    {
                        long parentId = Convert.ToInt64(parentObj);

                        TreeNode parentNode;
                        if (nodesById.TryGetValue(parentId, out parentNode))
                            parentNode.Nodes.Add(node);
                        else
                            tvDepartments.Nodes.Add(node);
                    }
                }

                tvDepartments.ExpandAll();
            }
            finally
            {
                tvDepartments.EndUpdate();
            }
        }
        private int GetNodeLevel(TreeNode node)
        {
            int depth = 0;
            var n = node;
            while (n.Parent != null) { depth++; n = n.Parent; }
            return 1 + depth;
        }
        private bool SelectNodeById(long id)
        {
            foreach (TreeNode n in tvDepartments.Nodes)
            {
                var found = FindNodeById(n, id);
                if (found != null)
                {
                    tvDepartments.SelectedNode = found;
                    found.EnsureVisible();
                    return true;
                }
            }
            return false;
        }
        private TreeNode FindNodeById(TreeNode node, long id)
        {
            if (node != null && node.Tag is long && (long)node.Tag == id)
                return node;

            foreach (TreeNode ch in node.Nodes)
            {
                var found = FindNodeById(ch, id);
                if (found != null) return found;
            }
            return null;
        }
        private void tvDepartments_AfterSelect(object sender, TreeViewEventArgs e)
        {
            bool has = tvDepartments.SelectedNode != null;
            btnChange.Enabled = has;
            btnDelete.Enabled = has;
        }
    }
}
