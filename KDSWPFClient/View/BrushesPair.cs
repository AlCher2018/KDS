using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace KDSWPFClient.View
{
    public class BrushesPair
    {
        // основная пара
        public Brush Background { get; set; }

        public Brush Foreground { get; set; }

        // словарь неосновных пар
        private Dictionary<string, BrushesPair> _subDict = null;
        public Dictionary<string, BrushesPair> SubDictionary { get { return _subDict; } }

        public void CreateEmptySubDict()
        {
            _subDict = new Dictionary<string, BrushesPair>();
        }

    }  // class
}
