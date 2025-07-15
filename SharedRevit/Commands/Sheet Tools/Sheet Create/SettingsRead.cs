﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using Autodesk.Windows;
using Autodesk.Revit.DB.Structure;
using SharedCore.SaveFile;
using SharedCore;

namespace SharedRevit.Commands
{
    public class SettingsRead
    {
        public static (string tradeAbbreviation, string SheetNumber, string titleBlockFamily, string titleBlockType) BaseControls()
        {

            string tradeAbbreviation = "";
            string SheetNumber = "";
            string titleBlockFamily = "";
            string titleBlockType = "";

            //Get txt Path
            string BasePath = Path.Combine(App.BasePath, "Settings.txt");

            SaveFileManager saveFileManager = new SaveFileManager(BasePath);
            SaveFileSection saveFileSection = saveFileManager.GetSectionsByName("Sheet Settings", "Sheet Creator Base Settings");
            tradeAbbreviation = saveFileSection.Rows[0][0];
            SheetNumber = saveFileSection.Rows[0][1];
            titleBlockFamily = saveFileSection.Rows[0][2];
            titleBlockType = saveFileSection.Rows[0][3];
            return (tradeAbbreviation,SheetNumber, titleBlockFamily, titleBlockType);
        }
        public static Dictionary<string,(string, string)> Scale ()
        {

            Dictionary<string, (string, string)> scale = new Dictionary<string, (string, string)> ();

            //Get txt Path
            string BasePath = Path.Combine(App.BasePath, "Settings.txt");

            SaveFileManager saveFileManager = new SaveFileManager(BasePath);
            SaveFileSection saveFileSection = saveFileManager.GetSectionsByName("Sheet Settings", "Scale");
            foreach (string[] row in saveFileSection.Rows)
            {
                scale.Add(row[0], (row[1], row[2]));
            }
            return scale;
        }

        public static Dictionary<string, (string, string)> NonstandardLevels()
        {

            Dictionary<string, (string, string)> nonstandardLevels = new Dictionary<string, (string, string)>();

            //Get txt Path
            string BasePath = Path.Combine(App.BasePath, "Settings.txt");

            SaveFileManager saveFileManager = new SaveFileManager(BasePath);
            SaveFileSection saveFileSection = saveFileManager.GetSectionsByName("Sheet Settings", "Nonstandard Level Info");
            foreach (string[] row in saveFileSection.Rows)
            {
                nonstandardLevels.Add(row[0], (row[1], row[2]));
            }
            return nonstandardLevels;
        }

        public static Dictionary<string, (string, string)> NonstandardArea()
        {

            Dictionary<string, (string, string)> nonstandardArea = new Dictionary<string, (string, string)>();

            //Get txt Path
            string BasePath = Path.Combine(App.BasePath, "Settings.txt");

            SaveFileManager saveFileManager = new SaveFileManager(BasePath);
            SaveFileSection saveFileSection = saveFileManager.GetSectionsByName("Sheet Settings", "Nonstandard Scopebox Info");
            foreach (string[] row in saveFileSection.Rows)
            {
                nonstandardArea.Add(row[0], (row[1], row[2]));
            }
            return nonstandardArea;
        }

        public static Dictionary<string, (string, string)> Discipline()
        {

            Dictionary<string, (string, string)> discipline = new Dictionary<string, (string, string)>();

            //Get txt Path
            string BasePath = Path.Combine(App.BasePath, "Settings.txt");

            SaveFileManager saveFileManager = new SaveFileManager(BasePath);
            SaveFileSection saveFileSection = saveFileManager.GetSectionsByName("Sheet Settings", "Sheet Discipline");
            foreach (string[] row in saveFileSection.Rows)
            {
                discipline.Add(row[0], (row[1], row[2]));
            }
            return discipline;
        }

        public static (List<string>, bool) SubDiscipline()
        {

            List<string> subDiscipline = new List<string>();
            bool check = false;

            //Get txt Path
            string BasePath = Path.Combine(App.BasePath, "Settings.txt");

            SaveFileManager saveFileManager = new SaveFileManager(BasePath);    
            SaveFileSection DisSection = saveFileManager.GetSectionsByName("Sheet Settings", "Sheet Sub Discipline");
            SaveFileSection ChecSection = saveFileManager.GetSectionsByName("Sheet Settings", "Sub Discipline check");

            check = ChecSection.Rows[0].FirstOrDefault() == "True";

            foreach (string[] row in DisSection.Rows)
            {
                subDiscipline.Add(row[0]);
            }
            return (subDiscipline,check);
        }
    }
}