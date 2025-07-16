using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Intech;
using System.Windows;
using System.Windows.Forms;
using SharedRevit.Forms.Settings;
using SharedRevit.Utils;
namespace SharedRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    //Settings
    public class RenumberMain : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            RevitUtilService.Get().init(doc);
            RenumberSettings settings = new RenumberSettings();
            settings.ShowDialog();

            return Result.Succeeded;
        }
    }
}