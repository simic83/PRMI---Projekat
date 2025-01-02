using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    [Serializable]
    public class Rezultat
    {
        public double ProsecnoReakcionoVreme { get; set; }
        public double MinimalnoReakcionoVreme { get; set; }
        public double Tacnost { get; set; }
        public double StopaLaznihPozitiva { get; set; }
        public double StopaLaznihNegativa { get; set; }
    }

}
