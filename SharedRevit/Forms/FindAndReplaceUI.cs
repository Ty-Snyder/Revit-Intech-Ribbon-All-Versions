﻿using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedRevit.Commands;

namespace SharedRevit.Forms
{
    public partial class FindAndReplaceUI : System.Windows.Forms.Form
    {
        public FindAndReplaceUI()
        {
            InitializeComponent();
            CenterToParent();
            Cancel.Click += Cancel_Click;
            Confirm.Click += Confirm_Click;

            List<Element> elems  = MainFindandReplace.GetSelectedElements();
            List<string> parameters = MainFindandReplace.GetCommonTextParameters(elems);
            parameterBox.Sorted = true;
            parameterBox.SetItems(parameters);
        }

        private void Confirm_Click(object sender, EventArgs e)
        {
            string selectedParameter = parameterBox.SelectedItem.ToString();
            string replaceText = replace.Text.Trim();
            string findText = find.Text.Trim();
            List<Element> elems = MainFindandReplace.GetSelectedElements();

            if (MainFindandReplace.Replace(elems, selectedParameter, findText, replaceText))
            {
                MessageBox.Show("Replacement successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("Replacement failed. Please check the parameters and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Cancel;
            }
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
