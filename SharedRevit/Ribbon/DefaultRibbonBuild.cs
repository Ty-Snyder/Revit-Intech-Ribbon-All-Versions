using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Ribbon;
using System.Windows.Media.Imaging;
using AW = Autodesk.Windows;

namespace SharedRevit.Ribbon
{
    public class DefaultRibbonBuild : RibbonBuild
    {
        public string BaseCodePath { get; set; } = "SharedRevit";

        public DefaultRibbonBuild(UIControlledApplication app, string basePath, string yearNameSpace, string addinPath)
        {
            uiApp = app;
            BaseImagePath = Path.Combine(basePath, "Images");
            AddInPath = Assembly.GetExecutingAssembly().Location;
            defaultImagePath = Path.Combine(BaseImagePath, "Icon.png");
            BaseCodePath = yearNameSpace;
            BuildRibbon();
        }

        protected virtual void BuildRibbon()
        {
            BuildTab();

            BuildQuickToolsPanel();
            BuildTaggingToolsPanel();
            BuildSheetToolsPanel();
            BuildExportPanel();
            BuildImportPanel();
        }

        protected virtual void BuildQuickToolsPanel()
        {
            QuickToolsStrip();

            ConnectButton();
            DisconnectButton();
            RotateButton();
            RotateAroundButton();
            FindAndReplaceButton();
            FilterButton();
            ParameterToolsPulldown();
            SleevePlaceButton();
            SleeveAtConnectButton();
            QuickToolsSettingsDropDown();
        }

        protected virtual void BuildTaggingToolsPanel()
        {
            TaggingStrip();

            AddTaggingStackedButtons();
            TaggingSplitButton();
            NumberToolButton();
            TaggingSettingsPulldown();
        }

        protected virtual void BuildSheetToolsPanel()
        {
            SheetStrip();

            TitleBlockSelectButton();
            DependentViewButton();
            SheetCreateButton();
            SheetSettingsButton();
        }

        protected virtual void BuildExportPanel()
        {
            ExportStrip();

            bomExportButton();
            tigerStopButton();
        }
        protected virtual void BuildImportPanel()
        {
            ImportStrip();
            ExcelImportButton();
        }
        protected virtual void BuildTab()
        {
            AddTab("Intech Ribbon");
        }

        protected virtual void QuickToolsStrip()
        {
            AddPanel("Intech Ribbon", "Quick Tools");
        }

        protected virtual void TaggingStrip()
        {
            AddPanel("Intech Ribbon", "Tagging Tools");
        }

        protected virtual void SheetStrip()
        {
            AddPanel("Intech Ribbon", "Sheet Tools");
        }

        protected virtual void ExportStrip()
        {
            AddPanel("Intech Ribbon", "Exports");
        }

        protected virtual void ImportStrip()
        {
            AddPanel("Intech Ribbon", "Import");
        }

        protected virtual void ConnectButton()
        {
            AddButton("Quick Tools", "Connect", "Connect", $"{BaseCodePath}.Commands.ConnectElementsCommand", "Connects fitting to pipe or duct to other duct element.");
        }

        protected virtual void DisconnectButton()
        {
            AddButton("Quick Tools", "Disconnect", "Disconnect", $"{BaseCodePath}.Commands.Disconnect", "Closest connector to point clicked on part will disconnect.");
        }

        protected virtual void RotateButton()
        {
            AddButton("Quick Tools", "Rotate", "Rotate", $"{BaseCodePath}.Commands.RotateConnector", "Selects a fitting then prompts angle.");
        }

        protected virtual void RotateAroundButton()
        {
            AddButton("Quick Tools", "Rotate Around", "Rotate Around", $"{BaseCodePath}.Commands.RotateConnectedElements", "Selects a fitting and duct/pipe then prompts angle.");
        }

        protected virtual void FindAndReplaceButton()
        {
            AddButton("Quick Tools", "F&R", "Find and Replace", $"{BaseCodePath}.Commands.MainFindandReplace", "Collects selected items and lets you find and replace values in the parameters.");
        }

        protected virtual void FilterButton()
        {
            AddButton("Quick Tools", "Filter", "Create Filter", $"{BaseCodePath}.Commands.FilterTool", "Lets you select an element then make a filter based on it.");
        }

        protected virtual void SleevePlaceButton()
        {
            AddButton("Quick Tools", "Sleeve Place", "PlaceSleeve", $"{BaseCodePath}.Commands.SleevePlace", "Select elements then run to detect wall collision to put sleeves on collision");
        }

        protected virtual void SleeveAtConnectButton()
        {
            AddButton("Quick Tools", "Sleeve at Connect", "Sleeve at Connect", $"{BaseCodePath}.Commands.PlaceSleeveAtConnectorCommand", "Finds closest connector to selection and places a sleeve");
        }
        protected virtual void TitleBlockSelectButton()
        {
            AddButton("Sheet Tools", "Title Block Selector", "TitleBlock Select", $"{BaseCodePath}.Commands.TitleBlockSelector", "Selects Title Blocks Inside Selected Sheets.");
        }

        protected virtual void DependentViewButton()
        {
            AddButton("Sheet Tools", "Dependent View", "Dependent View Creator", $"{BaseCodePath}.Commands.DependentView", "Create dependent views for a plan view.");
        }

        protected virtual void SheetCreateButton()
        {
            AddButton("Sheet Tools", "Sheet Create", "Sheet Create", $"{BaseCodePath}.Commands.SheetCreateInit", "Create dependent views for a plan view.");
        }

        protected virtual void SheetSettingsButton()
        {
            AddButton("Sheet Tools", "Sheet Settings", "Sheet Settings", $"{BaseCodePath}.Commands.SheetSettingsMenu", "Create dependent views for a plan view.", Path.Combine(BaseImagePath, "Tag Images\\settings.png"));
        }

        protected virtual void ExcelImportButton()
        {
            AddButton("Import", "Excel Import", "Import", $"{BaseCodePath}.Commands.linkUI", "Export schedules into Excel in a BOM format.", Path.Combine(BaseImagePath, "SmallExcelLogo.png"));
        }

        protected virtual void  bomExportButton()
        {
            AddButton("Exports", "BOM Export", "BOM Export", $"{BaseCodePath}.Commands.ExportBOM", "Export schedules into Excel in a BOM format.");
        }

        protected virtual void tigerStopButton()
        {
            AddButton("Exports", "TigerStop", "TigerStop", $"{BaseCodePath}.Commands.TigerStopExport", "Export schedules in tigerstop format");
        }

        protected virtual PushButtonData SleeveSettingsButtonData()
        {
            return CreateButton("Sleeve Settings Menu", "Sleeve Settings", $"{BaseCodePath}.Commands.SleeveSettingsMain", "Menu to change settings for sleeve auto place.");
        }

        protected virtual void QuickToolsSettingsDropDown()
        {
            List<PushButtonData> buttons = new List<PushButtonData>
            {
                SleeveSettingsButtonData()
            };
            AddPulldown("Quick Tools", "Settings", "Quick Tools Settings", buttons, Path.Combine(BaseImagePath, "Tag Images\\settings.png"));
        }

        protected virtual void NumberToolButton()
        {
            AddButton("Tagging Tools", "NumTool", "Number Tool", $"{BaseCodePath}.Commands.NumberTool", "Select a item (Filters based of categories in settings) and it will push an incrimented number into a category");
        }

        protected virtual PushButtonData NumberSettingsButtonData()
        {
            return CreateButton("NumSet", "Number Settings", $"{BaseCodePath}.Commands.RenumberMain", "Configure numbering behavior for tagging.");
        }

        protected virtual PushButtonData TagSettingsButtonData()
        {
            return CreateButton("Tag Settings", "Tag Settings", $"{BaseCodePath}.Commands.TagSettings", "Open tag configuration menu.");
        }

       
        protected void TaggingSettingsPulldown()
        {
            var buttons = new List<PushButtonData>
            {
            NumberSettingsButtonData(),
            TagSettingsButtonData()
            };

            AddPulldown("Tagging Tools", "Settings", "TaggingSettingsPulldown", buttons, Path.Combine(BaseImagePath, "Tag Images\\settings.png"));
        }

        protected virtual PushButtonData SizeButtonData()
        {
            return CreateButton("Size", "Size", $"{BaseCodePath}.Commands.Size", "Tag size", $"{BaseImagePath}\\Tag Images\\Size.png");
        }

        protected virtual PushButtonData ElevationButtonData()
        {
            return CreateButton("Elevation", "Elevation", $"{BaseCodePath}.Commands.Elevation", "Tag elevation", $"{BaseImagePath}\\Tag Images\\Elevation.png");
        }

        protected virtual PushButtonData OffsetButtonData()
        {
            return CreateButton("OffSet", "OffSet", $"{BaseCodePath}.Commands.Offset", "Tag offset", $"{BaseImagePath}\\Tag Images\\OffSet.png");
        }

        protected virtual PushButtonData RenumberButtonData()
        {
            return CreateButton("Renumber", "Number", $"{BaseCodePath}.Commands.Number", "Tag number", $"{BaseImagePath}\\Tag Images\\Number.png");
        }

        protected virtual PushButtonData LengthButtonData()
        {
            return CreateButton("Length", "Length", $"{BaseCodePath}.Commands.Length", "Tag length", $"{BaseImagePath}\\Tag Images\\Length.png");
        }

        protected virtual PushButtonData HangerButtonData()
        {
            return CreateButton("Hanger", "Hanger", $"{BaseCodePath}.Commands.Hanger", "Tag hanger", $"{BaseImagePath}\\Tag Images\\Hanger.png");
        }

        protected virtual void AddTaggingStackedButtons()
        {
            var panel = panels["Tagging Tools"];

            var buttons = new List<Autodesk.Windows.RibbonItem>();

            // Stack 1
            panel.AddStackedItems(SizeButtonData(), ElevationButtonData());
            buttons.Add(GetButton("Intech Ribbon", panel.Name, "Size"));
            buttons.Add(GetButton("Intech Ribbon", panel.Name, "Elevation"));

            // Stack 2
            panel.AddStackedItems(OffsetButtonData(), RenumberButtonData());
            buttons.Add(GetButton("Intech Ribbon", panel.Name, "OffSet"));
            buttons.Add(GetButton("Intech Ribbon", panel.Name, "Renumber"));

            // Stack 3
            panel.AddStackedItems(LengthButtonData(), HangerButtonData());
            buttons.Add(GetButton("Intech Ribbon", panel.Name, "Length"));
            buttons.Add(GetButton("Intech Ribbon", panel.Name, "Hanger"));

            // Apply appearance settings
            foreach (var item in buttons)
            {
                item.Size = Autodesk.Windows.RibbonItemSize.Large;
                item.ShowText = false;
            }
        }
        public AW.RibbonItem GetButton(string tabName, string panelName, string itemName)
        {
            AW.RibbonControl ribbon = AW.ComponentManager.Ribbon;
            foreach (AW.RibbonTab tab in ribbon.Tabs)
            {
                if (tab.Name == tabName)
                {
                    foreach (AW.RibbonPanel panel in tab.Panels)
                    {
                        if (panel.Source.Title == panelName)
                        {
                            return panel.FindItem("CustomCtrl_%CustomCtrl_%" + tabName + "%" + panelName + "%" + itemName, true);
                        }
                    }
                }
            }
            return null;
        }
        protected virtual List<PushButtonData> ExtraTaggingSplitButtons()
        {
            var buttons = new List<PushButtonData>();

            for (int i = 1; i <= 10; i++)
            {
                string name = $"Tag{i}";
                string text = $"Tag {i}";
                string className = $"{BaseCodePath}.Commands.Tag{i}";
                string tooltip = $"Activate Tag {i}";
                string imagePath = $"{BaseImagePath}\\Tag Images\\Tag{i}.png";

                var button = CreateButton(name, text, className, tooltip, imagePath);
                buttons.Add(button);
            }

            return buttons;
        }

        protected void TaggingSplitButton()
        {
            var buttons = ExtraTaggingSplitButtons();
            AddSplitButton("Tagging Tools", "Extra", "TaggingSplitButton", buttons);
        }


        protected virtual PushButtonData ParameterSyncButtonData()
        {
            return CreateButton(
            "ParameterSync",
            "Parameter Sync",
            $"{BaseCodePath}.Commands.ParameterSyncMenu",
            "Opens menu to do fancy parameter stuff."
            );
        }

        protected virtual PushButtonData SharedParamButtonData()
        {
            return CreateButton(
            "SharedParam",
            "Add Shared Parameter",
            $"{BaseCodePath}.Commands.FamilyTypeParameterAdd",
            "Opens menu to do fancy parameter stuff."
            );
        }

        protected virtual PushButtonData FormulaPushButtonData()
        {
            return CreateButton(
            "FormulaPush",
            "Formula Push",
            $"{BaseCodePath}.Commands.FormulaAddMain",
            "Opens menu to do fancy parameter stuff."
            );
        }

        protected void ParameterToolsPulldown()
        {
            var buttons = new List<PushButtonData>
            {
            ParameterSyncButtonData(),
            SharedParamButtonData(),
            FormulaPushButtonData()
            };

            AddPulldown("Quick Tools", "Parameter Tools", "ParameterTools", buttons);
        }

    }
}
