using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yo_Yo_Bot.Models;
using Yo_Yo_Bot.Services;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Microsoft.Bot.Builder.Location;
using Yo_Yo_Bot.Data;
using Microsoft.Extensions.Configuration;

namespace Yo_Yo_Bot.Dialogs
{
    public class PizzaOrderDialog : ComponentDialog
    {
        private string[] cheeseList = { "Cheddar", "Mozzarella", "Mozzarella" , "Provolone" };
        private string[] sizeList = {"Small 6\"","Medium 8\"", "Large 10\"" };
        private string[] sauceList = { "Special BBQ", "Pepper Jelly", "Béchamel", "Tapenade", "Mariana" };

        private List<SpecialPizzaModel> SpPizzas = new List<SpecialPizzaModel>();

        private BotServices _services;
        private IConfiguration configuration;

        // Prompts names
        private const string SpecialMenuPrompt = "specialMenuPrompt";
        private const string SizeOptionsPrompt = "sizeOptionsPrompt";
        private const string BaseOptionsPrompt = "baseOptionsPrompt";
        private const string CheeseOptionsPrompt = "cheeseOptionsPrompt";
        private const string SauceOptionsPrompt = "sauceOptionsPrompt";
        private const string DeliveryDatePrompt = "deliveryDatePrompt";
        private const string PreviewOrderPrompt = "previewOrderPrompt";
        private const string LocationPrompt = "locationPrompt";


        private const string PizzaDialog = "pizzaDialog";
        private const string TopsDialog = "topsDialog";
        private const string DeliveryDetailDialog = "deliveryDetailDialog";

        private IStatePropertyAccessor<PizzaOrderState> _pizzaOrderingState;
        private IStatePropertyAccessor<PizzaStatusModel> _pizzaStatusModel;


        public PizzaOrderDialog(BotServices services,
            UserState userState,
            IBotTelemetryClient telemetryClient, IConfiguration iConfig) : base(nameof(PizzaOrderDialog))
        {
            _services = services;
            configuration = iConfig;
            _pizzaOrderingState = userState.CreateProperty<PizzaOrderState>(nameof(PizzaOrderState));
            _pizzaStatusModel = userState.CreateProperty<PizzaStatusModel>(nameof(PizzaStatusModel));

            var pizzaOrderSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                SizeOptions,
                SpecialMenu,
                BaseTopsOptions,
                CheeseOptions,
                SauceOptions,
                DeliveryLocation,
                DeliveryTime,
                PreviewOrder,
                CompleteOrder
            };

            AddDialog(new WaterfallDialog(PizzaDialog, pizzaOrderSteps));
            //AddDialog(new WaterfallDialog(TopsDialog, topsSteps));
            //AddDialog(new WaterfallDialog(DeliveryDetailDialog, deliveryDetailSteps));
            AddDialog(new TextPrompt(SizeOptionsPrompt, ValidateSize));
            AddDialog(new TextPrompt(BaseOptionsPrompt, ValidateBase));
            AddDialog(new TextPrompt(CheeseOptionsPrompt, ValidateCheese));
            AddDialog(new TextPrompt(SauceOptionsPrompt,ValidateSauce));
            AddDialog(new DateTimePrompt(DeliveryDatePrompt, ValidateDate));
            AddDialog(new TextPrompt(PreviewOrderPrompt,ValidatePreview));
            AddDialog(new TextPrompt(SpecialMenuPrompt,ValidateSpecialPizza));
            AddDialog(new TextPrompt(LocationPrompt));
            InitialDialogId = PizzaDialog;
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaStatusModel = await _pizzaStatusModel.GetAsync(stepContext.Context, () => null);
            //if (pizzaStatusModel == null)
            //{
            //    await stepContext.(nameof(OnboardingDialog));
            //}
            //else
            //{
                var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);
                if (pizzaOrderState == null)
                {
                    var pizzaOrderStateOpt = stepContext.Options as PizzaOrderState;
                    if (pizzaOrderStateOpt != null)
                    {
                        await _pizzaOrderingState.SetAsync(stepContext.Context, pizzaOrderStateOpt);
                    }
                    else
                    {
                        await _pizzaOrderingState.SetAsync(stepContext.Context, new PizzaOrderState());
                    }
                //}
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> SpecialMenu(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);

            var connect = new DataBaseOperations(configuration.GetValue<string>("sqlDb:connectionString"));
            var specialPizzas = connect.GetSpecialPizzas();

            if (pizzaOrderState.IsSpecialPizza == null)
            {
                var menuCarousal = CreateMenuCarousal(specialPizzas, pizzaOrderState.Size);
                var customizeCard = CreateAdaptiveCardCustomizePizza();
                var customActivity = new Activity
                {
                    Type = ActivityTypes.Message,
                    Text = $"{configuration.GetValue<string>("PizzaOrderDialog:SpecialMenu")}",
                    AttachmentLayout = AttachmentLayoutTypes.Carousel,
                    Attachments = menuCarousal,
                };
                var opts = new PromptOptions
                {

                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"",
                        AttachmentLayout = AttachmentLayoutTypes.Carousel,
                        Attachments = new List<Attachment>() { customizeCard },
                    },
                };
                await stepContext.Context.SendActivityAsync(customActivity);
                return await stepContext.PromptAsync(SpecialMenuPrompt, opts);
            }
            else
                return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> SizeOptions(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);

            if (string.IsNullOrWhiteSpace(pizzaOrderState.Size))
            {
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"{configuration.GetValue<string>("PizzaOrderDialog:SizeOptions")}",
                        SuggestedActions = new SuggestedActions()
                        {
                            Actions = CreateAction(sizeList),
                        },
                    },

                };
                return await stepContext.PromptAsync(SizeOptionsPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> BaseTopsOptions(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);
            if (pizzaOrderState.IsSpecialPizza == false && string.IsNullOrWhiteSpace(pizzaOrderState.Base))
            {
                var pizzaCard = CreateAdaptiveCardforPizzaBase();
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = "",
                        Attachments = new List<Attachment>() { pizzaCard }
                    },
                };
                return await stepContext.PromptAsync(BaseOptionsPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> CheeseOptions(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);
           
            if (pizzaOrderState.IsSpecialPizza == false && string.IsNullOrWhiteSpace(pizzaOrderState.Cheese))
            {
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"{configuration.GetValue<string>("PizzaOrderDialog:CheeseOptions")}",
                        SuggestedActions = new SuggestedActions()
                        {
                            Actions = CreateAction(cheeseList),
                        },
                    },
                };
                return await stepContext.PromptAsync(CheeseOptionsPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> SauceOptions(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);

            if (pizzaOrderState.IsSpecialPizza == false && string.IsNullOrWhiteSpace(pizzaOrderState.Sauce))
            {
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"{configuration.GetValue<string>("PizzaOrderDialog:SauceOptions")}",
                        SuggestedActions = new SuggestedActions()
                        {
                            Actions = CreateAction(sauceList),
                        },
                    },

                };
                return await stepContext.PromptAsync(SauceOptionsPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }
        private async Task<DialogTurnResult> DeliveryLocation(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);
            if (string.IsNullOrWhiteSpace(pizzaOrderState.Location))
            {
                //var apiKey = "ArYP3wwcJ6Lw5f8S7d7zb4ewXAiQmHp40VLneot7dh-wUwaEUvYv-bxlu5mNHsQP";
                //var prompt = "Where should I ship your order? Type or say an address.";
                //var locationDialog = new LocationDialog(apiKey, stepContext.Context.Activity.ChannelId, prompt);
                //locationDialog.StartAsync(
                //return await stepContext.BeginDialogAsync(nameof(locationDialog));
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"{configuration.GetValue<string>("PizzaOrderDialog:DeliveryLocation")}"
                    }
                };
                //return await stepContext.PromptAsync(DeliveryDatePrompt,opts);

                return await stepContext.PromptAsync(LocationPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> DeliveryTime(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);
            if (stepContext.Result != null)
                pizzaOrderState.Location = (string)stepContext.Result;
            if (pizzaOrderState.DeliveryTime == null)
            {
                    // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"{configuration.GetValue<string>("PizzaOrderDialog:DeliveryTime")}",
                        SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                            {
                                new CardAction(type: ActionTypes.ImBack, title: "Now", value: "Now")
                            },
                        }
                    }
                };
                //return await stepContext.PromptAsync(DeliveryDatePrompt,opts);

                return await stepContext.PromptAsync(DeliveryDatePrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }


        private async Task<DialogTurnResult> PreviewOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);
            
            //var datenow = DateTime.Now;
            //var apiKey = "ArYP3wwcJ6Lw5f8S7d7zb4ewXAiQmHp40VLneot7dh-wUwaEUvYv-bxlu5mNHsQP";
            //var prompt = "Where should I ship your order? Type or say an address.";
            //var locationDialog = new LocationDialog(apiKey, stepContext.Context.Activity.ChannelId, prompt);
            if (pizzaOrderState.Ordered == false)
            {
                var previewCard = PreviewOrder(pizzaOrderState);
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"{configuration.GetValue<string>("PizzaOrderDialog:PerviewOrder")}",
                        Attachments = new List<Attachment> { previewCard },
                        
                    }
                };
                return await stepContext.PromptAsync(PreviewOrderPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> CompleteOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(stepContext.Context, () => null);
            var timeLeft = ((DateTime)pizzaOrderState.DeliveryTime - DateTime.Now.AddMinutes(330)).TotalMinutes;
            await stepContext.Context.SendActivityAsync($"Your Order has been placed and the OrderId is {pizzaOrderState.OrderId}. It will reach you in {(int)timeLeft} minutes").ConfigureAwait(false);
            _pizzaOrderingState.DeleteAsync(stepContext.Context);
            return await stepContext.EndDialogAsync();
        }
        protected bool ContainsTime(string timex)
        {
            return timex.Contains("T");
        }

        private async Task<bool> ValidateSize(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(promptContext.Context);
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;

            // Get cognitive models for locale
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cognitiveModels = _services.CognitiveModelSets[locale];

            // check luis intent
            cognitiveModels.LuisServices.TryGetValue("Order", out var luisService);

            // Check luis result
            var luisResult = await luisService.RecognizeAsync<PizzaServices>(promptContext.Context, cancellationToken);
            var intent = luisResult.TopIntent().intent;

            //pizzaOrderState.Size = intent;
            if(luisResult.Entities.pizza_size != null)
            {
                if (luisResult.Entities.pizza_size[0][0] != null)
                {
                    if (luisResult.Entities.pizza_size[0][0].ToString() == "different_size")
                    {
                        await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateSize:DifferentPan")}").ConfigureAwait(false);
                        return false;
                    }

                    pizzaOrderState.Size = luisResult.Entities.pizza_size[0][0].ToString();
                    if (pizzaOrderState.Size == "small")
                    {
                        await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateSize:SmallPizza")}");
                        pizzaOrderState.Price = 150;
                    }
                    else if (pizzaOrderState.Size == "medium")
                    {
                        //await promptContext.Context.SendActivityAsync($"A small personal Pizza.");
                        pizzaOrderState.Price = 250;
                    }
                    else
                    {
                        await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateSize:LargePizza")}");
                        pizzaOrderState.Price = 350;
                    }

                }
            }
            if (!string.IsNullOrWhiteSpace(pizzaOrderState.Size))
            {
                promptContext.Recognized.Value = luisResult.Entities.pizza_size[0][0].ToString();
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateSize:NoSize")}").ConfigureAwait(false);
                return false;
            }
        }

        private async Task<bool> ValidateCheese(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(promptContext.Context);
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;

            // Get cognitive models for locale
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cognitiveModels = _services.CognitiveModelSets[locale];

            // check luis intent
            cognitiveModels.LuisServices.TryGetValue("Order", out var luisService);

            // Check luis result
            var luisResult = await luisService.RecognizeAsync<PizzaServices>(promptContext.Context, cancellationToken);
            var intent = luisResult.TopIntent().intent;

            //pizzaOrderState.Size = intent;
            if (luisResult.Entities.pizza_cheese != null)
            {
                if (luisResult.Entities.pizza_cheese[0][0] != null)
                {
                    pizzaOrderState.Cheese = luisResult.Entities.pizza_cheese[0][0];
                }
            }
            if (!string.IsNullOrWhiteSpace(pizzaOrderState.Cheese))
            {
                //promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateCheese")}").ConfigureAwait(false);
                return false;
            }
        }

        private async Task<bool> ValidateBase(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(promptContext.Context);
            
            if (!string.IsNullOrWhiteSpace(pizzaOrderState.Base))
            {
                if (pizzaOrderState.VegTops != null || pizzaOrderState.NonVegTops != null)
                {
                    if (pizzaOrderState.VegTops == null)
                    {
                        await promptContext.Context.SendActivityAsync($"The base you have selected is: {pizzaOrderState.Base} and the toppings are: {string.Join(", ", pizzaOrderState.NonVegTops)}").ConfigureAwait(false);
                        return true;
                    }
                    if(pizzaOrderState.NonVegTops == null)
                    {
                        await promptContext.Context.SendActivityAsync($"The base you have selected is: {pizzaOrderState.Base} and the toppings are: {string.Join(", ", pizzaOrderState.VegTops)}").ConfigureAwait(false);
                        return true;
                    }
                    await promptContext.Context.SendActivityAsync($"The base you have selected is: {pizzaOrderState.Base} and the toppings are:  {string.Join(", ", pizzaOrderState.VegTops)} {string.Join(", ", pizzaOrderState.NonVegTops)}").ConfigureAwait(false);
                    return true;
                }
                else
                    await promptContext.Context.SendActivityAsync($"{pizzaOrderState.Base} base with no toppings.").ConfigureAwait(false);
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Please use the card to customize the pizza!").ConfigureAwait(false);
                return false;
            }
        }

        private async Task<bool> ValidateSpecialPizza(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(promptContext.Context);


            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;

            // Get cognitive models for locale
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cognitiveModels = _services.CognitiveModelSets[locale];

            // check luis intent
            cognitiveModels.LuisServices.TryGetValue("Order", out var luisService);


            if (pizzaOrderState.IsSpecialPizza != null)
            {
                if(!string.IsNullOrWhiteSpace(pizzaOrderState.PizzaName))
                    await promptContext.Context.SendActivityAsync($"Great choice with {pizzaOrderState.PizzaName}.").ConfigureAwait(false);
                else
                    await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateSpecialPizza:UseCard")}").ConfigureAwait(false);
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateSpecialPizza:CustomPizza")}").ConfigureAwait(false);
                return false;
            }
        }

        private async Task<bool> ValidateSauce(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(promptContext.Context);
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;

            // Get cognitive models for locale
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cognitiveModels = _services.CognitiveModelSets[locale];

            // check luis intent
            cognitiveModels.LuisServices.TryGetValue("Order", out var luisService);

            // Check luis result
            var luisResult = await luisService.RecognizeAsync<PizzaServices>(promptContext.Context, cancellationToken);
            var intent = luisResult.TopIntent().intent;

            //pizzaOrderState.Size = intent;
            if (luisResult.Entities.pizza_sauce != null)
            {
                if (luisResult.Entities.pizza_sauce[0][0] != null)
                {
                    pizzaOrderState.Sauce = luisResult.Entities.pizza_sauce[0][0];
                }
            }
            if (!string.IsNullOrWhiteSpace(pizzaOrderState.Sauce))
            {
                //promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateSauce")}").ConfigureAwait(false);
                return false;
            }
        }
        private async Task<bool> ValidateDate(PromptValidatorContext<IList<DateTimeResolution>> promptContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(promptContext.Context);
            //var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;

            //// Get cognitive models for locale
            //var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            //var cognitiveModels = _services.CognitiveModelSets[locale];

            //// check luis intent
            //cognitiveModels.LuisServices.TryGetValue("Order", out var luisService);
            //// Check luis result
            //var luisResult = await luisService.RecognizeAsync<PizzaServices>(promptContext.Context, cancellationToken);
            //var intent = luisResult.TopIntent().intent;

            //var resolution = TimexResolver.Resolve(new string[] { luisResult.Entities.datetime[0].Expressions[0].ToString() }, DateTime.Now);

            IList<DateTimeResolution> dateTimeResolutions = promptContext.Recognized.Value as IList<DateTimeResolution>;
            foreach (var resolution in dateTimeResolutions)
            {
                var dateTimeConvertType = resolution?.Timex;
                var dateTimeValue = resolution?.Value;
                if (dateTimeValue != null)
                {
                    try
                    {
                        var dateTime = DateTime.Parse(dateTimeValue);

                        if (dateTime != null)
                        {
                            if (ContainsTime(dateTimeConvertType))
                            {
                                pizzaOrderState.DeliveryTime = dateTime.AddMinutes(360);
                                return true;
                            }
                        }
                    }
                    catch (FormatException ex)
                    {
                        await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateDate")}").ConfigureAwait(false);
                        return false;
                    }
                }
            }
            await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidateDate")}").ConfigureAwait(false);
            return false;

        }

        private async Task<bool> ValidatePreview(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var pizzaOrderState = await _pizzaOrderingState.GetAsync(promptContext.Context);
            
            if (pizzaOrderState.Ordered == true)
            {
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"{configuration.GetValue<string>("PizzaOrderDialog:ValidatePreview")}").ConfigureAwait(false);
                return false;
            }
        }

        private Attachment CreateAdaptiveCardforPizzaBase()
        {
            var adaptiveCard = File.ReadAllText($@"{configuration.GetValue<string>("PizzaOrderDialog:CreateAdaptiveCardforPizzaBase")}");
            //adaptiveCard = adaptiveCard.Replace("DOCTORNAME_VALUE", value.strDOCNAME.Trim());
            //adaptiveCard = adaptiveCard.Replace("DOCTORID_VALUE", v.strDOCID.Trim());
            //adaptiveCard = adaptiveCard.Replace("TODAYDATE_VALUE", $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Date}");

            var attachment = new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };

            return attachment;
        }

        private Attachment CreateAdaptiveCardCustomizePizza()
        {
            var adaptiveCard = File.ReadAllText($@"{configuration.GetValue<string>("PizzaOrderDialog:CreateAdaptiveCardCustomizePizza")}");
            //adaptiveCard = adaptiveCard.Replace("DOCTORNAME_VALUE", value.strDOCNAME.Trim());
            //adaptiveCard = adaptiveCard.Replace("DOCTORID_VALUE", v.strDOCID.Trim());
            //adaptiveCard = adaptiveCard.Replace("TODAYDATE_VALUE", $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Date}");

            var attachment = new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };

            return attachment;
        }
        private IList<CardAction> CreateAction(string[] list)
        {
            var newList = new List<CardAction>() { };
            foreach (var action in list)
            {
                newList.Add(new CardAction(type: ActionTypes.ImBack, title: action, value: action));
            }
            return newList;
        }
        private List<Attachment> CreateMenuCarousal(System.Data.DataTable value, string size)
        {
            var fact = 1.0;
            if (size == "small")
                fact = 0.5;
            else if (size == "medium")
                fact = 3.0 / 4.0;
            
            var attachmentList = new List<Attachment>();
            foreach (System.Data.DataRow v in value.Rows)
            {
                var vegTops = "";
                var nonVegTops = "";

                if (v["VegTops"] != null)
                    vegTops = (string)v["VegTops"];
                if (v["NonVegTops"] != null)
                    nonVegTops = (string)v["NonVegTops"];
                var adaptiveCard = File.ReadAllText($@"{configuration.GetValue<string>("PizzaOrderDialog:CreateMenuCarousal")}");
                adaptiveCard = adaptiveCard.Replace("RATING_VALUE",$"★★★★☆ {(string)v["rating"]}");
                adaptiveCard = adaptiveCard.Replace("BASE_VALUE", (string)v["base"]);
                adaptiveCard = adaptiveCard.Replace("PIZZANAME_VALUE", (string)v["pizzaname"]);
                adaptiveCard = adaptiveCard.Replace("PRICE_VALUE", (Convert.ToDouble(v["price"]) * fact).ToString());
                adaptiveCard = adaptiveCard.Replace("VEGTOPS_VALUE", vegTops);
                adaptiveCard = adaptiveCard.Replace("NONVTOPS_VALUE", nonVegTops);
                adaptiveCard = adaptiveCard.Replace("PIZZAIMAGE_VALUE", (string)v["imageurl"]);

                attachmentList.Add(new Attachment
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = JsonConvert.DeserializeObject(adaptiveCard),
                });
            }

            return attachmentList;
        }

        private Attachment PreviewOrder(PizzaOrderState state)
        {
            string vegTops = "";
            string nonVegTops = "";
            float price = 0;
            if(state.IsSpecialPizza == false)
            {
                if (state.VegTops != null)
                    foreach (var item in state.VegTops)
                    {
                        vegTops += $"{item}, ";
                        price += 30;
                    }
                if (state.NonVegTops != null)
                    foreach (var item in state.NonVegTops)
                    {
                        nonVegTops += $"{item}, ";
                        price += 50;
                    }
                state.Price += price;
                if (state.Size == "small")
                {
                    price += 150;
                }
                else if (state.Size == "medium")
                {
                    price += 250;
                }
                else
                    price += 350;
            }
            var adaptiveCard = File.ReadAllText($@"{configuration.GetValue<string>("PizzaOrderDialog:PreviewOrderAttachment")}");
            adaptiveCard = adaptiveCard.Replace("PIZZAIMAGE_VALUE", state.ImageURL);
            adaptiveCard = adaptiveCard.Replace("BASE_VALUE", state.Base.Trim());
            adaptiveCard = adaptiveCard.Replace("PIZZANAME_VALUE", state.PizzaName?.Trim());
            adaptiveCard = adaptiveCard.Replace("PRICE_VALUE", (state.Price).ToString());
            adaptiveCard = adaptiveCard.Replace("TOPPINGS_VALUE", $"{vegTops.Trim()} {nonVegTops.Trim()}");
            adaptiveCard = adaptiveCard.Replace("CHEESE_VALUE", state.Cheese.Trim());
            adaptiveCard = adaptiveCard.Replace("SAUCE_VALUE", state.Sauce.Trim());
            adaptiveCard = adaptiveCard.Replace("LOCATION_VALUE", state.Location.Trim());
            adaptiveCard = adaptiveCard.Replace("TIME_VALUE",$"{state.DeliveryTime.Value.ToShortDateString()}, {state.DeliveryTime.Value.ToShortTimeString()}");
            //adaptiveCard = adaptiveCard.Replace("TODAYDATE_VALUE", $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Date}");
            var attachment = new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };

            return attachment;
        }

    }
}
