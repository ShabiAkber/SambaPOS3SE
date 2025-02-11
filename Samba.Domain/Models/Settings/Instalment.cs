using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Samba.Domain.Models.Settings
{
    public static class Instalment
    {
        public static decimal VersActu { get; set; }
        public static decimal VersAnt { get; set; }
        public static decimal Sold { get; set; }

        static Instalment(){}

        public static void Clear()
        {
            VersActu = 0;
            VersAnt = 0;
            Sold = 0;
        }
    }
}
