using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    [Serializable]
    public class Ispitanik
    {
        public string Ime { get; set; }
        public string Prezime { get; set; }
        public string ID { get; set; }
        public int Starost { get; set; }
    }

}
