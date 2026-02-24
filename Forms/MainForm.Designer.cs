namespace MissionTime.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.dgvMain = new System.Windows.Forms.DataGridView();
            this.tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.cbProgram = new System.Windows.Forms.ComboBox();
            this.cbDepartment = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.cbMonth = new System.Windows.Forms.ComboBox();
            this.cbYear = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.menuAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMainAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.menuReportForDivision = new System.Windows.Forms.ToolStripMenuItem();
            this.menuReportForDpartment = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMainReports = new System.Windows.Forms.ToolStripMenuItem();
            this.menuReportFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.menuListOfPrograms = new System.Windows.Forms.ToolStripMenuItem();
            this.menuListOfWork = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEmployeePosition = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDepartments = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEmployees = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMainManual = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMainFile = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMain)).BeginInit();
            this.tableLayoutPanel.SuspendLayout();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgvMain
            // 
            this.dgvMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvMain.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvMain.Location = new System.Drawing.Point(12, 63);
            this.dgvMain.Name = "dgvMain";
            this.dgvMain.Size = new System.Drawing.Size(1260, 550);
            this.dgvMain.TabIndex = 15;
            // 
            // tableLayoutPanel
            // 
            this.tableLayoutPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel.ColumnCount = 8;
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 55F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 85F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.tableLayoutPanel.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel.Controls.Add(this.cbProgram, 7, 0);
            this.tableLayoutPanel.Controls.Add(this.cbDepartment, 1, 0);
            this.tableLayoutPanel.Controls.Add(this.label4, 6, 0);
            this.tableLayoutPanel.Controls.Add(this.label2, 2, 0);
            this.tableLayoutPanel.Controls.Add(this.cbMonth, 5, 0);
            this.tableLayoutPanel.Controls.Add(this.cbYear, 3, 0);
            this.tableLayoutPanel.Controls.Add(this.label3, 4, 0);
            this.tableLayoutPanel.Location = new System.Drawing.Point(12, 27);
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            this.tableLayoutPanel.RowCount = 1;
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel.Size = new System.Drawing.Size(1260, 30);
            this.tableLayoutPanel.TabIndex = 13;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(104, 30);
            this.label1.TabIndex = 1;
            this.label1.Text = "Подразделение";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cbProgram
            // 
            this.cbProgram.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbProgram.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbProgram.FormattingEnabled = true;
            this.cbProgram.Location = new System.Drawing.Point(976, 3);
            this.cbProgram.Name = "cbProgram";
            this.cbProgram.Size = new System.Drawing.Size(281, 24);
            this.cbProgram.TabIndex = 8;
            // 
            // cbDepartment
            // 
            this.cbDepartment.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbDepartment.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbDepartment.FormattingEnabled = true;
            this.cbDepartment.Location = new System.Drawing.Point(113, 3);
            this.cbDepartment.Name = "cbDepartment";
            this.cbDepartment.Size = new System.Drawing.Size(527, 24);
            this.cbDepartment.TabIndex = 2;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(891, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(79, 30);
            this.label4.TabIndex = 7;
            this.label4.Text = "Программа";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(646, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(34, 30);
            this.label2.TabIndex = 3;
            this.label2.Text = "Год";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cbMonth
            // 
            this.cbMonth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbMonth.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMonth.FormattingEnabled = true;
            this.cbMonth.Location = new System.Drawing.Point(801, 3);
            this.cbMonth.Name = "cbMonth";
            this.cbMonth.Size = new System.Drawing.Size(84, 24);
            this.cbMonth.TabIndex = 6;
            // 
            // cbYear
            // 
            this.cbYear.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbYear.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbYear.FormattingEnabled = true;
            this.cbYear.Location = new System.Drawing.Point(686, 3);
            this.cbYear.Name = "cbYear";
            this.cbYear.Size = new System.Drawing.Size(54, 24);
            this.cbYear.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(746, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(49, 30);
            this.label3.TabIndex = 5;
            this.label3.Text = "Месяц";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // menuAbout
            // 
            this.menuAbout.Name = "menuAbout";
            this.menuAbout.Size = new System.Drawing.Size(153, 22);
            this.menuAbout.Text = "О программе";
            this.menuAbout.Click += new System.EventHandler(this.menuAbout_Click);
            // 
            // menuMainAbout
            // 
            this.menuMainAbout.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuAbout});
            this.menuMainAbout.Font = new System.Drawing.Font("Arial", 9.75F);
            this.menuMainAbout.Name = "menuMainAbout";
            this.menuMainAbout.Size = new System.Drawing.Size(69, 20);
            this.menuMainAbout.Text = "Справка";
            // 
            // menuReportForDivision
            // 
            this.menuReportForDivision.Name = "menuReportForDivision";
            this.menuReportForDivision.Size = new System.Drawing.Size(185, 22);
            this.menuReportForDivision.Text = "Отчет за комплекс";
            this.menuReportForDivision.Click += new System.EventHandler(this.menuReportForDivision_Click);
            // 
            // menuReportForDpartment
            // 
            this.menuReportForDpartment.Name = "menuReportForDpartment";
            this.menuReportForDpartment.Size = new System.Drawing.Size(185, 22);
            this.menuReportForDpartment.Text = "Отчет за отдел";
            this.menuReportForDpartment.Click += new System.EventHandler(this.menuReportForDpartment_Click);
            // 
            // menuMainReports
            // 
            this.menuMainReports.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuReportForDpartment,
            this.menuReportForDivision,
            this.menuReportFolder});
            this.menuMainReports.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.menuMainReports.Name = "menuMainReports";
            this.menuMainReports.Size = new System.Drawing.Size(62, 20);
            this.menuMainReports.Text = "Отчеты";
            // 
            // menuReportFolder
            // 
            this.menuReportFolder.Name = "menuReportFolder";
            this.menuReportFolder.Size = new System.Drawing.Size(185, 22);
            this.menuReportFolder.Text = "Папка с отчетами";
            this.menuReportFolder.Click += new System.EventHandler(this.menuReportFolder_Click);
            // 
            // menuListOfPrograms
            // 
            this.menuListOfPrograms.Name = "menuListOfPrograms";
            this.menuListOfPrograms.Size = new System.Drawing.Size(196, 22);
            this.menuListOfPrograms.Text = "Программы пусков";
            this.menuListOfPrograms.Click += new System.EventHandler(this.menuListOfPrograms_Click);
            // 
            // menuListOfWork
            // 
            this.menuListOfWork.Name = "menuListOfWork";
            this.menuListOfWork.Size = new System.Drawing.Size(196, 22);
            this.menuListOfWork.Text = "Номенклатура работ";
            this.menuListOfWork.Click += new System.EventHandler(this.menuListOfWork_Click);
            // 
            // menuEmployeePosition
            // 
            this.menuEmployeePosition.Name = "menuEmployeePosition";
            this.menuEmployeePosition.Size = new System.Drawing.Size(196, 22);
            this.menuEmployeePosition.Text = "Должности";
            this.menuEmployeePosition.Click += new System.EventHandler(this.menuEmployeePosition_Click);
            // 
            // menuDepartments
            // 
            this.menuDepartments.Name = "menuDepartments";
            this.menuDepartments.Size = new System.Drawing.Size(196, 22);
            this.menuDepartments.Text = "Подразделения";
            this.menuDepartments.Click += new System.EventHandler(this.menuDepartments_Click);
            // 
            // menuEmployees
            // 
            this.menuEmployees.Name = "menuEmployees";
            this.menuEmployees.Size = new System.Drawing.Size(196, 22);
            this.menuEmployees.Text = "Сотрудники";
            this.menuEmployees.Click += new System.EventHandler(this.menuEmployees_Click);
            // 
            // menuMainManual
            // 
            this.menuMainManual.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuEmployees,
            this.menuDepartments,
            this.menuEmployeePosition,
            this.menuListOfWork,
            this.menuListOfPrograms});
            this.menuMainManual.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.menuMainManual.Name = "menuMainManual";
            this.menuMainManual.Size = new System.Drawing.Size(97, 20);
            this.menuMainManual.Text = "Справочники";
            // 
            // menuExit
            // 
            this.menuExit.Name = "menuExit";
            this.menuExit.Size = new System.Drawing.Size(180, 22);
            this.menuExit.Text = "Выход";
            this.menuExit.Click += new System.EventHandler(this.menuExit_Click);
            // 
            // menuMainFile
            // 
            this.menuMainFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuExit});
            this.menuMainFile.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.menuMainFile.Name = "menuMainFile";
            this.menuMainFile.Size = new System.Drawing.Size(51, 20);
            this.menuMainFile.Text = "Файл";
            // 
            // statusStrip
            // 
            this.statusStrip.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.statusStrip.Location = new System.Drawing.Point(0, 616);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1284, 22);
            this.statusStrip.TabIndex = 14;
            this.statusStrip.Text = "statusStrip1";
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuMainFile,
            this.menuMainManual,
            this.menuMainReports,
            this.menuMainAbout});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1284, 24);
            this.menuStrip.TabIndex = 12;
            this.menuStrip.Text = "menuStrip1";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1284, 638);
            this.Controls.Add(this.dgvMain);
            this.Controls.Add(this.tableLayoutPanel);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Mission Time - Учёт трудозатрат космических программ";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvMain)).EndInit();
            this.tableLayoutPanel.ResumeLayout(false);
            this.tableLayoutPanel.PerformLayout();
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvMain;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cbProgram;
        private System.Windows.Forms.ComboBox cbDepartment;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbMonth;
        private System.Windows.Forms.ComboBox cbYear;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ToolStripMenuItem menuAbout;
        private System.Windows.Forms.ToolStripMenuItem menuMainAbout;
        private System.Windows.Forms.ToolStripMenuItem menuReportForDivision;
        private System.Windows.Forms.ToolStripMenuItem menuReportForDpartment;
        private System.Windows.Forms.ToolStripMenuItem menuMainReports;
        private System.Windows.Forms.ToolStripMenuItem menuListOfPrograms;
        private System.Windows.Forms.ToolStripMenuItem menuListOfWork;
        private System.Windows.Forms.ToolStripMenuItem menuEmployeePosition;
        private System.Windows.Forms.ToolStripMenuItem menuDepartments;
        private System.Windows.Forms.ToolStripMenuItem menuEmployees;
        private System.Windows.Forms.ToolStripMenuItem menuMainManual;
        private System.Windows.Forms.ToolStripMenuItem menuExit;
        private System.Windows.Forms.ToolStripMenuItem menuMainFile;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuReportFolder;
    }
}