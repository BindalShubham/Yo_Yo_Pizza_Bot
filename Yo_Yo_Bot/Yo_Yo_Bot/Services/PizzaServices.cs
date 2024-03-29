// <auto-generated>
// Code generated by LUISGen C:\Users\ShubhamBindal\source\repos\Yo_Yo_Bot\Yo_Yo_Bot\Yo_Yo_Bot\Deployment\Resources\Pizza\Yo_Yo_pizzaboten_order.json -cs Luis.PizzaServices -o C:\Users\ShubhamBindal\source\repos\Yo_Yo_Bot\Yo_Yo_Bot\Yo_Yo_Bot\Services
// Tool github: https://github.com/microsoft/botbuilder-tools
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
namespace Luis
{
    public partial class PizzaServices: IRecognizerConvert
    {
        [JsonProperty("text")]
        public string Text;

        [JsonProperty("alteredText")]
        public string AlteredText;

        public enum Intent {
            None, 
            pizza_order, 
            pizza_status
        };
        [JsonProperty("intents")]
        public Dictionary<Intent, IntentScore> Intents;

        public class _Entities
        {
            // Simple entities
            public string[] orderId;

            // Built-in entities
            public DateTimeSpec[] datetime;

            public string[] personName;

            // Lists
            public string[][] pizza_cheese;

            public string[][] pizza_sauce;

            public string[][] pizza_size;

            // Instance
            public class _Instance
            {
                public InstanceData[] datetime;
                public InstanceData[] orderId;
                public InstanceData[] personName;
                public InstanceData[] pizza_cheese;
                public InstanceData[] pizza_sauce;
                public InstanceData[] pizza_size;
            }
            [JsonProperty("$instance")]
            public _Instance _instance;
        }
        [JsonProperty("entities")]
        public _Entities Entities;

        [JsonExtensionData(ReadData = true, WriteData = true)]
        public IDictionary<string, object> Properties {get; set; }

        public void Convert(dynamic result)
        {
            var app = JsonConvert.DeserializeObject<PizzaServices>(JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            Text = app.Text;
            AlteredText = app.AlteredText;
            Intents = app.Intents;
            Entities = app.Entities;
            Properties = app.Properties;
        }

        public (Intent intent, double score) TopIntent()
        {
            Intent maxIntent = Intent.None;
            var max = 0.0;
            foreach (var entry in Intents)
            {
                if (entry.Value.Score > max)
                {
                    maxIntent = entry.Key;
                    max = entry.Value.Score.Value;
                }
            }
            return (maxIntent, max);
        }
    }
}
