namespace MissionTime.Forms
{
    partial class EmployeePositions
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
            this.dgvPositions = new System.Windows.Forms.DataGridView();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnChange = new System.Windows.Forms.Button();
            this.btnCreate = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPositions)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvPositions
            // 
            this.dgvPositions.AllowUserToAddRows = false;
            this.dgvPositions.AllowUserToDeleteRows = false;
            this.dgvPositions.AllowUserToResizeColumns = false;
            this.dgvPositions.AllowUserToResizeRows = false;
            this.dgvPositions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvPositions.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvPositions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPositions.Location = new System.Drawing.Point(13, 45);
            this.dgvPositions.Margin = new System.Windows.Forms.Padding(4);
            this.dgvPositions.MultiSelect = false;
            this.dgvPositions.Name = "dgvPositions";
            this.dgvPositions.ReadOnly = true;
            this.dgvPositions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvPositions.Size = new System.Drawing.Size(1032, 541);
            this.dgvPositions.TabIndex = 13;
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(202, 10);
            this.btnDelete.Margin = new System.Windows.Forms.Padding(4);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(88, 28);
            this.btnDelete.TabIndex = 16;
            this.btnDelete.Text = "Удалить";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnChange
            // 
            this.btnChange.Location = new System.Drawing.Point(107, 10);
            this.btnChange.Margin = new System.Windows.Forms.Padding(4);
            this.btnChange.Name = "btnChange";
            this.btnChange.Size = new System.Drawing.Size(88, 28);
            this.btnChange.TabIndex = 15;
            this.btnChange.Text = "Изменить";
            this.btnChange.UseVisualStyleBackColor = true;
            this.btnChange.Click += new System.EventHandler(this.btnChange_Click);
            // 
            // btnCreate
            // 
            this.btnCreate.Location = new System.Drawing.Point(13, 10);
            this.btnCreate.Margin = new System.Windows.Forms.Padding(4);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(88, 28);
            this.btnCreate.TabIndex = 14;
            this.btnCreate.Text = "Добавить";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            // 
            // EmployeePositions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1058, 599);
            this.Controls.Add(this.dgvPositions);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnChange);
            this.Controls.Add(this.btnCreate);
            this.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "EmployeePositions";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Справочник: Должности";
            this.Load += new System.EventHandler(this.EmployeePositions_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPositions)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvPositions;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnChange;
        private System.Windows.Forms.Button btnCreate;
    }
}