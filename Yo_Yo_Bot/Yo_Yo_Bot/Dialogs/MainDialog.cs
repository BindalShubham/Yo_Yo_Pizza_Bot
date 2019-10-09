// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Builder.Solutions;
using Microsoft.Bot.Builder.Solutions.Dialogs;
using Microsoft.Bot.Builder.Solutions.Feedback;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Yo_Yo_Bot.Data;
using Yo_Yo_Bot.Models;
using Yo_Yo_Bot.Responses.Cancel;
using Yo_Yo_Bot.Responses.Main;
using Yo_Yo_Bot.Services;

namespace Yo_Yo_Bot.Dialogs
{
    public class MainDialog : RouterDialog
    {
        private const string Location = "location";
        private const string TimeZone = "timezone";
        private BotSettings _settings;
        private BotServices _services;
        private UserState _userState;
        private MainResponses _responder = new MainResponses();
        private IStatePropertyAccessor<PizzaOrderState> _pizzaOrderingState;
        private IStatePropertyAccessor<OnboardingState> _onboardingState;
        private IStatePropertyAccessor<SkillContext> _skillContextAccessor;
        private IStatePropertyAccessor<PizzaStatusModel> _pizzaStatusModel;

        public MainDialog(
            BotSettings settings,
            BotServices services,
            PizzaStatusDialog pizzaStatusDialog,
            PizzaOrderDialog pizzaOrderingDialog,
            OnboardingDialog onboardingDialog,
            EscalateDialog escalateDialog,
            CancelDialog cancelDialog,
            List<SkillDialog> skillDialogs,
            IBotTelemetryClient telemetryClient,
            UserState userState)
            : base(nameof(MainDialog), telemetryClient)
        {
            _settings = settings;
            _services = services;
            _userState = userState;
            TelemetryClient = telemetryClient;
            _pizzaStatusModel = userState.CreateProperty<PizzaStatusModel>(nameof(PizzaStatusModel));
            _pizzaOrderingState = userState.CreateProperty<PizzaOrderState>(nameof(PizzaOrderState));
            _onboardingState = userState.CreateProperty<OnboardingState>(nameof(OnboardingState));
            _skillContextAccessor = userState.CreateProperty<SkillContext>(nameof(SkillContext));
            
            AddDialog(pizzaStatusDialog);
            AddDialog(pizzaOrderingDialog);
            AddDialog(onboardingDialog);
            AddDialog(escalateDialog);
            AddDialog(cancelDialog);

            foreach (var skillDialog in skillDialogs)
            {
                AddDialog(skillDialog);
            }
        }

        protected override async Task OnStartAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var onboardingState = await _onboardingState.GetAsync(dc.Context, () => new OnboardingState());

            if (string.IsNullOrEmpty(onboardingState.Name) || onboardingState.Number == 0)
            {
                await _responder.ReplyWith(dc.Context, MainResponses.ResponseIds.NewUserGreeting);
                await dc.BeginDialogAsync(nameof(OnboardingDialog));
            }
            else
            {
                var attachement = GetGreetingCard(onboardingState.Name);
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Attachments = new List<Attachment> { attachement },
                    },

                };
                await dc.PromptAsync("GreetingPrompt", opts);
            }
        }

        protected override async Task RouteAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Get cognitive models for locale
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cognitiveModels = _services.CognitiveModelSets[locale];

            // Check dispatch result
            var dispatchResult = await cognitiveModels.DispatchService.RecognizeAsync<DispatchLuis>(dc.Context, CancellationToken.None);
            var intent = dispatchResult.TopIntent().intent;

            // Identify if the dispatch intent matches any Action within a Skill if so, we pass to the appropriate SkillDialog to hand-off
            var identifiedSkill = SkillRouter.IsSkill(_settings.Skills, intent.ToString());

            if (intent.ToString() == "Order_Pizza")
            {
                await dc.BeginDialogAsync(nameof(PizzaOrderDialog));
                return;
            }
            if (intent.ToString() == "Order_Status")
            {
                await dc.BeginDialogAsync(nameof(PizzaStatusDialog));
                return;
            }
            if (intent.ToString() == "another_pizza")
            {
                await _pizzaOrderingState.DeleteAsync(dc.Context);
                await dc.BeginDialogAsync(nameof(PizzaStatusDialog));
                return;
            }
            //if (intent.ToString() == "Onboard")
            //{
            //    var result = await dc.BeginDialogAsync(nameof(OnboardingDialog));
            //    if (result.Status == DialogTurnStatus.Complete)
            //    {
            //        await CompleteAsync(dc);
            //    }
            //}
            if (identifiedSkill != null)
            {
                // We have identiifed a skill so initialize the skill connection with the target skill
                var result = await dc.BeginDialogAsync(identifiedSkill.Id);

                if (result.Status == DialogTurnStatus.Complete)
                {
                    await CompleteAsync(dc);
                }
            }
            else if (intent == DispatchLuis.Intent.l_General)
            {
                // If dispatch result is General luis model
                cognitiveModels.LuisServices.TryGetValue("General", out var luisService);

                if (luisService == null)
                {
                    throw new Exception("The General LUIS Model could not be found in your Bot Services configuration.");
                }
                else
                {
                    var result = await luisService.RecognizeAsync<GeneralLuis>(dc.Context, CancellationToken.None);

                    var generalIntent = result?.TopIntent().intent;

                    // switch on general intents
                    switch (generalIntent)
                    {
                        case GeneralLuis.Intent.Escalate:
                            {
                                // start escalate dialog
                                await dc.BeginDialogAsync(nameof(EscalateDialog));
                                break;
                            }

                        case GeneralLuis.Intent.None:
                        default:
                            {
                                // No intent was identified, send confused message
                                await _responder.ReplyWith(dc.Context, MainResponses.ResponseIds.Confused);
                                break;
                            }
                    }
                }
            }
            else if (intent == DispatchLuis.Intent.q_Faq)
            {
                cognitiveModels.QnAServices.TryGetValue("Faq", out var qnaService);

                if (qnaService == null)
                {
                    throw new Exception("The specified QnA Maker Service could not be found in your Bot Services configuration.");
                }
                else
                {
                    var answers = await qnaService.GetAnswersAsync(dc.Context, null, null);

                    if (answers != null && answers.Count() > 0)
                    {
                        await dc.Context.SendActivityAsync(answers[0].Answer, speak: answers[0].Answer);
                    }
                    else
                    {
                        await _responder.ReplyWith(dc.Context, MainResponses.ResponseIds.Confused);
                    }
                }
            }
            else if (intent == DispatchLuis.Intent.q_Chitchat)
            {
                cognitiveModels.QnAServices.TryGetValue("Chitchat", out var qnaService);

                if (qnaService == null)
                {
                    throw new Exception("The specified QnA Maker Service could not be found in your Bot Services configuration.");
                }
                else
                {
                    var answers = await qnaService.GetAnswersAsync(dc.Context, null, null);

                    if (answers != null && answers.Count() > 0)
                    {
                        await dc.Context.SendActivityAsync(answers[0].Answer, speak: answers[0].Answer);
                    }
                    else
                    {
                        await _responder.ReplyWith(dc.Context, MainResponses.ResponseIds.Confused);
                    }
                }
            }
            else
            {
                // If dispatch intent does not map to configured models, send "confused" response.
                // Alternatively as a form of backup you can try QnAMaker for anything not understood by dispatch.
                await _responder.ReplyWith(dc.Context, MainResponses.ResponseIds.Confused);
            }
        }

        protected override async Task OnEventAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Check if there was an action submitted from intro card
            var value = dc.Context.Activity.Value;

            if (value.GetType() == typeof(JObject))
            {
                var submit = JObject.Parse(value.ToString());
                if (value != null && (string)submit["action"] == "startOnboarding")
                {
                    await dc.BeginDialogAsync(nameof(OnboardingDialog));
                    return;
                }
                if (value != null && (string)submit["action"] == "pizzaOrdering")
                {
                    await dc.BeginDialogAsync(nameof(PizzaOrderDialog));
                    return;
                }
                if (value != null && (string)submit["action"] == "orderStatus")
                {
                    await dc.BeginDialogAsync(nameof(PizzaStatusDialog));
                    return;
                }
                if (value != null && (string)submit["action"] == "pizza_base_tops")
                {
                    var pizzaOrderState = await _pizzaOrderingState.GetAsync(dc.Context);

                    pizzaOrderState.Base = (string)submit["BaseChoice"];

                    var vegTops = (string)submit[$"{pizzaOrderState.Base}VegTops"];
                    var nonVegTops = (string)submit[$"{pizzaOrderState.Base}NonVegTops"];

                    if (!string.IsNullOrEmpty(vegTops))
                        pizzaOrderState.VegTops = vegTops.Split(",");
                    if (!string.IsNullOrEmpty(nonVegTops))
                        pizzaOrderState.NonVegTops = nonVegTops.Split(",");

                    await dc.ContinueDialogAsync();
                    return;
                }
                if (value != null && (string)submit["action"] == "specialPizza")
                {
                    var pizzaOrderState = await _pizzaOrderingState.GetAsync(dc.Context);

                    pizzaOrderState.IsSpecialPizza = false;
                    if ((string)submit["PizzaChoice"] != "customize")
                    {
                        var fact = 1.0;
                        if (pizzaOrderState.Size == "small")
                            fact = 0.5;
                        else if (pizzaOrderState.Size == "medium")
                            fact = 3.0 / 4.0;
                        //pizzaOrderState.Toppings = (string)submit["Toppings"];
                        pizzaOrderState.IsSpecialPizza = true;
                        pizzaOrderState.PizzaName = (string)submit["PizzaChoice"];
                        var connect = new DataBaseOperations("Server=tcp:yoyopizzaserver.database.windows.net,1433;Initial Catalog=PizzaOrderdb;Persist Security Info=False;User ID=shubham;Password=Dota365365;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
                        var pizza = connect.GetByName(pizzaOrderState.PizzaName);
                        pizzaOrderState.Base = (string)pizza["base"];
                        pizzaOrderState.Cheese = (string)pizza["cheese"];
                        pizzaOrderState.Sauce = (string)pizza["sauce"];
                        pizzaOrderState.Rating = (string)pizza["rating"];
                        pizzaOrderState.ImageURL = (string)pizza["imageurl"];
                        pizzaOrderState.VegTops = ((string)pizza["vegtops"]).Split(", ");
                        pizzaOrderState.NonVegTops = ((string)pizza["nonvegtops"]).Split(", ");
                        pizzaOrderState.Price = (Convert.ToDouble(pizza["price"]) * fact);

                    }
                    else
                        pizzaOrderState.PizzaName = "Your Pizza";

                    await dc.ContinueDialogAsync();
                    return;
                }
                if (value != null && (string)submit["action"] == "confirmOrder")
                {
                    var pizzaOrderState = await _pizzaOrderingState.GetAsync(dc.Context);
                    var onboardingState = await _onboardingState.GetAsync(dc.Context);
                    pizzaOrderState.Ordered = false;
                    var connect = new DataBaseOperations("Server=tcp:yoyopizzaserver.database.windows.net,1433;Initial Catalog=PizzaOrderdb;Persist Security Info=False;User ID=shubham;Password=Dota365365;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
                    pizzaOrderState.OrderId = connect.Add(pizzaOrderState, onboardingState.Number);
                    pizzaOrderState.Ordered = true;
                    await dc.ContinueDialogAsync();
                    return;
                }
            }

            var forward = true;
            var ev = dc.Context.Activity.AsEventActivity();
            if (!string.IsNullOrWhiteSpace(ev.Name))
            {
                switch (ev.Name)
                {
                    case Events.TimezoneEvent:
                        {
                            try
                            {
                                var timezone = ev.Value.ToString();
                                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                                var timeZoneObj = new JObject();
                                timeZoneObj.Add(TimeZone, JToken.FromObject(tz));

                                var skillContext = await _skillContextAccessor.GetAsync(dc.Context, () => new SkillContext());
                                if (skillContext.ContainsKey(TimeZone))
                                {
                                    skillContext[TimeZone] = timeZoneObj;
                                }
                                else
                                {
                                    skillContext.Add(TimeZone, timeZoneObj);
                                }

                                await _skillContextAccessor.SetAsync(dc.Context, skillContext);
                            }
                            catch
                            {
                                await dc.Context.SendActivityAsync(new Activity(type: ActivityTypes.Trace, text: $"Timezone passed could not be mapped to a valid Timezone. Property not set."));
                            }

                            forward = false;
                            break;
                        }

                    case Events.LocationEvent:
                        {
                            var location = ev.Value.ToString();
                            var locationObj = new JObject();
                            locationObj.Add(Location, JToken.FromObject(location));

                            var skillContext = await _skillContextAccessor.GetAsync(dc.Context, () => new SkillContext());
                            if (skillContext.ContainsKey(Location))
                            {
                                skillContext[Location] = locationObj;
                            }
                            else
                            {
                                skillContext.Add(Location, locationObj);
                            }

                            await _skillContextAccessor.SetAsync(dc.Context, skillContext);

                            forward = false;
                            break;
                        }

                    case TokenEvents.TokenResponseEventName:
                        {
                            forward = true;
                            break;
                        }

                    default:
                        {
                            await dc.Context.SendActivityAsync(new Activity(type: ActivityTypes.Trace, text: $"Unknown Event {ev.Name} was received but not processed."));
                            forward = false;
                            break;
                        }
                }
            }

            if (forward)
            {
                var result = await dc.ContinueDialogAsync();

                if (result.Status == DialogTurnStatus.Complete)
                {
                    await CompleteAsync(dc);
                }
            }
        }

        protected override async Task CompleteAsync(DialogContext dc, DialogTurnResult result = null, CancellationToken cancellationToken = default(CancellationToken))
        {

            // The active dialog's stack ended with a complete status
            await _responder.ReplyWith(dc.Context, MainResponses.ResponseIds.Completed);

            // Request feedback on the last activity.
            if (Id != nameof(OnboardingDialog))
                await FeedbackMiddleware.RequestFeedbackAsync(dc.Context, Id);
        }

        protected override async Task<InterruptionAction> OnInterruptDialogAsync(DialogContext dc, CancellationToken cancellationToken)
        {
            if (dc.Context.Activity.Type == ActivityTypes.Message && !string.IsNullOrWhiteSpace(dc.Context.Activity.Text))
            {
                // get current activity locale
                var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var cognitiveModels = _services.CognitiveModelSets[locale];

                // check luis intent
                cognitiveModels.LuisServices.TryGetValue("General", out var luisService);
                if (luisService == null)
                {
                    throw new Exception("The General LUIS Model could not be found in your Bot Services configuration.");
                }
                else
                {
                    var luisResult = await luisService.RecognizeAsync<GeneralLuis>(dc.Context, cancellationToken);
                    var intent = luisResult.TopIntent().intent;

                    if (luisResult.TopIntent().score > 0.5)
                    {
                        switch (intent)
                        {
                            case GeneralLuis.Intent.Cancel:
                                {
                                    return await OnCancel(dc);
                                }

                            case GeneralLuis.Intent.Help:
                                {
                                    return await OnHelp(dc);
                                }

                            case GeneralLuis.Intent.Logout:
                                {
                                    return await OnLogout(dc);
                                }
                            case GeneralLuis.Intent.reset:
                                {
                                    return await OnReset(dc);
                                }
                        }
                    }
                }
            }

            return InterruptionAction.NoAction;
        }

        private async Task<InterruptionAction> OnCancel(DialogContext dc)
        {
            if (dc.ActiveDialog != null && dc.ActiveDialog.Id != nameof(CancelDialog))
            {
                // Don't start restart cancel dialog
                await dc.BeginDialogAsync(nameof(CancelDialog));

                // Signal that the dialog is waiting on user response
                return InterruptionAction.StartedDialog;
            }

            var view = new CancelResponses();
            await view.ReplyWith(dc.Context, CancelResponses.ResponseIds.NothingToCancelMessage);

            return InterruptionAction.StartedDialog;
        }

        private async Task<InterruptionAction> OnHelp(DialogContext dc)
        {
            var view = new MainResponses();
            await view.ReplyWith(dc.Context, MainResponses.ResponseIds.Help);

            // Signal the conversation was interrupted and should immediately continue
            return InterruptionAction.MessageSentToUser;
        }

        private async Task<InterruptionAction> OnReset(DialogContext dc)
        {
            await _userState.ClearStateAsync(dc.Context);
            var onboardingState = await _onboardingState.GetAsync(dc.Context);

            await dc.Context.SendActivityAsync($"The state has been reset.").ConfigureAwait(false);

            if (dc.ActiveDialog != null && dc.ActiveDialog.Id != nameof(CancelDialog))
            {
                // Don't start restart cancel dialog
                await dc.CancelAllDialogsAsync();

                // Signal that the dialog is waiting on user response
                return InterruptionAction.StartedDialog;
            }

            var view = new CancelResponses();
            await view.ReplyWith(dc.Context, CancelResponses.ResponseIds.NothingToCancelMessage);

            return InterruptionAction.StartedDialog;
        }

        private async Task<InterruptionAction> OnLogout(DialogContext dc)
        {
            IUserTokenProvider tokenProvider;
            var supported = dc.Context.Adapter is IUserTokenProvider;
            if (!supported)
            {
                throw new InvalidOperationException("OAuthPrompt.SignOutUser(): not supported by the current adapter");
            }
            else
            {
                tokenProvider = (IUserTokenProvider)dc.Context.Adapter;
            }

            await dc.CancelAllDialogsAsync();

            // Sign out user
            var tokens = await tokenProvider.GetTokenStatusAsync(dc.Context, dc.Context.Activity.From.Id);
            foreach (var token in tokens)
            {
                await tokenProvider.SignOutUserAsync(dc.Context, token.ConnectionName);
            }

            await dc.Context.SendActivityAsync(MainStrings.LOGOUT);

            return InterruptionAction.StartedDialog;
        }

        private Attachment GetGreetingCard(string name)
        {
            var adaptiveCard = File.ReadAllText(@".\Content\Pizza\UserGreeting.json");
            adaptiveCard = adaptiveCard.Replace("USERNAME_VALUE", name);

            var attachment = new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };

            return attachment;
        }

        private class Events
        {
            public const string TimezoneEvent = "VA.Timezone";
            public const string LocationEvent = "VA.Location";
        }
    }
}