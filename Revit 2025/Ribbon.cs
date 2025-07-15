using Autodesk.Revit.UI;
using SharedRevit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Revit_2025
{
    internal class Ribbon : SharedRevit.Ribbon.DefaultRibbonBuild
    {
        public Ribbon(UIControlledApplication app, string basePath) : 
            base(app, basePath, "SharedRevit",Assembly.GetExecutingAssembly().Location)
        {
            // Initialize the ribbon with the application and base path
        }
    }
}
