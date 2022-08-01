using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sovell.ShopAPI.Model
{
    public class policy
    {

        public int id { get; set; }
        public int type { get; set; }
        public string name { get; set; }
        public string trigger { get; set; }
        public string action { get; set; }
        public int state { get; set; }
        public string remark { get; set; }
        public DateTime create_date { get; set; }
        public DateTime last_date { get; set; }


    }
}