using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Samba.Domain.Models.Tickets;
using Samba.Localization.Properties;
using Samba.Presentation.Services.Common;
using Samba.Services.Common;

namespace Samba.Modules.TicketModule.ActionProcessors
{
    [Export(typeof(IActionType))]
    class UnlockTicket : ActionType
    {
        public override void Process(ActionData actionData)
        {
            var ticket = actionData.GetDataValue<Ticket>("Ticket");
            if (ticket != null && !ticket.IsRefund) ticket.UnLock();   //Modified by Tim GU, to avoid adding any calculations (discount/tip/rounding) to a refund ticket during the payment process
            EventServiceFactory.EventService.PublishEvent(EventTopicNames.UnlockTicketRequested);
        }

        protected override object GetDefaultData()
        {
            return new object();
        }

        protected override string GetActionName()
        {
            return Resources.UnlockTicket;
        }

        protected override string GetActionKey()
        {
            return ActionNames.UnlockTicket;
        }
    }
}
