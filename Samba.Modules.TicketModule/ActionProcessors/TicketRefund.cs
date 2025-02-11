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

namespace Samba.Modules.TicketModule.ActionProcessors
{
    [Export(typeof(IActionType))]
    class TicketRefund : ActionType   //This entire class is added by Tim GU
    {
        private readonly ITicketService _ticketService;
        [ImportingConstructor]
        public TicketRefund(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        public override void Process(ActionData actionData)
        {
            var ticket = actionData.GetDataValue<Ticket>("Ticket");
            var ticketState = ticket.GetStateData(x => true);
            if (ticket != null && ticketState.Contains("Paid"))
            {
                if(ticket.Calculations.Count == 0) 
                {
                    if (MessageBox.Show("Veuillez confirmer pour effectuer un remboursement\nen fonction du ticket sélectionné.", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        var tid = ticket.Id;
                        EventServiceFactory.EventService.PublishEvent(EventTopicNames.CloseTicketRequested, true);
                        ticket = _ticketService.OpenTicket(tid);
                        var commitResult = _ticketService.TicketRefund(ticket);

                        if (string.IsNullOrEmpty(commitResult.ErrorMessage) && commitResult.TicketId > 0)
                        {
                            ExtensionMethods.PublishIdEvent(commitResult.TicketId, EventTopicNames.DisplayTicket);
                        }
                        else
                        {
                            ExtensionMethods.PublishIdEvent(tid, EventTopicNames.DisplayTicket);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Désolé, ce type de ticket avec des frais supplémentaires\nne peut pas être remboursé pour le moment!", "Oops");
                }    
            }
        }

        protected override object GetDefaultData()
        {
            return new object();
        }

        protected override string GetActionName()
        {
            return Resources.RefundTicket;
        }

        protected override string GetActionKey()
        {
            return ActionNames.RefundTicket;
        }
    }
}
