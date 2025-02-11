using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Sockets;
using Samba.Domain.Models.Settings;
using Samba.Domain.Models.Tickets;
using Samba.Infrastructure.Helpers;
using Samba.Infrastructure.Settings;
using Samba.Localization.Properties;
using Samba.Presentation.Common;
using Samba.Presentation.Common.Commands;
using Samba.Presentation.Services;
using Samba.Presentation.Services.Common;
using Samba.Presentation.ViewModels;
//using SRSSDK;
using System.Windows.Forms;
using Samba.Services.Common;
using Samba.Persistance;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using Samba.Services;

namespace Samba.Modules.PaymentModule
{
    [Export]
    public class PaymentEditorViewModel : ObservableObject
    {
        private readonly IApplicationState _applicationState;

        private readonly ICaptionCommand _makePaymentCommand;
        private readonly ICaptionCommand _selectChangePaymentTypeCommand;

        private readonly TicketTotalsViewModel _paymentTotals;
        private readonly PaymentEditor _paymentEditor;
        private readonly NumberPadViewModel _numberPadViewModel;
        private readonly OrderSelectorViewModel _orderSelectorViewModel;
        private readonly ITicketService _ticketService;
        private readonly ForeignCurrencyButtonsViewModel _foreignCurrencyButtonsViewModel;
        private readonly CommandButtonsViewModel _commandButtonsViewModel;
        private readonly TenderedValueViewModel _tenderedValueViewModel;
        private readonly ReturningAmountViewModel _returningAmountViewModel;
        private readonly ChangeTemplatesViewModel _changeTemplatesViewModel;
        private readonly AccountBalances _accountBalances;

        [ImportingConstructor]
        public PaymentEditorViewModel(IApplicationState applicationState,
            TicketTotalsViewModel paymentTotals, PaymentEditor paymentEditor, NumberPadViewModel numberPadViewModel,
            OrderSelectorViewModel orderSelectorViewModel, ITicketService ticketService,
            ForeignCurrencyButtonsViewModel foreignCurrencyButtonsViewModel, PaymentButtonsViewModel paymentButtonsViewModel,
            CommandButtonsViewModel commandButtonsViewModel, TenderedValueViewModel tenderedValueViewModel,
            ReturningAmountViewModel returningAmountViewModel, ChangeTemplatesViewModel changeTemplatesViewModel, AccountBalances accountBalances)
        {
            _applicationState = applicationState;
            _paymentTotals = paymentTotals;
            _paymentEditor = paymentEditor;
            _numberPadViewModel = numberPadViewModel;
            _orderSelectorViewModel = orderSelectorViewModel;
            _ticketService = ticketService;
            _foreignCurrencyButtonsViewModel = foreignCurrencyButtonsViewModel;
            _commandButtonsViewModel = commandButtonsViewModel;
            _tenderedValueViewModel = tenderedValueViewModel;
            _returningAmountViewModel = returningAmountViewModel;
            _changeTemplatesViewModel = changeTemplatesViewModel;
            _accountBalances = accountBalances;

            _makePaymentCommand = new CaptionCommand<PaymentType>("", OnMakePayment, CanMakePayment);
            _selectChangePaymentTypeCommand = new CaptionCommand<PaymentData>("", OnSelectChangePaymentType);

            ClosePaymentScreenCommand = new CaptionCommand<string>(Resources.Close, OnClosePaymentScreen, CanClosePaymentScreen);
            paymentButtonsViewModel.SetButtonCommands(_makePaymentCommand, null, ClosePaymentScreenCommand);
        }

        public CaptionCommand<string> ClosePaymentScreenCommand { get; set; }

        public string SelectedTicketTitle { get { return _paymentTotals.TitleWithAccountBalances; } }

        private bool CanMakePayment(PaymentType arg)
        {
            if (arg == null) return false;
            if (_paymentEditor.AccountMode && _tenderedValueViewModel.GetTenderedValue() > _tenderedValueViewModel.GetPaymentDueValue()) return false;
            if (_paymentEditor.AccountMode && arg.Account == null) return false;
            return _paymentEditor.SelectedTicket != null
                && !_paymentEditor.SelectedTicket.IsClosed
                && _tenderedValueViewModel.GetTenderedValue() != 0
                && _paymentEditor.GetRemainingAmount() != 0
                && (arg.Account != null || _paymentEditor.SelectedTicket.TicketEntities.Any(x =>
                    _ticketService.CanMakeAccountTransaction(x, arg.AccountTransactionType, _accountBalances.GetAccountBalance(x.AccountId) + _tenderedValueViewModel.GetTenderedValue())));
        }

        private void OnMakePayment(PaymentType obj)
        {
            if (obj.Name.Contains("Aucun"))   //Function modified by Tim GU, in order to add the handler for the "Aucun Paiement" case
                SubmitNoPayment();
            else
                SubmitPayment(obj);
        }

        private bool CanClosePaymentScreen(string arg)
        {
            return string.IsNullOrEmpty(_tenderedValueViewModel.TenderedAmount) || (_paymentEditor.SelectedTicket != null && _paymentEditor.GetRemainingAmount() == 0);
        }

        private void OnClosePaymentScreen(string obj)
        {
            _orderSelectorViewModel.PersistTicket();
            _paymentEditor.Close();
        }

        private void SubmitNoPayment()    //Function added by Tim GU. Handler of the "Aucun Paiement" payment type.
        {
            if(_paymentEditor.SelectedTicket.Payments.Count > 1)
            {
                MessageBox.Show("Ceci est interdit en cas de versements multiples!", "ATTENTION", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            if (_paymentEditor.SelectedTicket.IsRefund)
            {
                MessageBox.Show("Ceci est interdit en cas de remboursement!", "ATTENTION", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            if (MessageBox.Show("Veuillez confirmer la fermeture de ce ticket\ncomme 'Aucun paiement'.", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                SRSState.Clear();
                SRS_Submit_No_Payment(_paymentEditor.SelectedTicket);
                _ticketService.ForcedCloseTicket(_paymentEditor.SelectedTicket);
            }
        }

        private void SubmitPayment(PaymentType paymentType)
        {
            var paymentDueAmount = _tenderedValueViewModel.GetPaymentDueValue();
            var tenderedAmount = _tenderedValueViewModel.GetTenderedValue();

            if (Math.Abs(paymentDueAmount - _paymentEditor.GetRemainingAmount()) <= 0.01m)
                paymentDueAmount = _paymentEditor.GetRemainingAmount();

            if (tenderedAmount == 0 || Math.Abs(paymentDueAmount - tenderedAmount) <= 0.01m)
                tenderedAmount = paymentDueAmount;

            if (tenderedAmount <= paymentDueAmount)
            {
                SubmitPaymentAmount(paymentType, null, paymentDueAmount, tenderedAmount);
                return;
            }

            var changeTemplates = GetChangePaymentTypes(paymentType);
            if (changeTemplates.Count() < 2)
            {
                SubmitPaymentAmount(paymentType, changeTemplates.SingleOrDefault(), paymentDueAmount, tenderedAmount);
            }
            else
            {
                _changeTemplatesViewModel.Display(changeTemplates, tenderedAmount, paymentDueAmount, paymentType, _selectChangePaymentTypeCommand);
            }
        }

        private void OnSelectChangePaymentType(PaymentData paymentData)
        {
            SubmitPaymentAmount(paymentData.PaymentType, paymentData.ChangePaymentType,
                paymentData.PaymentDueAmount, paymentData.TenderedAmount);
        }

        private void SubmitPaymentAmount(PaymentType paymentType, ChangePaymentType changeTemplate, decimal paymentDueAmount, decimal tenderedAmount)
        {
            var returningAmount = _returningAmountViewModel.GetReturningAmount(tenderedAmount, paymentDueAmount, changeTemplate);

            var paidAmount = (changeTemplate == null) ? tenderedAmount - returningAmount : tenderedAmount;

            var paymentAmount = paymentDueAmount > paidAmount
                    ? paymentDueAmount - paidAmount
                    : _paymentEditor.GetRemainingAmount();

            _orderSelectorViewModel.UpdateSelectedTicketPaidItems();
            _paymentEditor.UpdateTicketPayment(paymentType, changeTemplate, paymentDueAmount, paidAmount, tenderedAmount);
            _numberPadViewModel.LastTenderedAmount = (paidAmount / _paymentEditor.ExchangeRate).ToString(LocalSettings.ReportCurrencyFormat);
            _tenderedValueViewModel.UpdatePaymentAmount(paymentAmount);

            SRSState.Clear();   //Added by Tim GU
            SRS_Submit_Payment(paymentType, paymentDueAmount, tenderedAmount);    //Added by Tim GU

            if (returningAmount == 0 && _paymentEditor.GetRemainingAmount() == 0)
            {
                OnClosePaymentScreen("");
            }
            else
            {
                if (returningAmount > 0)
                {
                    _returningAmountViewModel.PublishEvent(EventTopicNames.Activate);
                }
                if (paymentDueAmount <= paidAmount)
                    _orderSelectorViewModel.PersistSelectedItems();
                _numberPadViewModel.ResetValues();
                RaisePropertyChanged(() => SelectedTicketTitle);
            }
        }

        private IList<ChangePaymentType> GetChangePaymentTypes(PaymentType paymentType)
        {
            return _foreignCurrencyButtonsViewModel.ForeignCurrency == null
                ? new List<ChangePaymentType>()
                : _applicationState.GetChangePaymentTypes().Where(x => x.AccountTransactionType.TargetAccountTypeId == paymentType.AccountTransactionType.SourceAccountTypeId).ToList();
        }

        public void Prepare(Ticket selectedTicket)
        {
            _foreignCurrencyButtonsViewModel.Prepare();
            _paymentTotals.Model = selectedTicket;
            _paymentEditor.SelectedTicket = selectedTicket;
            _orderSelectorViewModel.UpdateTicket(selectedTicket);
            _numberPadViewModel.ResetValues();
            _numberPadViewModel.LastTenderedAmount = _tenderedValueViewModel.PaymentDueAmount;
            _numberPadViewModel.BalanceMode = false;
            _commandButtonsViewModel.Update();
            _foreignCurrencyButtonsViewModel.UpdateCurrencyButtons();

            RaisePropertyChanged(() => SelectedTicketTitle);
        }        

        private void SRS_Submit_No_Payment(Ticket ticket)   //Function added by Tim GU
        {
            //Transaction tran = new Transaction();
            //List<TransactionItem> lstItems = new List<TransactionItem>();

            //try
            //{
            //    tran.UserName = _applicationState.CurrentLoggedInUser.Name;
            //    tran.TransactionNumber = ticket.TicketNumber;
            //    ticket.SRSTransactionNo = tran.TransactionNumber;
            //    tran.sevRecord.SRSDeviceIdapprl = SRSState.SRSDeviceIdapprl;

            //    if (SelectedTicketTitle != null)
            //    {
            //        if (ticket.TicketEntities.Count > 0)
            //        {
            //            string strTemp = string.Empty;
            //            switch (ticket.TicketEntities[0].EntityTypeId)
            //            {
            //                case 1:
            //                    tran.ServiceType = Transaction.ServiceTypeList.COUNTER_SERVICE;
            //                    tran.client.NomClint = ticket.TicketEntities[0].EntityName;
            //                    try
            //                    {
            //                        string[] strTemp2 = ticket.TicketEntities[0].EntityCustomData.Split('"');
            //                        tran.client.Tel1 = strTemp2[7];
            //                    }
            //                    catch { };
            //                    break;
            //                case 2:
            //                    tran.ServiceType = Transaction.ServiceTypeList.TABLE_SERVICE;
            //                    strTemp = SelectedTicketTitle.Replace("#", "").Replace($"{tran.TransactionNumber}", "").Replace("Table:", "").Trim();
            //                    strTemp = strTemp.Replace("Table:", "").Trim();
            //                    tran.TableNumber = strTemp.Length > 0 ? strTemp : "";
            //                    break;
            //            }
            //        }
            //    }

            //    tran.NumberOfCustomer = ticket.GetCustomerCount();
                ticket.RemoveAllCalculations();

            //    for (int i = 0; i < ticket.Orders.Count; i++)
            //    {
            //        List<TransactionItemDetail> lstDetail = new List<TransactionItemDetail>();
            //        if (ticket.Orders[i].CalculatePrice)
            //        {
            //            if (ticket.Orders[i].OrderTags != null)
            //            {
            //                IList<OrderTagValue> OrderTagValues = JsonHelper.Deserialize<List<OrderTagValue>>(ticket.Orders[i].OrderTags);
            //                if (OrderTagValues.Count > 0)
            //                {
            //                    lstDetail.Clear();
            //                    for (int j = 0; j < OrderTagValues.Count; j++)
            //                    {
            //                        string strTax = OrderTagValues[j].Price == 0 ? Transaction.ApplicableTaxList.UNKNOWN : Transaction.ApplicableTaxList.GST_AND_QST;
            //                        string strActi = OrderTagValues[j].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //                        lstDetail.Add(new TransactionItemDetail(OrderTagValues[j].Quantity, OrderTagValues[j].TagValue, OrderTagValues[j].Price, OrderTagValues[j].Quantity * OrderTagValues[j].Price, strTax, strActi));
            //                    }
            //                }
            //            }
            //            string strACTI = ticket.Orders[i].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //            if (lstDetail.Count > 0)
            //                lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI, lstDetail));
            //            else
            //                lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI));
            //        }
            //    }
                
            //    tran.lstItems = lstItems;
            //    tran.TransactionTypeId = Transaction.TransactionTypeIDList.NO_BILL_PRODUCED_FAILED_TO_PAY;     //parti sans payer
            //    tran.CurrentInstalment = 0;
            //    tran.SumPaidInstalments = 0;
            //    tran.SplitPaymentBalanceToPay = 0;
            //    tran.AdjustedAmount = 0;

            //    tran.AmountBeforeTax = _paymentTotals.TicketSubTotalValue;
            //    tran.GSTAmount = _paymentTotals.TicketTPSValue;
            //    tran.QSTAmount = _paymentTotals.TicketTVQValue;
            //    tran.AmountAfterTax = tran.AmountBeforeTax + tran.GSTAmount + tran.QSTAmount;                
            //    tran.AmountDue = _tenderedValueViewModel.GetPaymentDueValue();
            //    tran.TipAmount = 0;
            //    tran.BusinessRelationship = Transaction.BusinessRelationshipList.B2C;
            //    tran.IsOnlineOrder = Transaction.OnlineOrderStatus.NOT_ONLINE_ORDER;
            //    tran.TransactionType = Transaction.TransactionTypeList.CLOSING_RECEIPT;

            //    if (ticket.SRSSubmitCount > 0)
            //    {
            //        int iSubNo = ticket.SRSSubNo;
            //        string strRefNo = iSubNo == 0 ? ticket.TicketNumber : $"{ticket.TicketNumber}-{ticket.SRSSubNo}";
            //        tran.AddRef(strRefNo);
            //        ticket.SRSSubNo++;
            //        ticket.SRSTransactionNo = $"{ticket.TicketNumber}-{ticket.SRSSubNo}";
            //        tran.TransactionNumber = ticket.SRSTransactionNo;
            //    }                

            //    tran.PaymentMethod = Transaction.PaymentMethodList.NO_PAYMENT;
                SRSState.PaymentMethod = SRSState.PaymentMethodLableList.NO_PAYMENT;             
            //    tran.PrintMode = Transaction.PrintModeList.FAILED_PAYMENT;
            //    tran.PrintOption = Transaction.PrintOptionList.NOT_PRINTED;
            //    tran.OperationMode = Transaction.OperationModeList.OPERATING;

            //    tran.sevRecord.PaymentMethod = SRSState.PaymentMethod;
                SRSState.PaymentStatus = "PAIEMENT NON REÇU";
            //    tran.sevRecord.PaymentStatus = SRSState.PaymentStatus;
            //    SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;

            //    ticket.SRSSubmitCount++;
            //    string strSRSResult = string.Empty;
            //    strSRSResult = tran.Submit();                
            //}
            //catch (Exception) { }
        }

        private string SRS_REVERSE_RFER_Submit(Ticket ticket)   //Function added by Tim GU
        {
            //Transaction tran = new Transaction();

            //return tran.Reverse(ticket.TicketNumber, ticket.SRSSubNo);
            return "#00#";
        }

        private void SRS_Submit_Payment(PaymentType paymentType, decimal paymentDueAmount, decimal tenderedAmount)    //Function added by Tim GU
        {
            //Transaction tran = new Transaction();
            //List<TransactionItem> lstItems = new List<TransactionItem>();

            //try
            //{
            //    tran.UserName = _applicationState.CurrentLoggedInUser.Name;
            //    tran.TransactionNumber = _paymentEditor.SelectedTicket.TicketNumber;
            //    //_paymentEditor.SelectedTicket.SRSTransactionNo = tran.TransactionNumber;
            //    tran.sevRecord.SRSDeviceIdapprl = SRSState.SRSDeviceIdapprl;

            //    if (SelectedTicketTitle != null)
            //    {
            //        if(_paymentEditor.SelectedTicket.TicketEntities.Count > 0)
            //        {
            //            string strTemp = string.Empty;
            //            switch (_paymentEditor.SelectedTicket.TicketEntities[0].EntityTypeId)
            //            {
            //                case 1:
            //                    tran.ServiceType = Transaction.ServiceTypeList.COUNTER_SERVICE;
            //                    tran.client.NomClint = _paymentEditor.SelectedTicket.TicketEntities[0].EntityName;
            //                    try
            //                    {
            //                        string[] strTemp2 = _paymentEditor.SelectedTicket.TicketEntities[0].EntityCustomData.Split('"');
            //                        tran.client.Tel1 = strTemp2[7];
            //                    }
            //                    catch { };
            //                    break;
            //                case 2:
            //                    tran.ServiceType = Transaction.ServiceTypeList.TABLE_SERVICE;
            //                    strTemp = SelectedTicketTitle.Replace("#", "").Replace($"{tran.TransactionNumber}", "").Replace("Table:", "").Trim();
            //                    strTemp = strTemp.Replace("Table:", "").Trim();
            //                    tran.TableNumber = strTemp.Length > 0 ? strTemp : "";
            //                    break;
            //            }
            //        }                
            //    }

            //    tran.NumberOfCustomer = _paymentEditor.SelectedTicket.GetCustomerCount();
            //    //bool bTotalAmountChanged = false;
            //    bool bSplitPaymentCausedSubAdd = false;
            //    bool bShouldAddSub = false;
            //    bool bIsDoingSplitPayment = false;

            //    for (int i = 0; i < _paymentEditor.SelectedTicket.Orders.Count; i++)
            //    {
            //        List<TransactionItemDetail> lstDetail = new List<TransactionItemDetail>();
            //        if (_paymentEditor.SelectedTicket.Orders[i].CalculatePrice)
            //        {
            //            if (_paymentEditor.SelectedTicket.Orders[i].OrderTags != null)
            //            {
            //                IList<OrderTagValue> OrderTagValues = JsonHelper.Deserialize<List<OrderTagValue>>(_paymentEditor.SelectedTicket.Orders[i].OrderTags);
            //                if (OrderTagValues.Count > 0)
            //                {
            //                    lstDetail.Clear();
            //                    for (int j = 0; j < OrderTagValues.Count; j++)
            //                    {
            //                        string strTax = OrderTagValues[j].Price == 0 ? Transaction.ApplicableTaxList.UNKNOWN : Transaction.ApplicableTaxList.GST_AND_QST;
            //                        string strActi = OrderTagValues[j].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //                        lstDetail.Add(new TransactionItemDetail(OrderTagValues[j].Quantity, OrderTagValues[j].TagValue, OrderTagValues[j].Price, OrderTagValues[j].Quantity * OrderTagValues[j].Price, strTax, strActi));
            //                    }
            //                }
            //            }
            //            decimal dUnitr = _paymentEditor.SelectedTicket.IsRefund ? -_paymentEditor.SelectedTicket.Orders[i].Price : _paymentEditor.SelectedTicket.Orders[i].Price;
            //            string strACTI = _paymentEditor.SelectedTicket.Orders[i].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //            if (lstDetail.Count > 0)
            //                lstItems.Add(new TransactionItem(_paymentEditor.SelectedTicket.Orders[i].Quantity, _paymentEditor.SelectedTicket.Orders[i].Description, dUnitr, _paymentEditor.SelectedTicket.Orders[i].Quantity * _paymentEditor.SelectedTicket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI, lstDetail));
            //            else
            //                lstItems.Add(new TransactionItem(_paymentEditor.SelectedTicket.Orders[i].Quantity, _paymentEditor.SelectedTicket.Orders[i].Description, dUnitr, _paymentEditor.SelectedTicket.Orders[i].Quantity * _paymentEditor.SelectedTicket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI));
            //        }
            //    }

            //    decimal tipAmount = 0;
            //    if (_paymentTotals.PreServices.Count > 0)
            //    {
            //        for (int i = 0; i < _paymentTotals.PreServices.Count; i++)
            //        {
            //            if (_paymentTotals.PreServices[i].Name.ToLower().Contains("rabais"))
            //            {
            //                lstItems.Add(new TransactionItem(1m, $"Rabais@{_paymentTotals.PreServices[i].Description}", _paymentTotals.PreServices[i].CalculationAmount, _paymentTotals.PreServices[i].CalculationAmount, Transaction.ApplicableTaxList.GST_AND_QST, Transaction.ActivitySubsectorList.RESTAURANT));
            //                //bTotalAmountChanged = true;
            //            }
            //            else
            //            {
            //                if (_paymentTotals.PreServices[i].Name.ToLower().Contains("pourboire"))
            //                {
            //                    tipAmount += _paymentTotals.PreServices[i].CalculationAmount;
            //                    //bTotalAmountChanged = true;
            //                }
            //            }
            //        }
            //    }
            //    tran.lstItems = lstItems;

            //    if (_paymentEditor.SelectedTicket.RemainingAmount == 0)
            //    {
            //        if (_paymentEditor.SelectedTicket.Payments.Count == 1)
            //        {
            //            tran.TransactionTypeId = Transaction.TransactionTypeIDList.CLOSING_RECEIPT_WITH_ONE_PAYMENT;    //no split payment
            //            tran.CurrentInstalment = 0;
            //            tran.SumPaidInstalments = 0;
            //            tran.SplitPaymentBalanceToPay = 0;
            //        }
            //        else
            //        {
            //            tran.TransactionTypeId = Transaction.TransactionTypeIDList.CLOSING_RECEIPT_NOT_FIRST_SPLIT_PAYMENT;   //the last instalment of the split payment
            //            tran.CurrentInstalment = paymentDueAmount; //tenderedAmount;
            //            decimal paidSum = 0;
            //            if (_paymentTotals.Payments.Count > 0)
            //            {
            //                for (int i = 0; i < _paymentTotals.Payments.Count; i++)
            //                {
            //                    paidSum += _paymentTotals.Payments[i].Amount;
            //                }
            //            }
            //            tran.SumPaidInstalments = paidSum;
            //            tran.SplitPaymentBalanceToPay = 0;
            //            bIsDoingSplitPayment = true;                        
            //        }
            //    }
            //    else
            //    {
            //        if (_paymentEditor.SelectedTicket.Payments.Count == 1)
            //        {
            //            tran.TransactionTypeId = Transaction.TransactionTypeIDList.CLOSING_RECEIPT_FIRST_SPLIT_PAYMENT;    //the first instalment of the split payment
            //            tran.CurrentInstalment = tenderedAmount;
            //            tran.SumPaidInstalments = 0;
            //            tran.SplitPaymentBalanceToPay = _paymentEditor.SelectedTicket.RemainingAmount;
            //            bIsDoingSplitPayment = true;
            //        }
            //        else
            //        {
            //            tran.TransactionTypeId = Transaction.TransactionTypeIDList.CLOSING_RECEIPT_NOT_FIRST_SPLIT_PAYMENT;   //neither the first nor the last instalment of the split payment
            //            tran.CurrentInstalment = tenderedAmount;
            //            decimal paidSum = 0;
            //            if(_paymentTotals.Payments.Count > 0)
            //            {
            //                for(int i = 0; i < _paymentTotals.Payments.Count; i++)
            //                {
            //                    paidSum += _paymentTotals.Payments[i].Amount;
            //                }
            //            }
            //            tran.SumPaidInstalments = paidSum;
            //            tran.SplitPaymentBalanceToPay = _paymentEditor.SelectedTicket.RemainingAmount;
            //            bSplitPaymentCausedSubAdd = true;
            //            bIsDoingSplitPayment = true;
            //        }
            //    }

            //    decimal adAmount = 0;
            //    if (_paymentTotals.PostServices.Count > 0)
            //    {
            //        for (int i = 0; i < _paymentTotals.PostServices.Count; i++)
            //        {
            //            if (_paymentTotals.PostServices[i].Name.ToLower().Contains("bon de"))
            //            {
            //                adAmount += _paymentTotals.PostServices[i].CalculationAmount;
            //                //bTotalAmountChanged = true;
            //            }                            
            //            else
            //            {
            //                if (_paymentTotals.PostServices[i].Name.ToLower().Contains("pourboire"))
            //                {
            //                    tipAmount += _paymentTotals.PostServices[i].CalculationAmount;
            //                    //bTotalAmountChanged = true;
            //                }
            //            }                          
            //        }
            //    }
            //    tran.AdjustedAmount = adAmount;

            //    tran.AmountBeforeTax = _paymentTotals.TicketSubTotalValue;
            //    tran.GSTAmount = _paymentTotals.TicketTPSValue;
            //    tran.QSTAmount = _paymentTotals.TicketTVQValue;
            //    tran.AmountAfterTax = tran.AmountBeforeTax + tran.GSTAmount + tran.QSTAmount;                
            //    tran.AmountDue = paymentDueAmount;
            //    tran.TipAmount = tipAmount;
            //    tran.BusinessRelationship = Transaction.BusinessRelationshipList.B2C;
            //    tran.IsOnlineOrder = Transaction.OnlineOrderStatus.NOT_ONLINE_ORDER;
            //    tran.TransactionType = Transaction.TransactionTypeList.CLOSING_RECEIPT;

            //    if(_paymentEditor.SelectedTicket.SRSSubmitCount == 0)    //Closing receipt without any temporary bill before the payment
            //    {
            //        bShouldAddSub = bSplitPaymentCausedSubAdd;
            //    }
            //    else
            //    {
            //        bShouldAddSub = true;   //= (bTotalAmountChanged && !bSplitPaymentCausedSubAdd) || bSplitPaymentCausedSubAdd;
            //    }

            //    if (bShouldAddSub)
            //    {
            //        int iSubNo = _paymentEditor.SelectedTicket.SRSSubNo;
            //        string strRefNo = iSubNo == 0 ? _paymentEditor.SelectedTicket.TicketNumber : $"{_paymentEditor.SelectedTicket.TicketNumber}-{_paymentEditor.SelectedTicket.SRSSubNo}";
            //        tran.AddRef(strRefNo);
            //        _paymentEditor.SelectedTicket.SRSSubNo++;
            //        if(!bIsDoingSplitPayment)
            //            _paymentEditor.SelectedTicket.SRSTransactionNo = $"{_paymentEditor.SelectedTicket.TicketNumber}-{_paymentEditor.SelectedTicket.SRSSubNo}";
            //        tran.TransactionNumber = _paymentEditor.SelectedTicket.SRSTransactionNo;
            //    }
            //    //else
            //    //{
            //        if (!string.IsNullOrEmpty(_paymentEditor.SelectedTicket.RefSRSTransactionNo))
            //        {
            //            if (_paymentEditor.SelectedTicket.RefSRSTransactionNo.Contains(","))
            //            {
            //                var refs = _paymentEditor.SelectedTicket.RefSRSTransactionNo.Split(',');
            //                foreach (var strRef in refs)
            //                {
            //                    if (strRef != string.Empty)
            //                        tran.AddRef(strRef);
            //                }
            //            }
            //            else
            //                tran.AddRef(_paymentEditor.SelectedTicket.RefSRSTransactionNo);
            //        }
            //    //}

            switch (paymentType.Id)
            {
                case 1:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.CASH;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.CASH_PAYMENT;
                    break;
                case 2:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.CREDIT_CARD;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.CREDIT_CARD;
                    break;
                case 3:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.COUPON;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.COUPON;
                    break;
                case 4:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.CHARGE_TO_ACCOUNT;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.CHARGE_TO_ACCOUNT;
                    break;
                case 5:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.DEBIT_CARD;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.DEBIT_CARD;
                    break;
                case 6:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.NO_PAYMENT;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.NO_PAYMENT;
                    break;
                case 7:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.CHECK;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.CHECK;
                    break;
                case 8:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.PREPAID_OR_GIFT_CARD;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.GIFT_CARD;
                    break;
                default:
                    //tran.PaymentMethod = Transaction.PaymentMethodList.OTHER_PAYMENT_METHOD;
                    SRSState.PaymentMethod = SRSState.PaymentMethodLableList.OTHER_PAYMENT_METHOD;
                    break;
            }

            //    tran.PrintMode = Transaction.PrintModeList.BILL_OR_DOCUMENT_NOT_INCLUDED;
            //    tran.PrintOption = Transaction.PrintOptionList.PAPER;
            //    tran.OperationMode = Transaction.OperationModeList.OPERATING;

            //    tran.sevRecord.PaymentMethod = SRSState.PaymentMethod;
            //    SRSState.PaymentStatus = paymentType.Id == 4 ? "PORTÉ AU COMPTE" : "PAIEMENT REÇU";
            //    if (tenderedAmount < 0)
            //        SRSState.PaymentStatus = "NOTE DE CRÉDIT";
            //    tran.sevRecord.PaymentStatus = SRSState.PaymentStatus;
            //    SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;

            //    _paymentEditor.SelectedTicket.SRSSubmitCount++;
            //    string strSRSResult = string.Empty;
            //    strSRSResult = tran.Submit();

            //    if (strSRSResult.Contains("#00#"))
            //    {
            //        SRSState.PSIQRString = tran.RQ_QRCode_String;
            //        SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
            //        DateTime dt;
            //        if (DateTime.TryParseExact(tran.RQ_PSI_DAT_TRANS, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //            SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //        else
            //            SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //    }
            //    else
            //    {
            //        if (strSRSResult != string.Empty)
            //        {
            //            var strTmp = strSRSResult.Split('#');
            //            if (!string.IsNullOrEmpty(strTmp[2]))
            //            {
            //                SRSState.PSIQRString = tran.RQ_QRCode_String;
            //                SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
            //                SRSState.PSITransactionDate = tran.RQ_PSI_DAT_TRANS;
            //            }
            //            else
            //                MessageBox.Show(strSRSResult, "SRS Result");
            //        }
            //    }
            //}
            //catch (Exception) { }            
        }
    }
}