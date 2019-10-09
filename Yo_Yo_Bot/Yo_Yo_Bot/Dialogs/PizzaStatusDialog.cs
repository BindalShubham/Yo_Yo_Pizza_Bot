using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yo_Yo_Bot.Data;
using Yo_Yo_Bot.Models;

namespace Yo_Yo_Bot.Dialogs
{
    public class PizzaStatusDialog : ComponentDialog
    {
        private IConfiguration configuration;
        private IStatePropertyAccessor<PizzaStatusModel> _pizzaStatusModel;

        private const string OrderIdPrompt = "orderIdPrompt";
        private const string StatusDialog = "statusDialog";


        public PizzaStatusDialog(UserState userState, IConfiguration iConfig) : base(nameof(PizzaStatusDialog))
        {
            configuration = iConfig;
            _pizzaStatusModel = userState.CreateProperty<PizzaStatusModel>(nameof(PizzaStatusModel));
            var pizzaStatusSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                getOrderId,
                completeStatus
            };
            AddDialog(new WaterfallDialog(StatusDialog, pizzaStatusSteps));
            AddDialog(new TextPrompt(OrderIdPrompt, validateOrderId));

            InitialDialogId = StatusDialog;

        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaStatusModel = await _pizzaStatusModel.GetAsync(stepContext.Context, () => null);
            if (pizzaStatusModel == null)
            {
                await _pizzaStatusModel.SetAsync(stepContext.Context, new PizzaStatusModel());

            }
            return await stepContext.NextAsync();

        }

        private async Task<DialogTurnResult> getOrderId(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var pizzaStatusModel = await _pizzaStatusModel.GetAsync(stepContext.Context, () => new PizzaStatusModel());
            if (pizzaStatusModel.OrderId == null)
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = "Please provide the orderId."
                    },

                };
                return await stepContext.PromptAsync(OrderIdPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> completeStatus(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }

        private async Task<bool> validateOrderId(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var pizzaStatusModel = await _pizzaStatusModel.GetAsync(promptContext.Context, () => new PizzaStatusModel());
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;

            // Get cognitive models for locale
            if (Enumerable.Range(1, 100).Contains(Convert.ToInt32(value)))
            {
                pizzaStatusModel.OrderId = Convert.ToInt32(value);
                var connect = new DataBaseOperations(configuration.GetValue<string>("sqlDb:connectionString"));
                var order = connect.Get(pizzaStatusModel.OrderId);
                if(order != null)
                {
                    pizzaStatusModel.DeliveryTime = Convert.ToDateTime( order["time"]);
                    if((int)((pizzaStatusModel.DeliveryTime - DateTime.Now.AddMinutes(330)).TotalMinutes) <= 0)
                    {
                        await promptContext.Context.SendActivityAsync($"This order has been delivered at {(pizzaStatusModel.DeliveryTime).ToShortDateString()}, {(pizzaStatusModel.DeliveryTime).ToShortTimeString()}.").ConfigureAwait(false);
                        return true;
                    }
                    else
                    {
                        await promptContext.Context.SendActivityAsync($"Your order will reach you hot in {(int)((pizzaStatusModel.DeliveryTime - DateTime.Now.AddMinutes(330)).TotalMinutes)} minutes.").ConfigureAwait(false);
                        return true;
                    }
                }
                await promptContext.Context.SendActivityAsync($"There is no order with this order Id. Please try again.").ConfigureAwait(false);
                return false;

            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Issue with the order Id.").ConfigureAwait(false);
                return false;
            }
        }

    }
}
