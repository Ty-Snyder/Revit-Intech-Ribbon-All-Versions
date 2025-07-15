using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SharedRevit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedRevit.Forms.Settings;
namespace SharedRevit.Commands
{

    [Transaction(TransactionMode.Manual)]
    public class SleeveSettingsMain : IExternalCommand
    {
        RevitUtilsDefault RevitUtils = RevitUtilService.Get();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Autodesk.Revit.UI.UIApplication app = commandData.Application;
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            RevitUtils.init(doc);
            SleeveSettings settingsForm = new SleeveSettings();
            settingsForm.ShowDialog();

            return Result.Succeeded;
        }
    }
}

