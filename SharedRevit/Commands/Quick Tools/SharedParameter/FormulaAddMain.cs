using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedRevit.Forms;
using SharedRevit.Utils;

namespace SharedRevit.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class FormulaAddMain : IExternalCommand
    {
        static RevitUtilsDefault RevitUtils = RevitUtilService.Get();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            Document doc = commandData.Application.ActiveUIDocument.Document;
            RevitUtils.init(doc);
            FormulaAdd formulaAdd = new FormulaAdd();
            formulaAdd.ShowDialog();
            return Result.Succeeded;
        }
    }
}
