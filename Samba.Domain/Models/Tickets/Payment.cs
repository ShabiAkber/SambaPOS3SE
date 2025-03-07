﻿using System;
using Samba.Domain.Models.Accounts;
using Samba.Infrastructure.Data;

namespace Samba.Domain.Models.Tickets
{
    public class Payment : ValueClass
    {
        public Payment()
        {
            Date = DateTime.Now;
        }

        public int PaymentTypeId { get; set; }
        public int TicketId { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public int AccountTransactionId { get; set; }
        public virtual AccountTransaction AccountTransaction { get; set; }
        public decimal Amount { get; set; }
        public decimal VersActu {  get; set; }  //Added by Tim GU
        public decimal VersAnt {  get; set; }   //Added by Tim GU
        public decimal Sold {  get; set; }   //Added by Tim GU
        public int UserId { get; set; }
    }
}
