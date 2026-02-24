namespace MissionTime.Forms
{
    partial class Employee
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
            this.cbFired = new System.Windows.Forms.CheckBox();
            this.btnPositions = new System.Windows.Forms.Button();
            this.btnCreate = new System.Windows.Forms.Button();
            this.dgvEmployee = new System.Windows.Forms.DataGridView();
            this.btnTransfer = new System.Windows.Forms.Button();
            this.btnFire = new System.Windows.Forms.Button();
            this.btnChangeFio = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgvEmployee)).BeginInit();
            this.SuspendLayout();
            // 
            // cbFired
            // 
            this.cbFired.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbFired.AutoSize = true;
            this.cbFired.Location = new System.Drawing.Point(12, 520);
            this.cbFired.Margin = new System.Windows.Forms.Padding(5);
            this.cbFired.Name = "cbFired";
            this.cbFired.Size = new System.Drawing.Size(165, 20);
            this.cbFired.TabIndex = 24;
            this.cbFired.Text = "Показывать уволенных";
            this.cbFired.UseVisualStyleBackColor = true;
            // 
            // btnPositions
            // 
            this.btnPositions.Location = new System.Drawing.Point(482, 12);
            this.btnPositions.Name = "btnPositions";
            this.btnPositions.Size = new System.Drawing.Size(148, 23);
            this.btnPositions.TabIndex = 23;
            this.btnPositions.Text = "История переводов";
            this.btnPositions.UseVisualStyleBackColor = true;
            this.btnPositions.Click += new System.EventHandler(this.btnPositions_Click);
            // 
            // btnCreate
            // 
            this.btnCreate.Location = new System.Drawing.Point(12, 12);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(85, 23);
            this.btnCreate.TabIndex = 22;
            this.btnCreate.Text = "Добавить";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            // 
            // dgvEmployee
            // 
            this.dgvEmployee.AllowUserToAddRows = false;
            this.dgvEmployee.AllowUserToDeleteRows = false;
            this.dgvEmployee.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvEmployee.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvEmployee.Location = new System.Drawing.Point(12, 43);
            this.dgvEmployee.Margin = new System.Windows.Forms.Padding(5);
            this.dgvEmployee.MultiSelect = false;
            this.dgvEmployee.Name = "dgvEmployee";
            this.dgvEmployee.ReadOnly = true;
            this.dgvEmployee.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvEmployee.Size = new System.Drawing.Size(907, 467);
            this.dgvEmployee.TabIndex = 21;
            // 
            // btnTransfer
            // 
            this.btnTransfer.Location = new System.Drawing.Point(221, 12);
            this.btnTransfer.Name = "btnTransfer";
            this.btnTransfer.Size = new System.Drawing.Size(168, 23);
            this.btnTransfer.TabIndex = 26;
            this.btnTransfer.Text = "Перевести на должность";
            this.btnTransfer.UseVisualStyleBackColor = true;
            this.btnTransfer.Click += new System.EventHandler(this.btnTransfer_Click);
            // 
            // btnFire
            // 
            this.btnFire.Location = new System.Drawing.Point(395, 12);
            this.btnFire.Name = "btnFire";
            this.btnFire.Size = new System.Drawing.Size(81, 23);
            this.btnFire.TabIndex = 25;
            this.btnFire.Text = "Уволить";
            this.btnFire.UseVisualStyleBackColor = true;
            this.btnFire.Click += new System.EventHandler(this.btnFire_Click);
            // 
            // btnChangeFio
            // 
            this.btnChangeFio.Location = new System.Drawing.Point(103, 12);
            this.btnChangeFio.Name = "btnChangeFio";
            this.btnChangeFio.Size = new System.Drawing.Size(112, 23);
            this.btnChangeFio.TabIndex = 27;
            this.btnChangeFio.Text = "Изменить ФИО";
            this.btnChangeFio.UseVisualStyleBackColor = true;
            this.btnChangeFio.Click += new System.EventHandler(this.btnChangeFio_Click);
            // 
            // Employee
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(933, 554);
            this.Controls.Add(this.btnChangeFio);
            this.Controls.Add(this.cbFired);
            this.Controls.Add(this.btnPositions);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.dgvEmployee);
            this.Controls.Add(this.btnTransfer);
            this.Controls.Add(this.btnFire);
            this.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "Employee";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Справочник: Сотрудники";
            this.Load += new System.EventHandler(this.Employee_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvEmployee)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbFired;
        private System.Windows.Forms.Button btnPositions;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.DataGridView dgvEmployee;
        private System.Windows.Forms.Button btnTransfer;
        private System.Windows.Forms.Button btnFire;
        private System.Windows.Forms.Button btnChangeFio;
    }
}