using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedCore
{
    public class App
    {
        static public string BasePath;
        public void Init(string basePath)
        {
            BasePath = basePath;
        }
    }
}
