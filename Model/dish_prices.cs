using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sovell.ShopAPI.Model
{
    public class dish_prices
    {
       public int policy{get;set;}
        public int id {get;set;}
        public int times {get;set;}
        public DateTime create_date{get;set;}
        public decimal price { get;set;}
        public int state { get;set;}
        public int windowid { get;set;}
        public int type { get;set;}
        public int rank { get;set;}
        public long autoid { get;set;}
        public string dish { get;set;}
        public int qty_book { get;set;}
        public string name { get;set;}
        public string proname { get; set; }
        public string prodno { get; set; }
        
    }

    public class dish_prices_timer
    {
        public int id { get; set; }
        public string proname { get; set; }
        public string prodno { get; set; }
        public decimal timer1_price { get; set; }
        public decimal timer2_price { get; set; }
        public decimal timer3_price { get; set; }
        public decimal timer4_price { get; set; }

        public decimal timer1 { get; set; }
        public decimal timer2 { get; set; }
        public decimal timer3 { get; set; }
        public decimal timer4 { get; set; }


    }

}