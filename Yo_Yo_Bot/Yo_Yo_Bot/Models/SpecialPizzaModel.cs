using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Yo_Yo_Bot.Models
{
    public class SpecialPizzaModel
    {
        public string PizzaName { get; set; }

        public string PizzaBase { get; set; }

        public string Rating { get; set; }

        public string[] VegTops { get; set; }

        public string[] NonVegTops { get; set; }

        public string ImageURL { get; set; }

        public float Price { get; set; }
    }
}
