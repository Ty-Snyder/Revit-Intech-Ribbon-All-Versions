using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SharedRevit.Forms;
using SharedRevit.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace SharedRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FilterTool : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            RevitUtilService.Get().init(doc);
            // Get the selected element
            Element selectedElement = uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .FirstOrDefault();

            if (selectedElement == null)
            {
                try
                {
                    Reference pickedRef = uiDoc.Selection.PickObject(ObjectType.Element, "Please select an element.");
                    selectedElement = doc.GetElement(pickedRef);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    TaskDialog.Show("Selection Cancelled", "No element was selected.");
                    return Result.Cancelled;
                }
            }

            // Open the filter creation form
            Transaction trans = new Transaction(doc, "Make Filter");
            trans.Start();
            FilterCreateForm filterForm = new FilterCreateForm(selectedElement);
            filterForm.ShowDialog();
            trans.Commit();
            return Result.Succeeded;
        }
    }
}
