using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionTime.Forms
{
    public partial class DBSelect : Form
    {
        public string SelectedDbPath { get; private set; }
        private string DbFolder => AppDomain.CurrentDomain.BaseDirectory;
        public DBSelect()
        {
            InitializeComponent();
        }

        private void DBSelect_Load(object sender, EventArgs e)
        {
            LoadExistingDatabases();
            UpdateButtonsState();
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            if (lbDBList.SelectedItem == null)
                return;

            string fileName = lbDBList.SelectedItem.ToString();
            string fullPath = Path.Combine(DbFolder, fileName);

            if (!DbSchema.Validate(fullPath, out var err))
            {
                MessageBox.Show("Файл БД не соответствует структуре:\n" + err, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedDbPath = fullPath;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            using (var dlg = new DBSelect_create())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                string name = dlg.DbName;

                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    MessageBox.Show("Имя содержит недопустимые символы.", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    name += ".db";

                string fullPath = Path.Combine(DbFolder, name);

                if (File.Exists(fullPath))
                {
                    MessageBox.Show("База данных с таким именем уже существует.", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    DbSchema.CreateNew(fullPath);

                    LoadExistingDatabases();
                    UpdateButtonsState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось создать БД:\n" + ex.Message, "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lbDBList.SelectedItem == null)
                return;

            string fileName = lbDBList.SelectedItem.ToString();
            string fullPath = Path.Combine(DbFolder, fileName);

            if (!File.Exists(fullPath))
            {
                MessageBox.Show("Файл базы данных не найден.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                LoadExistingDatabases();
                UpdateButtonsState();
                return;
            }

            var res = MessageBox.Show(
                $"Удалить базу данных \"{fileName}\"?",
                "Подтверждение удаления",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res != DialogResult.Yes)
                return;

            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                SQLiteConnection.ClearAllPools();

                File.Delete(fullPath);

                LoadExistingDatabases();

                if (lbDBList.Items.Count > 0)
                {
                    lbDBList.SelectedIndex = 0;
                }

                UpdateButtonsState();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить базу данных:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        private void LoadExistingDatabases()
        {
            lbDBList.Items.Clear();

            if (!Directory.Exists(DbFolder)) return;

            string[] dbFiles;
            try
            {
                dbFiles = Directory.GetFiles(DbFolder, "*.db", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            lbDBList.BeginUpdate();
            try
            {
                foreach (var fullPath in dbFiles)
                {
                    var fileName = Path.GetFileName(fullPath);
                    lbDBList.Items.Add(fileName);
                }
            }
            finally
            {
                lbDBList.EndUpdate();
                if (lbDBList.Items.Count > 0)
                {
                    lbDBList.SelectedIndex = 0;
                    UpdateButtonsState();
                }
            }
        }
        private void UpdateButtonsState()
        {
            bool hasSelection = lbDBList.SelectedIndex != -1;
            btnSelect.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection;
        }
    }
}
