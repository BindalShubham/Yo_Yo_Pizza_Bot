using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Yo_Yo_Bot.Models
{
    public class PizzaOrderState
    {
        public bool? IsSpecialPizza { get; set; }
        public string Size { get; set; }

        public string PizzaName { get; set; }

        public double Price { get; set; }

        public string Base { get; set; }

        public string Cheese { get; set; }

        public string[] VegTops { get; set; }

        public string[] NonVegTops { get; set; }

        public string Sauce { get; set; }

        public string Location { get; set; }

        public string Rating { get; set; }

        public string ImageURL { get; set; }

        public bool Ordered { get; set; }

        public int OrderId { get; set; }

        public DateTime? DeliveryTime { get; set; }
    }
}
