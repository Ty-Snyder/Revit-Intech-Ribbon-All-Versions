using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SharedRevit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Commands
{

    public class AddSharedParametersHandler : IExternalEventHandler
    {
        public List<Family> Families { get; set; }
        public List<Definition> Definitions { get; set; }
        public ForgeTypeId Group { get; set; }
        public bool IsInstance { get; set; }

        public AddSharedParametersHandler(List<Family> families, List<Definition> definitions, ForgeTypeId group, bool isInstance)
        {
            Families = families;
            Definitions = definitions;
            Group = group;
            IsInstance = isInstance;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                RevitUtilService.Get().AddSharedParametersToFamiliesNoSave(Families, Definitions, Group, IsInstance);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName() => "Add Shared Parameters to Families";
    }

}
