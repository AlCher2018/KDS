using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDS.ViewModel
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public CookPhaseEnum OrderStatusId { get; set; }
        public string UID { get; set; }
        public int Number { get; set; }

        public DateTime DateCreate { get; set; }
        public DateTime DateStart { get; set; }
        public int SpentTime { get; set; }

        public string HallName { get; set; }
        public string TableName { get; set; }

        public string Garson { get; set; }
    }

}
