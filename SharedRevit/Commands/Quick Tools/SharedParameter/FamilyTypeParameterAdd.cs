using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SharedRevit.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedRevit.Forms;

namespace SharedRevit.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class FamilyTypeParameterAdd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            Document doc = commandData.Application.ActiveUIDocument.Document;
            RevitUtilService.Get().init(doc);
            SharedParameterAdd sharedParameterForm = new SharedParameterAdd(app);
            sharedParameterForm.ShowDialog();
            return Result.Succeeded;
        }
    }
}
