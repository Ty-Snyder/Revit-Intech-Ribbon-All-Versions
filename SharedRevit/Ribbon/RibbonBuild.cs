using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Ribbon;
using System.Windows.Media.Imaging;

namespace SharedRevit.Ribbon
{
    public class RibbonBuild
    {
        protected string AddInPath = string.Empty;
        protected string defaultImagePath = string.Empty;
        protected string BaseImagePath = string.Empty;
        protected UIControlledApplication uiApp = null;
        protected List<string> tabs = new ();
        protected Dictionary<string, RibbonPanel> panels = new ();

        protected void AddTab(string name)
        {
            uiApp.CreateRibbonTab(name);
            tabs.Add(name);
        }

        protected void AddPanel(string tabName, string panelName)
        {
            if (!tabs.Contains(tabName))
            {
                throw new ArgumentException("tabName does not exist.");
            }
            RibbonPanel panel = uiApp.CreateRibbonPanel(tabName, panelName);
            panels.Add(panelName, panel);
        }

        protected PushButtonData CreateButton(string name, string text, string className, string tooltip)
        {
            PushButtonData Data = new PushButtonData(name, text, AddInPath, className);
            Data.ToolTip = tooltip;
            Data.LargeImage = new BitmapImage(new Uri(defaultImagePath));
            return Data;
        }

        protected PushButtonData CreateButton(string name, string text, string className, string tooltip, string image)
        {
            PushButtonData Data = new PushButtonData(name, text, AddInPath, className);
            Data.ToolTip = tooltip;
            Data.LargeImage = new BitmapImage(new Uri(image));
            return Data;
        }

        protected void AddButton(string panel, string name, string text, string className, string tooltip)
        {
            if (panels.Keys.Contains(panel) == false)
            {
                throw new ArgumentException("Panel does not exist in the current ribbon.");
            }
            panels[panel].AddItem(CreateButton(name, text, className, tooltip));
        }

        protected void AddButton(string panel, string name, string text, string className, string tooltip, string image)
        {
            if (panels.Keys.Contains(panel) == false)
            {
                throw new ArgumentException("Panel does not exist in the current ribbon.");
            }
            panels[panel].AddItem(CreateButton(name, text, className, tooltip, image));
        }

        protected void AddStackButton (string panel, PushButtonData b1, PushButtonData b2)
        {
            if (panels.Keys.Contains(panel) == false)
            {
                throw new ArgumentException("Panel does not exist in the current ribbon.");
            }
            panels[panel].AddStackedItems( b1, b2 );
        }

        protected void AddStackButton(string panel, PushButtonData b1, PushButtonData b2, PushButtonData b3)
        {
            if (panels.Keys.Contains(panel) == false)
            {
                throw new ArgumentException("Panel does not exist in the current ribbon.");
            }
            panels[panel].AddStackedItems(b1, b2, b3);
        }

        protected void AddPulldown(string panel, string pulldownText, string pulldownName, List<PushButtonData> buttons)
        {
            if (panels.Keys.Contains(panel) == false)
            {
                throw new ArgumentException("Panel does not exist in the current ribbon.");
            }

            var pulldownData = new PulldownButtonData(pulldownName, pulldownText);
            pulldownData.LargeImage = new BitmapImage(new Uri(defaultImagePath));
            PulldownButton pulldown = panels[panel].AddItem(pulldownData) as PulldownButton;

            foreach (var buttonData in buttons)
            {
                if (buttonData != null)
                {
                    pulldown.AddPushButton(buttonData);
                }
            }
        }

        protected void AddPulldown(string panel, string pulldownText, string pulldownName, List<PushButtonData> buttons, string image)
        {
            if (panels.Keys.Contains(panel) == false)
            {
                throw new ArgumentException("Panel does not exist in the current ribbon.");
            }

            var pulldownData = new PulldownButtonData(pulldownName, pulldownText);
            pulldownData.LargeImage = new BitmapImage(new Uri(image));
            PulldownButton pulldown = panels[panel].AddItem(pulldownData) as PulldownButton;

            foreach (var buttonData in buttons)
            {
                if (buttonData != null)
                {
                    pulldown.AddPushButton(buttonData);
                }
            }
        }

        protected void AddSplitButton(string panel, string splitButtonText, string splitButtonName, List<PushButtonData> buttons)
        {
            if (!panels.ContainsKey(panel))
            {
                throw new ArgumentException("Panel does not exist in the current ribbon.");
            }

            var splitButtonData = new SplitButtonData(splitButtonName, splitButtonText);
            SplitButton splitButton = panels[panel].AddItem(splitButtonData) as SplitButton;

            foreach (var buttonData in buttons)
            {
                if (buttonData != null)
                {
                    splitButton.AddPushButton(buttonData);
                }
            }
        }

    }
}
