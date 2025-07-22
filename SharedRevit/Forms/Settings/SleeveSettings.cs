using Autodesk.Revit.DB;
using Autodesk.Windows.ToolBars;
using Intech;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedCore.SaveFile;
using SharedCore;
using SharedRevit.Utils;
using System.Runtime.ExceptionServices;

namespace SharedRevit.Forms.Settings 
{
    public partial class SleeveSettings : System.Windows.Forms.Form
    {
        SaveFileManager saveFileManager = null;
        List<Family> fams = null;
        RevitUtilsDefault RevitUtils = RevitUtilService.Get();

        public SleeveSettings()
        {
            InitializeComponent();
            CenterToParent();
            Save.Click += Save_Click;
            Cancel.Click += Cancel_Click;
            this.Text = "Sleeve Settings";
            List<RevitLinkInstance> links = RevitUtils.GetLinkedModels();
            List<string> linkNames = links.Select(x => x.Name).ToList();
            linkNames.Add("Current");
            linkNames.Sort();
            structCombo.DataSource = linkNames;

            fams = RevitUtils.GetFamilies();
            List<string> famName = fams.Select(f => f.Name).ToList();
            famName.Sort();

            string basePath = Path.Combine(App.BasePath, "Settings.txt");
            saveFileManager = new SaveFileManager(basePath);
            SaveFileSection linkedSection = saveFileManager.GetSectionsByName("Sleeve Place", "linked Model") ?? 
                new SaveFileSection("Sleeve Place", "linked Model", "Selected link name");
            if (linkedSection.Rows.Count() > 0 && linkedSection.Rows[0].Count() > 0 && linkNames.Contains(linkedSection.Rows[0][0]))
            {
                structCombo.Text = linkedSection.Rows[0][0];
            }
            else
            {
                structCombo.Text = "Current";
            }
            SaveFileSection rectSection = saveFileManager.GetSectionsByName("Sleeve Place", "Forced Rect") ??
                new SaveFileSection("Sleeve Place", "Forced Rect", "Override Round to rect");
            if (rectSection.Rows.Count() > 0 && rectSection.Rows[0].Count() > 0 && rectSection.Rows[0][0] == "True")
            {
                ForceRectangular.Checked = true;
            }
            {
                SaveFileSection saveFileSection = saveFileManager.GetSectionsByName("Sleeve Place", "Round Sleeve") ??
                new SaveFileSection("Sleeve Place", "Round Sleeve", "Active\tName\tFamily\tFamily Type\tLength Parameter\t" +
                    "Diameter Parameter\tLength Tolerance\tDiameter Tolerance\tLength Round\tDiameter Round");
                RoundPanel.CellEdited += RoundPanel_CellEdited;
                RoundPanel.RowAdded += RoundPanel_RowAdded;

                RoundPanel.ConfigureColumnTypes(new Dictionary<string, ColumnType>
                {
                    { "Active", ColumnType.CheckBox },
                    { "Name", ColumnType.Text },
                    { "Family", ColumnType.ComboBox },
                    { "Family Type", ColumnType.ComboBox },
                    { "Length Parameter", ColumnType.ComboBox },
                    { "Diameter Parameter", ColumnType.ComboBox },
                    { "Length Tolerance", ColumnType.Text },
                    { "Diameter Tolerance", ColumnType.Text },
                    { "Length Round", ColumnType.Text },
                    { "Diameter Round", ColumnType.Text }
                });
                RoundPanel.SetDefaultColumnValue("Active", "False");
                RoundPanel.SetDefaultColumnValue("Length Tolerance", "0");
                RoundPanel.SetDefaultColumnValue("Diameter Tolerance", "0");
                RoundPanel.SetDefaultColumnValue("Length Round", "0.5");
                RoundPanel.SetDefaultColumnValue("Diameter Round", "0.5");

                RoundPanel.Initialize(saveFileManager, saveFileSection);

                RoundPanel.SetComboBoxItems("Family", famName);

            }

            {
                SaveFileSection saveFileSection = saveFileManager.GetSectionsByName("Sleeve Place", "Rect Sleeve") ??
                new SaveFileSection("Sleeve Place", "Rect Sleeve", "Active\tName\tFamily\tFamily Type\tLength Parameter\t" +
                    "Width Parameter\tHeight Parameter\tLength Tolerance\tWidth Tolerance\tHeight Tolerance\tLength Round\tWidth Round\tHeight Round");
                RectPanel.CellEdited += RectPanel_CellEdited;
                RectPanel.RowAdded += RectPanel_RowAdded;

                RectPanel.ConfigureColumnTypes(new Dictionary<string, ColumnType>
                {
                    { "Active", ColumnType.CheckBox },
                    { "Name", ColumnType.Text },
                    { "Family", ColumnType.ComboBox },
                    { "Family Type", ColumnType.ComboBox },
                    { "Length Parameter", ColumnType.ComboBox },
                    { "Width Parameter", ColumnType.ComboBox },
                    { "Height Parameter", ColumnType.ComboBox }
                });
                RectPanel.SetDefaultColumnValue("Active", "False");
                RectPanel.SetDefaultColumnValue("Length Tolerance", "0");
                RectPanel.SetDefaultColumnValue("Width Tolerance", "0");
                RectPanel.SetDefaultColumnValue("Height Tolerance", "0");
                RectPanel.SetDefaultColumnValue("Length Round", "0.5");
                RectPanel.SetDefaultColumnValue("Width Round", "0.5");
                RectPanel.SetDefaultColumnValue("Height Round", "0.5");

                RectPanel.Initialize(saveFileManager, saveFileSection);

                RectPanel.SetComboBoxItems("Family", famName);
            }
        }

        private void RectPanel_RowAdded(object sender, EventArgs e)
        {
            DataGridViewRowsAddedEventArgs rowEvent = e as DataGridViewRowsAddedEventArgs;

            if (RectPanel.GetCellValue(2, rowEvent.RowIndex) is string family && !string.IsNullOrWhiteSpace(family))
            {
                Family fam = fams.FirstOrDefault(f => f.Name == family);
                if (fam != null)
                {
                    List<FamilySymbol> types = RevitUtils.GetFamilySymbols(fam);
                    RectPanel.SetComboBoxItems("Family Type", rowEvent.RowIndex, types.Select(fs => fs.Name).ToList());
                    List<string> parameter = RevitUtils.GetParameters(fam);
                    RectPanel.SetComboBoxItems("Length Parameter", parameter);
                    RectPanel.SetComboBoxItems("Width Parameter", parameter);
                    RectPanel.SetComboBoxItems("Height Parameter", parameter);
                }
            }
        }

        private void RectPanel_CellEdited(object sender, EventArgs e)
        {
            DataGridViewCellEventArgs cellEvent = e as DataGridViewCellEventArgs;
            if (cellEvent.ColumnIndex == 0)
            {
                if (RectPanel.GetCellValue(0, cellEvent.RowIndex) is Boolean boolean && boolean)
                {
                    int rows = RectPanel.GetRowCount();
                    for (int i = 0; i < rows; i++)
                    {
                        if (i != cellEvent.RowIndex)
                            RectPanel.SetCellValue(0, i, false);
                    }
                }
            }
            if (cellEvent.ColumnIndex == 2)
            {
                Family fam = fams.FirstOrDefault(f => f.Name == (string)RectPanel.GetCellValue(2, cellEvent.RowIndex));
                List<FamilySymbol> types = RevitUtils.GetFamilySymbols(fam);
                List<string> typeNames = types.Select(t => t.Name).ToList();
                typeNames.Sort();
                RectPanel.SetComboBoxItems("Family Type", cellEvent.RowIndex, typeNames);

                List<string> names = new List<string>();
                List<string> parameter = RevitUtils.GetParameters(fam);
                RectPanel.SetComboBoxItems("Length Parameter", parameter);
                RectPanel.SetComboBoxItems("Width Parameter", parameter);
                RectPanel.SetComboBoxItems("Height Parameter", parameter);
            }
        }

        private void RoundPanel_RowAdded(object sender, EventArgs e)
        {
            DataGridViewRowsAddedEventArgs rowEvent = e as DataGridViewRowsAddedEventArgs;

            if (RoundPanel.GetCellValue(2, rowEvent.RowIndex) is string family && !string.IsNullOrWhiteSpace(family))
            {
                Family fam = fams.FirstOrDefault(f => f.Name == family);
                if (fam != null)
                {
                    List<FamilySymbol> types = RevitUtils.GetFamilySymbols(fam);
                    RoundPanel.SetComboBoxItems("Family Type", rowEvent.RowIndex, types.Select(fs => fs.Name).ToList());
                    List<string> parameter = RevitUtils.GetParameters(fam);
                    RoundPanel.SetComboBoxItems("Length Parameter", parameter);
                    RoundPanel.SetComboBoxItems("Diameter Parameter", parameter);
                }
            }
        }

        private void RoundPanel_CellEdited(object sender, EventArgs e)
        {
            DataGridViewCellEventArgs cellEvent = e as DataGridViewCellEventArgs;
            if (cellEvent.ColumnIndex == 0)
            {
                if (RoundPanel.GetCellValue(0, cellEvent.RowIndex) is Boolean boolean && boolean)
                {
                    int rows = RoundPanel.GetRowCount();
                    for (int i = 0; i < rows; i++)
                    {
                        if ( i != cellEvent.RowIndex)
                            RoundPanel.SetCellValue(0, i, false);
                    }
                }
            }
            if (cellEvent.ColumnIndex == 2)
            {
                Family fam = fams.FirstOrDefault(f => f.Name == (string)RoundPanel.GetCellValue(2, cellEvent.RowIndex));
                List<FamilySymbol> types = RevitUtils.GetFamilySymbols(fam);
                List<string> typeNames = types.Select(t => t.Name).ToList();
                typeNames.Sort();
                RoundPanel.SetComboBoxItems("Family Type", cellEvent.RowIndex, typeNames);

                List<string> names = new List<string>();
                List<string> parameter = RevitUtils.GetParameters(fam);
                RoundPanel.SetComboBoxItems("Length Parameter", parameter);
                RoundPanel.SetComboBoxItems("Diameter Parameter", parameter);
            }
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            SaveFileSection linkSection = new SaveFileSection("Sleeve Place", "linked Model", "Selected link name");
            linkSection.Rows.Add(new string[] { structCombo.Text });
            saveFileManager.AddOrUpdateSection(linkSection);
            
            SaveFileSection forcedRectSection = new SaveFileSection("Sleeve Place", "Forced Rect", "Override Round to rect");
            forcedRectSection.Rows.Add(new string[] { ForceRectangular.Checked.ToString() });
            saveFileManager.AddOrUpdateSection(forcedRectSection);

            RoundPanel.Confirm();
            RectPanel.Confirm();
            this.Close();
        }
    }
}
