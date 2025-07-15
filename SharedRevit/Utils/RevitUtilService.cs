using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Utils
{
    public static class RevitUtilService
    {
        private static RevitUtilsDefault _instance;

        public static void Initialize(RevitUtilsDefault utils)
        {
            _instance = utils;
        }

        public static RevitUtilsDefault Get() => _instance;
    }
}
