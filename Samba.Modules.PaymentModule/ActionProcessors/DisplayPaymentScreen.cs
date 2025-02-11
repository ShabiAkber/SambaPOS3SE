using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Samba.Domain.Models.Tickets;
using Samba.Localization.Properties;
using Samba.Presentation.Services;
using Samba.Presentation.Services.Common;
using Samba.Services.Common;
using System.Windows.Forms;

namespace Samba.Modules.PaymentModule.ActionProcessors
{
    [Export(typeof(IActionType))]
    class DisplayPaymentScreen : ActionType
    {
        private readonly ITicketService _ticketService;

        [ImportingConstructor]
        public DisplayPaymentScreen(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        public override void Process(ActionData actionData)
        {
            var ticket = actionData.GetDataValue<Ticket>("Ticket");
            if (ticket != null && ticket != Ticket.Empty && _ticketService.CanSettleTicket(ticket))
            {
                #region SRS_Related
                if (ticket.IsRefund)    
                {
                    bool hasNoRefundOrder = true;
                    for(int i = 0; i<ticket.Orders.Count; i++)
                    {
                        if (ticket.Orders[i].Price < 0)
                        {
                            hasNoRefundOrder = false; 
                            break;
                        }
                    }
                    if (hasNoRefundOrder)
                    {
                        MessageBox.Show("S'il vous plaît remboursez au moins une commande!","ATTENTION", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                    else
                    {
                        bool isOrderRemoved = false;
                        for (int i = ticket.Orders.Count - 1; i >= 0; i--)
                        {
                            var order = ticket.Orders[i];
                            if (order.Price > 0)
                            {
                                ticket.Orders.RemoveAt(i);       //In this case, only refund orders (with a negative price) are allowed to enter the Payment process
                                isOrderRemoved = true;
                            }
                        }
                        if (isOrderRemoved)
                        {
                            ticket.Recalculate();
                        }
                        ticket.IsLocked = true;
                    }                    
                }
                #endregion   //Region Code Block added by Tim GU, to support partial refund
                ticket.IsRFER = true;
                ticket.PublishEvent(EventTopicNames.MakePayment);
            }
        }

        protected override object GetDefaultData()
        {
            return new object();
        }

        protected override string GetActionName()
        {
            return Resources.DisplayPaymentScreen;
        }

        protected override string GetActionKey()
        {
            return ActionNames.DisplayPaymentScreen;
        }
    }
}
