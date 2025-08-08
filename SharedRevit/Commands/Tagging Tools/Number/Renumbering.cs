﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Intech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using SharedCore;
using SharedCore.SaveFile;

namespace SharedRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]

    //Settings
    public class NumberTool : IExternalCommand
    {
        Utils.RevitUtilsDefault RevitUtils = Utils.RevitUtilService.Get();
        Dictionary<string, string> NumberMap = new Dictionary<string, string>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            RevitUtils.init(doc);
            try
            {
                string filePath = Path.Combine(App.BasePath, "Settings.txt");
                SaveFileManager saveFileManager = new SaveFileManager(filePath, new TxtFormat());

                SaveFileSection sec = saveFileManager.GetSectionsByName("Number Settings", "Main");
                if (sec == null)
                {
                    throw new InvalidOperationException("No section found for 'Number Settings::Main' in the save file. Please go to Numbering settings and make sure to add a row and click Confirm.");
                }
                List<Category> categories = new List<Category>();
                CategoryNameMap categoryMap = RevitUtils.GetAllCategories();
                foreach (string catNames in sec.GetColumn(0))
                {
                    categories.Add(categoryMap.get_Item(catNames));
                }
                // Create the selection filter
                ISelectionFilter filter = new CategoryObjectSelectionFilter(categories);

                // Prompt user to select multiple elements
                Reference selectedRef = uidoc.Selection.PickObject(ObjectType.Element, filter, "Select elements to number");
                Element selectedElement = uidoc.Document.GetElement(selectedRef);
                Category category = selectedElement.Category;

                SaveFileSection matchParamsSec = saveFileManager.GetSectionsByName("Number Setting",category.Name);
                string hash = string.Empty;
                if (matchParamsSec != null )
                {
                    List<string> match = matchParamsSec.GetColumn(0);
                    List<string> values = new List<string>();
                    foreach (string param in match)
                    {
                        Parameter p = selectedElement.LookupParameter(param);
                        if (p != null && p.HasValue)
                        {
                            values.Add(p.AsValueString());
                        }
                    }
                    hash = GetParameterIdentity(values);
                }

                string[] row = sec.lookUp(0, category.Name).FirstOrDefault();
                string paramName = row[1];

                String paramVal = string.Empty;
                if (NumberMap.ContainsKey(hash))
                {
                    paramVal = NumberMap[hash];
                }
                else
                {
                    bool tag = string.Equals(row[2], "True");
                    string prefix = row.Length > 3 ? row[3] : string.Empty;
                    string num = row.Length > 4 ? row[4] : "1";
                    string suffix = row.Length > 5 ? row[5] : string.Empty;
                    string sep = row.Length > 6 ? row[6] : string.Empty;
                    paramVal = num;
                    row[4] = (int.Parse(num) + 1).ToString();
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        paramVal = prefix + sep + num;
                    }
                    if (!string.IsNullOrEmpty(suffix))
                    {
                        paramVal += sep + suffix;
                    }
                    if (!string.IsNullOrEmpty(hash))
                        NumberMap.Add(hash, paramVal);
                }
                
                using (Transaction tran = new Transaction(doc))
                {
                    tran.Start("Number Parameter");
                    selectedElement.LookupParameter(paramName).Set(paramVal);
                    tran.Commit();
                }
                saveFileManager.AddOrUpdateSection(sec);
                var TagFam = tagtools.SaveInformation("Number");
                tagtools.tag
                    (
                    commandData,
                    TagFam.Category,
                    TagFam.Path,
                    TagFam.TagFamily,
                    TagFam.Leader,
                    selectedElement.Id
                    );
                Execute(commandData, ref message, elements);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"An error occurred: {ex.Message}");
                return Result.Failed;
            }
            return Result.Succeeded;
        }


        public class CategoryObjectSelectionFilter : ISelectionFilter
        {
            private readonly HashSet<ElementId> _allowedCategoryIds;

            public CategoryObjectSelectionFilter(IEnumerable<Category> categories)
            {
                _allowedCategoryIds = new HashSet<ElementId>(categories.Select(c => c.Id));
            }

            public bool AllowElement(Element elem)
            {
                return elem.Category != null && _allowedCategoryIds.Contains(elem.Category.Id);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }

        public static string GetParameterIdentity(List<string> values)
        {
            var normalized = values
                 .Select(v => v?.Trim().ToLowerInvariant() ?? "")
                 .OrderBy(v => v)
                 .ToList();
            string combined = string.Join("|", normalized);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

    }
}
