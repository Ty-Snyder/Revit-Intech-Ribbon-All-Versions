using Autodesk.Revit.UI;
using Revit_2025;
using SharedRevit.Events;
using SharedRevit.Utils;
using SharedCore;

namespace Revit2025
{
    public class App : IExternalApplication
    {
        internal string basePath;
        public Result OnStartup(UIControlledApplication application)
        {
            string dllPath = typeof(App).Assembly.Location;
            basePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(dllPath)));
            SharedCore.App.BasePath = basePath;
            SaveFileEventLink.Register(application);

            new Ribbon(application , basePath);

            SharedRevit.Utils.RevitUtilService.Initialize(new RevitUtils());
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            SaveFileEventLink.Cleanup();
            return Result.Succeeded;
        }
    }
}

