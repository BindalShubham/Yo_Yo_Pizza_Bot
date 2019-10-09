// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Yo_Yo_Bot.Data;
using Yo_Yo_Bot.Models;
using Yo_Yo_Bot.Responses.Main;
using Yo_Yo_Bot.Responses.Onboarding;
using Yo_Yo_Bot.Services;

namespace Yo_Yo_Bot.Dialogs
{
    public class OnboardingDialog : ComponentDialog
    {
        private static OnboardingResponses _responder = new OnboardingResponses();
        private IStatePropertyAccessor<OnboardingState> _accessor;
        private OnboardingState _state;

        private BotServices _services;


        public OnboardingDialog(
            BotServices services,
            UserState userState,
            IBotTelemetryClient telemetryClient)
            : base(nameof(OnboardingDialog))
        {
            _accessor = userState.CreateProperty<OnboardingState>(nameof(OnboardingState));
            InitialDialogId = "onboard";

            var onboarding = new WaterfallStep[]
            {
                AskForName,
                AskForNumber,
                FinishOnboardingDialog,
            };

            // To capture built-in waterfall dialog telemetry, set the telemetry client
            // to the new waterfall dialog and add it to the component dialog
            TelemetryClient = telemetryClient;
            AddDialog(new WaterfallDialog(InitialDialogId, onboarding) { TelemetryClient = telemetryClient });
            AddDialog(new TextPrompt(DialogIds.NamePrompt,validateName));
            AddDialog(new TextPrompt(DialogIds.NumberPrompt,validateNumber));
            AddDialog(new AttachmentPrompt(DialogIds.GreetingPrompt));
            _services = services;
        }

        public async Task<DialogTurnResult> AskForName(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            _state = await _accessor.GetAsync(sc.Context);

            if (!string.IsNullOrEmpty(_state.Name))
            {
                return await sc.NextAsync();
            }
            else
            {
                return await sc.PromptAsync(DialogIds.NamePrompt, new PromptOptions
                {

                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"Whats your name?",
                    },  
                });

            }
        }

        public async Task<DialogTurnResult> AskForNumber(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            _state = await _accessor.GetAsync(sc.Context);
            
            if(_state.Number != 0)
            {
                await sc.NextAsync();
            }

            return await sc.PromptAsync(DialogIds.NumberPrompt, new PromptOptions()
            {
                Prompt = new Activity
                {
                    Type = ActivityTypes.Message,
                    Text = "Please help me with your phone number?"
                },
            });
        }

        public async Task<DialogTurnResult> FinishOnboardingDialog(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            var view = new MainResponses();

            _state = await _accessor.GetAsync(sc.Context);
            await _accessor.SetAsync(sc.Context, _state, cancellationToken);
            var name = _state.Name;

            var connect = new DataBaseOperations("Server=tcp:yoyopizzaserver.database.windows.net,1433;Initial Catalog=PizzaOrderdb;Persist Security Info=False;User ID=shubham;Password=Dota365365;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
            connect.addUserInfo(_state);
            //await _responder.ReplyWith(sc.Context, OnboardingResponses.ResponseIds.HaveNameMessage, new { name });
            var attachement = GetGreetingCard(name);
            var opts = new PromptOptions
            {
                Prompt = new Activity
                {
                    Type = ActivityTypes.Message,
                    Attachments = new List<Attachment> { attachement },
                },

            };
            await sc.PromptAsync(DialogIds.GreetingPrompt, opts);
            return await sc.EndDialogAsync();
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

        private async Task<bool> validateNumber(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var _state = await _accessor.GetAsync(promptContext.Context);
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;

            // Get cognitive models for locale
            if (Regex.Match(value, @"^([0-9]{10})$").Success && Convert.ToInt64(value) >= 1000000000)
            {
                _state.Number = Convert.ToInt64(value);
                return true;

            }
            else
            {
                await promptContext.Context.SendActivityAsync($"This phone number looks weird. Please provide 10 digit phone number without any extension/special characters.").ConfigureAwait(false);
                return false;
            }
        }

        private async Task<bool> validateName(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var _state = await _accessor.GetAsync(promptContext.Context, () => new OnboardingState());
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
            if (luisResult.Entities.personName != null)
            {
                if (luisResult.Entities.personName[0] != null)
                {
                    _state.Name = luisResult.Entities.personName[0];
                    return true;
                }
                await promptContext.Context.SendActivityAsync($"I am really sorry but can you please try again.").ConfigureAwait(false);
                return false;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"May I know you name, please!.").ConfigureAwait(false);
                return false;
            }
        }

        private class DialogIds
        {
            public const string NamePrompt = "namePrompt";
            public const string NumberPrompt = "numberPrompt";
            public const string GreetingPrompt = "greetingPrompt";

        }

    }
}
