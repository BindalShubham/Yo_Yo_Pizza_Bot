// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Yo_Yo_Bot.Responses.Escalate;
using Yo_Yo_Bot.Services;

namespace Yo_Yo_Bot.Dialogs
{
    public class EscalateDialog : ComponentDialog
    {
        private EscalateResponses _responder = new EscalateResponses();

        public EscalateDialog(BotServices botServices, IBotTelemetryClient telemetryClient)
            : base(nameof(EscalateDialog))
        {
            InitialDialogId = nameof(EscalateDialog);

            var escalate = new WaterfallStep[]
            {
                SendPhone,
            };

            AddDialog(new WaterfallDialog(InitialDialogId, escalate));
        }

        private async Task<DialogTurnResult> SendPhone(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            await _responder.ReplyWith(sc.Context, EscalateResponses.ResponseIds.SendPhoneMessage);
            return await sc.EndDialogAsync();
        }
    }
}
