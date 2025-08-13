using SharedRevit.Forms.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedRevit.Commands.Tagging_Tools.Number
{
    public static class KeyWatcher
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        private static CancellationTokenSource _cts;

        public static void StartWatching()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                return; // Already running

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    if ((GetAsyncKeyState(Keys.S) & 0x8000) != 0)
                    {
                        Application.OpenForms[0]?.BeginInvoke(new Action(() =>
                        {
                            // Only open if not already open
                            if (Application.OpenForms["RenumberSettings"] == null)
                            {
                                RenumberSettings settingsForm = new RenumberSettings();
                                settingsForm.Name = "RenumberSettings";
                                settingsForm.Show();
                            }
                        }));

                        Thread.Sleep(500);
                    }

                    Thread.Sleep(100);
                }
            }, token);
        }


        public static void StopWatching()
        {
            _cts?.Cancel();
            _cts = null;
        }
    }
}
