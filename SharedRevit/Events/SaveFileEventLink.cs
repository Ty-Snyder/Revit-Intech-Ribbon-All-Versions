using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using System;
using System.IO;
using SharedRevit.SavedFile;
using SharedCore.SaveFile;

namespace SharedRevit.Events
{
    public static class SaveFileEventLink
    {
        public static void Register(UIControlledApplication app)
        {
            app.ControlledApplication.DocumentOpened += OnDocumentOpened;
            app.ControlledApplication.DocumentSaved += OnDocumentSaved;
            app.ControlledApplication.DocumentSynchronizedWithCentral += OnDocumentSynced;
        }

        public static void Cleanup()
        {
            try
            {
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Intech");
                string tempPath = Path.Combine(baseDir, "temp.txt");

                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Shutdown Cleanup", $"Failed to delete temp file:\n{ex.Message}");
            }
        }

        private static void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            var manager = new RevitFileManager(e.Document, new TxtFormat());
            manager.InitializeTempFromLocal();
            manager.SyncToSharedWithDeletions();
        }

        private static void OnDocumentSaved(object sender, DocumentSavedEventArgs e)
        {
            var manager = new RevitFileManager(e.Document, new TxtFormat());
            manager.SaveToLocal();
        }

        private static void OnDocumentSynced(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            var manager = new RevitFileManager(e.Document, new TxtFormat());
            manager.SyncToSharedWithDeletions();
        }
    }
}
