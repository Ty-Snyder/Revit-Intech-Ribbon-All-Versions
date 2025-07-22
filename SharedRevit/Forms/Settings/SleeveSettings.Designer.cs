﻿namespace SharedRevit.Forms.Settings
{
    partial class SleeveSettings
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
            this.structCombo = new System.Windows.Forms.ComboBox();
            this.Save = new System.Windows.Forms.Button();
            this.Cancel = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.roundRectTabControl = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.RoundPanel = new SharedRevit.Forms.SectionEditorControl();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.RectPanel = new SharedRevit.Forms.SectionEditorControl();
            this.ForceRectangular = new System.Windows.Forms.CheckBox();
            this.roundRectTabControl.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // structCombo
            // 
            this.structCombo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.structCombo.FormattingEnabled = true;
            this.structCombo.Location = new System.Drawing.Point(100, 9);
            this.structCombo.Name = "structCombo";
            this.structCombo.Size = new System.Drawing.Size(845, 21);
            this.structCombo.TabIndex = 0;
            // 
            // Save
            // 
            this.Save.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Save.Location = new System.Drawing.Point(776, 423);
            this.Save.Name = "Save";
            this.Save.Size = new System.Drawing.Size(75, 23);
            this.Save.TabIndex = 1;
            this.Save.Text = "Save...";
            this.Save.UseVisualStyleBackColor = true;
            // 
            // Cancel
            // 
            this.Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Cancel.Location = new System.Drawing.Point(866, 423);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(75, 23);
            this.Cancel.TabIndex = 2;
            this.Cancel.Text = "Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            this.textBox1.BackColor = System.Drawing.SystemColors.Menu;
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox1.Location = new System.Drawing.Point(12, 12);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(82, 13);
            this.textBox1.TabIndex = 3;
            this.textBox1.Text = "Architectural Model";
            this.textBox1.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // roundRectTabControl
            // 
            this.roundRectTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.roundRectTabControl.Controls.Add(this.tabPage1);
            this.roundRectTabControl.Controls.Add(this.tabPage2);
            this.roundRectTabControl.Location = new System.Drawing.Point(12, 36);
            this.roundRectTabControl.Name = "roundRectTabControl";
            this.roundRectTabControl.SelectedIndex = 0;
            this.roundRectTabControl.Size = new System.Drawing.Size(929, 381);
            this.roundRectTabControl.TabIndex = 4;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.RoundPanel);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(921, 355);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Round Sleeve";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // RoundPanel
            // 
            this.RoundPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.RoundPanel.Location = new System.Drawing.Point(0, 0);
            this.RoundPanel.Name = "RoundPanel";
            this.RoundPanel.Size = new System.Drawing.Size(921, 355);
            this.RoundPanel.TabIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.RectPanel);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(921, 355);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Rectangular Sleeve";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // RectPanel
            // 
            this.RectPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.RectPanel.Location = new System.Drawing.Point(0, 0);
            this.RectPanel.Name = "RectPanel";
            this.RectPanel.Size = new System.Drawing.Size(925, 361);
            this.RectPanel.TabIndex = 0;
            // 
            // ForceRectangular
            // 
            this.ForceRectangular.AutoSize = true;
            this.ForceRectangular.Anchor = (System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left);
            this.ForceRectangular.Location = new System.Drawing.Point(16, 425);
            this.ForceRectangular.Name = "ForceRectangular";
            this.ForceRectangular.Size = new System.Drawing.Size(114, 17);
            this.ForceRectangular.TabIndex = 5;
            this.ForceRectangular.Text = "Force Rectangular";
            this.ForceRectangular.UseVisualStyleBackColor = true;
            // 
            // SleeveSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(953, 458);
            this.Controls.Add(this.ForceRectangular);
            this.Controls.Add(this.roundRectTabControl);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.Cancel);
            this.Controls.Add(this.Save);
            this.Controls.Add(this.structCombo);
            this.Name = "SleeveSettings";
            this.Text = "Sleeve Place Settings";
            this.roundRectTabControl.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox structCombo;
        private System.Windows.Forms.Button Save;
        private System.Windows.Forms.Button Cancel;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TabControl roundRectTabControl;
        private System.Windows.Forms.TabPage tabPage1;
        private SectionEditorControl RoundPanel;
        private System.Windows.Forms.TabPage tabPage2;
        private SectionEditorControl RectPanel;
        private System.Windows.Forms.CheckBox ForceRectangular;
    }
}