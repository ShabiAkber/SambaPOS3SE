using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Documents;
using System.Windows.Threading;
//using SRSSDK;
using Samba.Domain.Models.Settings;
using Samba.Domain.Models.Tickets;
using Samba.Infrastructure.Settings;
using Samba.Localization.Properties;
using Samba.Services.Common;
using Samba.Services.Implementations.PrinterModule.Formatters;
using Samba.Services.Implementations.PrinterModule.PrintJobs;
using Samba.Services.Implementations.PrinterModule.Tools;
using Samba.Services.Implementations.PrinterModule.ValueChangers;
using System.Drawing.Printing;
using Samba.Infrastructure.Helpers;
//using Microsoft.Win32;

namespace Samba.Services.Implementations.PrinterModule
{
    [Export(typeof(IPrinterService))]
    public class PrinterService : IPrinterService
    {
        private readonly ICacheService _cacheService;
        private readonly ILogService _logService;
        private readonly TicketFormatter _ticketFormatter;
        private readonly FunctionRegistry _functionRegistry;
        private readonly TicketPrintTaskBuilder _ticketPrintTaskBuilder;

        [ImportingConstructor]
        PrinterService(ISettingService settingService, ICacheService cacheService, IExpressionService expressionService, ILogService logService,
            TicketFormatter ticketFormatter, FunctionRegistry functionRegistry, TicketPrintTaskBuilder ticketPrintTaskBuilder)
        {
            _cacheService = cacheService;
            _logService = logService;
            _ticketFormatter = ticketFormatter;
            _functionRegistry = functionRegistry;
            _ticketPrintTaskBuilder = ticketPrintTaskBuilder;
            _functionRegistry.RegisterFunctions();
        }

        [ImportMany]
        public IEnumerable<IDocumentFormatter> DocumentFormatters { get; set; }

        [ImportMany]
        public IEnumerable<ICustomPrinter> CustomPrinters { get; set; }

        public IEnumerable<string> GetPrinterNames()
        {
            return PrinterInfo.GetPrinterNames();
        }

        public IEnumerable<string> GetCustomPrinterNames()
        {
            return CustomPrinters.Select(x => x.Name);
        }

        public ICustomPrinter GetCustomPrinter(string customPrinterName)
        {
            return CustomPrinters.FirstOrDefault(x => x.Name == customPrinterName);
        }

        public void PrintTicket(Ticket ticket, PrintJob printJob, Func<Order, bool> orderSelector, bool highPriority)
        {
            #region PrintTicket_SRS_Related
            switch (printJob.Id)
            {
                case 1:  //Print Temporary Bill, original or revised
                    if (ticket.GetPlainSum() == 0)
                        return;
                    bool bShouldSubmit = false;
                    if (ticket.Orders.Count > 0)
                    {
                        for (int i = 0; i < ticket.Orders.Count; i++)
                        {
                            if (ticket.Orders[i].OrderStates.Contains("New"))
                            {
                                bShouldSubmit = true;
                                break;
                            }
                        }
                        if (ticket.SRSSubmitCount == 0 && !string.IsNullOrEmpty(ticket.RefSRSTransactionNo))
                            bShouldSubmit = true;
                    }
                    if (bShouldSubmit)
                    {
                        string formImpr = "PAP"; //Transaction.PrintOptionList.PAPER;
                        SRS_ADDI_Submit(ticket, printJob, formImpr);
                    }                    
                    else
                    {
                        //int iTranTypeId = ticket.SRSSubmitCount == 0 ? Transaction.TransactionTypeIDList.ORIGINAL_TEMPORARY_BILL : Transaction.TransactionTypeIDList.REVISED_TEMPORARY_BILL;
                        //SEVRecord sEVRecord = new SEVRecord(ticket.TicketNumber, ticket.GetPlainSum(), ticket.GetSum(), iTranTypeId);
                        //if (sEVRecord.Id != 0)
                        //{
                        //    SRSState.Clear();
                        //    SRSState.PaymentMethod = sEVRecord.PaymentMethod;
                        //    SRSState.PaymentStatus = sEVRecord.PaymentStatus;
                        //    SRSState.AdditionalInfo = sEVRecord.AdditionalInfo;
                        //    SRSState.PrintCopyType = sEVRecord.PrintCopyType;
                        //    SRSState.PSIQRString = sEVRecord.QRCodeString;
                        //    SRSState.PSITransactionNumber = sEVRecord.PSITransactionNo;
                        //    DateTime dt;
                        //    if (DateTime.TryParseExact(sEVRecord.PSITransactionDate, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                        //        SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        //    else
                        //        SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        //}
                        //SRS_ADDI_RPR_Submit(ticket);
                    }
                    break;
                case 2:
                    if (ticket.ShouldNotPrint)
                        return;
                    break;
                case 3:  //Print Closing Receipt
                    if (ticket.ShouldNotPrint)
                        return;
                    if (ticket.Calculations.Count() == 1)
                    {
                        if (ticket.Calculations[0].Name.Contains("Annulation"))
                            return;
                    }
                    if (ticket.GetPlainSum() == 0)
                    {
                        if (ticket.Calculations.Count == 0)
                            SRS_ANN_SOB_NON_Submit(ticket);
                        return;
                    }
                    if (ticket.Payments.Count() > 1)
                        return;
                    if (ticket.SRSTransactionNo == null)
                        ticket.SRSTransactionNo = ticket.TicketNumber;
                    break;
                case 4:  //Reprint Customer Copy
                case 5:  //Print Merchant Copy
                    if (ticket.GetPlainSum() == 0 && ticket.Calculations.Count == 0)  //[modPai:SOB modImpr:ANN formImpr:NON] Canceled ticket, zero beforetax amount, all orders have been voided, should print NOTHING
                        return;
                    SRSState.Clear();
                    SRS_RFER_Submit(ticket, printJob);
                    break;
                case 6:  //Print Abort Ticket
                    SRSState.Clear();
                    if (ticket.IsRFER && ticket.GetPlainSum() == 0)
                    {
                        if (ticket.Calculations.Count == 0)
                        {
                            ticket.ShouldNotPrint = true;
                            SRS_ANN_SOB_NON_Submit(ticket);
                        }                            
                        return;
                    }
                    var ticketState = ticket.GetStateData(x => true);
                    if (ticketState.Contains("New Orders"))
                    {
                        ticket.ShouldNotPrint = true;
                        SRS_ADDI_SOB_Submit(ticket);
                        return;
                    }
                    else
                        SRS_RFER_AUC_Submit(ticket);                     
                    break;
                case 7:
                    if (ticket.ShouldNotPrint)
                        return;
                    break;
                case 8:  //Split Payment printing closing receipts
                    if (ticket.Payments.Count == 1 && ticket.RemainingAmount == 0)
                        return;
                    Instalment.Clear();
                    int iCount = ticket.Payments.Count;
                    if (iCount > 0)
                    {
                        Instalment.VersActu = ticket.Payments[iCount - 1].VersActu;
                        Instalment.VersAnt = ticket.Payments[iCount-1].VersAnt;
                        Instalment.Sold = ticket.Payments[iCount - 1].Sold;
                    }
                    if (ticket.SRSTransactionNo == null)
                        ticket.SRSTransactionNo = ticket.TicketNumber;
                    else
                    {
                        int i = ticket.SRSSubNo;
                        i++;
                        ticket.SRSTransactionNo = $"{ticket.TicketNumber}-{i}";
                    }
                    break;
                case 9:  //Transmit data to RQ w/o printing bills
                    SRS_ADDI_Submit(ticket, printJob, "NON"); // Transaction.PrintOptionList.NOT_PRINTED);
                    return;
                case 11: //Reopen Ticket w/o printing bills
                    if (MessageBox.Show("Veuillez confirmer la réouverture de ce ticket déjà payé\npour corriger le billet, ou changer le mode de paiement.", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        string strReverseResult = SRS_REVERSE_RFER_Submit(ticket);
                        if (strReverseResult.Contains("#00#"))
                        {
                            int iSubNo = ticket.SRSSubNo;
                            string[] strParam = strReverseResult.Split('#');
                            if (strParam.Length > 3)
                            {
                                int.TryParse(strParam[2], out iSubNo);
                                if (iSubNo > ticket.SRSSubNo)
                                {
                                    ticket.SRSSubNo = iSubNo;
                                    ticket.SRSSubmitCount = iSubNo + 1;
                                    ticket.SRSTransactionNo = $"{ticket.TicketNumber}-{iSubNo}";
                                    ticket.Reopen();
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Désolé, cela ne peut pas être fait pour le moment,\nveuillez réessayer plus tard.");
                        }
                    }
                    return;
                case 12: //Print Temporary Bill Merchant Copy (duplication)
                    if (ticket.GetPlainSum() == 0 && ticket.Calculations.Count == 0)  //[modPai:SOB modImpr:ANN formImpr:NON] Canceled ticket, zero beforetax amount, all orders have been voided, should print NOTHING
                        return;
                    SRSState.Clear();
                    SRS_ADDI_DUP_Submit(ticket);
                    break;
            }
            #endregion  //Region added by Tim GU

            TicketPrinter.For(ticket)
                .WithPrinterService(this)
                .WithLogService(_logService)
                .WithTaskBuilder(_ticketPrintTaskBuilder)
                .WithPrintJob(printJob)
                .WithOrderSelector(orderSelector)
                .IsHighPriority(highPriority)
                .Print();
        }

        public void PrintObject(object item, Printer printer, PrinterTemplate printerTemplate)
        {
            var formatter = DocumentFormatters.FirstOrDefault(x => x.ObjectType == item.GetType());
            if (formatter != null)
            {
                var lines = formatter.GetFormattedDocument(item, printerTemplate);
                if (lines != null)
                {
                    AsyncPrintTask.Exec(false, () => PrintJobFactory.CreatePrintJob(printer, this).DoPrint(lines), _logService);
                }
            }
        }

        public void PrintReport(FlowDocument document, Printer printer)
        {
            ReportPrinter.For(document)
                .WithPrinterService(this)
                .WithLogService(_logService)
                .WithPrinter(printer)
                .Print();
        }

        public void ExecutePrintJob(PrintJob printJob, bool highPriority)
        {
            PrintJobExecutor.For(printJob)
                .WithPrinterService(this)
                .WithLogSerivce(_logService)
                .WithCacheService(_cacheService)
                .IsHighPriority(highPriority)
                .Execute();
        }

        public IDictionary<string, string> GetTagDescriptions()
        {
            return _functionRegistry.Descriptions;
        }

        public void ResetCache()
        {
            PrinterInfo.ResetCache();
        }

        public string GetPrintingContent(Ticket ticket, string format, int width)
        {
            var lines = _ticketFormatter.GetFormattedTicket(ticket, ticket.Orders, new PrinterTemplate { Template = format });
            var result = new FormattedDocument(lines, width).GetFormattedText();
            return result;
        }

        public string ExecuteFunctions<T>(string printerTemplate, T model)
        {
            return _functionRegistry.ExecuteFunctions(printerTemplate, model, new PrinterTemplate { Template = printerTemplate });
        }

        public object GetCustomPrinterData(string customPrinterName, string customPrinterData)
        {
            var printer = GetCustomPrinter(customPrinterName);
            return printer != null ? printer.GetSettingsObject(customPrinterData) : "";
        }

        #region PrinterService_SRS_Related_New_Functions
        private void SRS_ANN_SOB_NON_Submit(Ticket ticket)    //By Tim GU. SRS Transaction Submission for Cancelled Tickets (either temporary bills, or unpaid closing receipt, by closing tickets with zero orders or all orders voided)
        {
            //string strUserName = string.Empty;
            //try
            //{
            //    using (RegistryKey rke = Registry.CurrentUser.CreateSubKey(@"Software\Allagma\SRS\State"))
            //    {
            //        string rkeV = rke.GetValue("Logged_In_USER", "").ToString();
            //        strUserName = rkeV;
            //        rke.Close();
            //    }
            //}
            //catch (Exception)
            //{
            //}

            //Transaction tran = new Transaction();
            //List<TransactionItem> lstItems = new List<TransactionItem>();

            //try
            //{
            //    tran.TransactionNumber = ticket.TicketNumber;
            //    tran.UserName = strUserName;
            //    tran.sevRecord.SRSDeviceIdapprl = SRSState.SRSDeviceIdapprl;

            //    if (ticket.TicketEntities.Count > 0)
            //    {
            //        if (ticket.TicketEntities[0].EntityTypeId == 2)
            //        {
            //            tran.ServiceType = Transaction.ServiceTypeList.TABLE_SERVICE;
            //            tran.TableNumber = ticket.TicketEntities[0].EntityName;
            //        }
            //        else
            //        {
            //            tran.ServiceType = Transaction.ServiceTypeList.COUNTER_SERVICE;
            //            tran.client.NomClint = ticket.TicketEntities[0].EntityName;
            //            try
            //            {
            //                string[] strTemp = ticket.TicketEntities[0].EntityCustomData.Split('"');
            //                tran.client.Tel1 = strTemp[7];
            //            }
            //            catch { };
            //        }
            //    }

            //    tran.NumberOfCustomer = ticket.GetCustomerCount();
            //    lstItems.Add(new TransactionItem(1m, "SOB", 0, 0, Transaction.ApplicableTaxList.UNKNOWN, Transaction.ActivitySubsectorList.UNKNOWN));
            //    tran.lstItems = lstItems;

            //    tran.TransactionTypeId = Transaction.TransactionTypeIDList.NO_BILL_PRODUCED_CANCELLED_TRANSACTION;
            //    tran.sevRecord.PaymentStatus = "FACTURE ANNULÉE";
            //    tran.sevRecord.PaymentMethod = "AUCUN PAIEMENT";

            //    tran.AmountBeforeTax = 0;
            //    tran.sevRecord.AmountBeforeTax = 0;
            //    tran.GSTAmount = 0;
            //    tran.QSTAmount = 0;
            //    tran.AmountAfterTax = 0;
            //    tran.sevRecord.AmountAfterTax = 0;
            //    tran.TipAmount = 0;
            //    tran.BusinessRelationship = Transaction.BusinessRelationshipList.B2C;
            //    tran.IsOnlineOrder = Transaction.OnlineOrderStatus.NOT_ONLINE_ORDER;
            //    tran.TransactionType = ticket.IsRFER ? Transaction.TransactionTypeList.CLOSING_RECEIPT : Transaction.TransactionTypeList.UNKNOWN;
            //    tran.PaymentMethod = ticket.IsRFER ? Transaction.PaymentMethodList.NO_PAYMENT : Transaction.PaymentMethodList.NA;
            //    tran.PrintMode = Transaction.PrintModeList.CANCELLATION;
            //    tran.PrintOption = Transaction.PrintOptionList.NOT_PRINTED;
            //    tran.OperationMode = Transaction.OperationModeList.OPERATING;

            //    string strSRSResult = string.Empty;
            //    ticket.SRSTransactionNo = ticket.TicketNumber;
            //    ticket.SRSSubmitCount++;
            //    strSRSResult = tran.Submit();                
            //}
            //catch (Exception) { }
        }

        private void SRS_RFER_AUC_Submit(Ticket ticket)   //By Tim GU. to cancel submitted but unpaid bills (by clicking the 'Annuler Billet' button)
        {
            //Transaction tran = new Transaction();
            //List<TransactionItem> lstItems = new List<TransactionItem>();

            //try
            //{
            //    //tran.sevRecord = new SEVRecord(ticket.TicketNumber);
            //    tran.TransactionNumber = ticket.TicketNumber;
            //    tran.UserName = ticket.LastModifiedUserName;
            //    tran.sevRecord.SRSDeviceIdapprl = SRSState.SRSDeviceIdapprl;

            //    if (ticket.TicketEntities.Count > 0)
            //    {
            //        if (ticket.TicketEntities[0].EntityTypeId == 2)
            //        {
            //            tran.ServiceType = Transaction.ServiceTypeList.TABLE_SERVICE;
            //            tran.TableNumber = ticket.TicketEntities[0].EntityName;
            //        }
            //        else
            //        {
            //            tran.ServiceType = Transaction.ServiceTypeList.COUNTER_SERVICE;
            //            tran.client.NomClint = ticket.TicketEntities[0].EntityName;
            //            try
            //            {
            //                string[] strTemp = ticket.TicketEntities[0].EntityCustomData.Split('"');
            //                tran.client.Tel1 = strTemp[7];
            //            }
            //            catch { };
            //        }
            //    }

            //    tran.NumberOfCustomer = ticket.GetCustomerCount();

            //    if (ticket.Orders.Count > 0)
            //    {
            //        for (int i = 0; i < ticket.Orders.Count; i++)
            //        {
            //            List<TransactionItemDetail> lstPreci = new List<TransactionItemDetail>();
            //            if (ticket.Orders[i].CalculatePrice)
            //            {
            //                if (ticket.Orders[i].OrderTags != null)
            //                {
            //                    IList<OrderTagValue> OrderTagValues = JsonHelper.Deserialize<List<OrderTagValue>>(ticket.Orders[i].OrderTags);
            //                    if (OrderTagValues.Count > 0)
            //                    {
            //                        lstPreci.Clear();
            //                        for (int j = 0; j < OrderTagValues.Count; j++)
            //                        {
            //                            string strTax = OrderTagValues[j].Price == 0 ? Transaction.ApplicableTaxList.UNKNOWN : Transaction.ApplicableTaxList.GST_AND_QST;
            //                            string strActi = OrderTagValues[j].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //                            lstPreci.Add(new TransactionItemDetail(OrderTagValues[j].Quantity, OrderTagValues[j].TagValue, OrderTagValues[j].Price, OrderTagValues[j].Quantity * OrderTagValues[j].Price, strTax, strActi));
            //                        }
            //                    }
            //                }
            //                string strACTI = ticket.Orders[i].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //                if (lstPreci.Count > 0)
            //                    lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI, lstPreci));
            //                else
            //                    lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI));
            //            }                            
            //        }
            //        if(ticket.Calculations.Count > 0)
            //        {
            //            List<TransactionItemDetail> lstPreci = new List<TransactionItemDetail>();
            //            for (int i = 0; i < ticket.Calculations.Count; i++)
            //            {
            //                if (ticket.Calculations[i].Name == "Annulation")
            //                {   
            //                    lstPreci.Clear();
            //                    lstPreci.Add(new TransactionItemDetail(0, "commande annulée",0,0, Transaction.ApplicableTaxList.GST_AND_QST, Transaction.ActivitySubsectorList.UNKNOWN));
            //                    lstItems.Add(new TransactionItem(1m, "Annulation", ticket.Calculations[i].CalculationAmount, ticket.Calculations[i].CalculationAmount, Transaction.ApplicableTaxList.GST_AND_QST, Transaction.ActivitySubsectorList.RESTAURANT,lstPreci));
            //                }
            //            }
            //        }

            //        tran.lstItems = lstItems;
            //        SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;

            //        int iSubNo = ticket.SRSSubNo;
            //        string strRefNo = iSubNo == 0 ? ticket.TicketNumber : $"{ticket.TicketNumber}-{ticket.SRSSubNo}";
            //        tran.AddRef(strRefNo);
                    //ticket.SRSSubNo++;
                    //ticket.SRSTransactionNo = $"{ticket.TicketNumber}-{ticket.SRSSubNo}";
                    //tran.TransactionNumber = ticket.SRSTransactionNo;
                    //tran.TransactionTypeId = Transaction.TransactionTypeIDList.CANCELLED_BILL;
                    SRSState.PaymentStatus = "FACTURE ANNULÉE";
                    //tran.sevRecord.PaymentStatus = SRSState.PaymentStatus;
                    SRSState.PaymentMethod = "AUCUN PAIEMENT";
                    //tran.sevRecord.PaymentMethod = SRSState.PaymentMethod;

                    //tran.AmountBeforeTax = 0;
                    //tran.sevRecord.AmountBeforeTax = 0;
                    //tran.GSTAmount = 0;
                    //tran.QSTAmount = 0;
                    //tran.AmountAfterTax  = 0;
                    //tran.sevRecord.AmountAfterTax = 0;
                    //tran.TipAmount = 0;
                    //tran.BusinessRelationship = Transaction.BusinessRelationshipList.B2C;
                    //tran.IsOnlineOrder = Transaction.OnlineOrderStatus.NOT_ONLINE_ORDER;
                    //tran.TransactionType = Transaction.TransactionTypeList.CLOSING_RECEIPT;
                    //tran.PaymentMethod = Transaction.PaymentMethodList.NO_PAYMENT;
                    //tran.PrintMode = Transaction.PrintModeList.BILL_OR_DOCUMENT_NOT_INCLUDED;
                    //tran.PrintOption = Transaction.PrintOptionList.PAPER;
                    //tran.OperationMode = Transaction.OperationModeList.OPERATING;

                    //string strSRSResult = string.Empty;
                    //ticket.SRSTransactionNo = tran.TransactionNumber;
                    //ticket.SRSSubmitCount++;
                    //strSRSResult = tran.Submit();
                    //if (strSRSResult.Contains("#00#"))
                    //{
                    //    SRSState.PSIQRString = tran.RQ_QRCode_String;
                    //    SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
                    //    DateTime dt;
                    //    if (DateTime.TryParseExact(tran.RQ_PSI_DAT_TRANS, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                    //        SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
                    //    else
                    //        SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                //    }
                //    else
                //    {
                //        if (strSRSResult != string.Empty)
                //        {
                //            var strTmp = strSRSResult.Split('#');
                //            if (strTmp.Length > 2 && !string.IsNullOrEmpty(strTmp[3]))
                //            {
                //                SRSState.PSIQRString = tran.RQ_QRCode_String;
                //                SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
                //                SRSState.PSITransactionDate = tran.RQ_PSI_DAT_TRANS;
                //            }
                //            else
                //                MessageBox.Show(strSRSResult.Replace("^", "\n"), "SRS message d'erreur");
                //        }
                //    }
                //}
            //}
            //catch (Exception) { }
        }

        private void SRS_ADDI_SOB_Submit(Ticket ticket)   //By Tim GU. to cancel unsubmitted temporary bills with ordered items (by clicking the 'Annuler Billet' button)
        {
            //Transaction tran = new Transaction();
            //List<TransactionItem> lstItems = new List<TransactionItem>();

            //try
            //{
            //    tran.TransactionNumber = ticket.TicketNumber;
            //    tran.UserName = ticket.LastModifiedUserName;
            //    tran.sevRecord.SRSDeviceIdapprl = SRSState.SRSDeviceIdapprl;

            //    if (ticket.TicketEntities.Count > 0)
            //    {
            //        if (ticket.TicketEntities[0].EntityTypeId == 2)
            //        {
            //            tran.ServiceType = Transaction.ServiceTypeList.TABLE_SERVICE;
            //            tran.TableNumber = ticket.TicketEntities[0].EntityName;
            //        }
            //        else
            //        {
            //            tran.ServiceType = Transaction.ServiceTypeList.COUNTER_SERVICE;
            //            tran.client.NomClint = ticket.TicketEntities[0].EntityName;
            //            try
            //            {
            //                string[] strTemp = ticket.TicketEntities[0].EntityCustomData.Split('"');
            //                tran.client.Tel1 = strTemp[7];
            //            }
            //            catch { };
            //        }
            //    }

            //    tran.NumberOfCustomer = ticket.GetCustomerCount();

            //    if (ticket.Orders.Count > 0)
            //    {
            //        for (int i = 0; i < ticket.Orders.Count; i++)
            //        {
            //            List<TransactionItemDetail> lstPreci = new List<TransactionItemDetail>();
            //            if (ticket.Orders[i].CalculatePrice)
            //            {
            //                if (ticket.Orders[i].OrderTags != null)
            //                {
            //                    IList<OrderTagValue> OrderTagValues = JsonHelper.Deserialize<List<OrderTagValue>>(ticket.Orders[i].OrderTags);
            //                    if (OrderTagValues.Count > 0)
            //                    {
            //                        lstPreci.Clear();
            //                        for (int j = 0; j < OrderTagValues.Count; j++)
            //                        {
            //                            string strTax = OrderTagValues[j].Price == 0 ? Transaction.ApplicableTaxList.UNKNOWN : Transaction.ApplicableTaxList.GST_AND_QST;
            //                            string strActi = OrderTagValues[j].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //                            lstPreci.Add(new TransactionItemDetail(OrderTagValues[j].Quantity, OrderTagValues[j].TagValue, OrderTagValues[j].Price, OrderTagValues[j].Quantity * OrderTagValues[j].Price, strTax, strActi));
            //                        }
            //                    }
            //                }
            //                string strACTI = ticket.Orders[i].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
            //                if (lstPreci.Count > 0)
            //                    lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI, lstPreci));
            //                else
            //                    lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI));
            //            }
            //        }                    

            //        tran.lstItems = lstItems;
            //        SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;

                    ticket.RemoveAllCalculations();

            //        int iSubNo = ticket.SRSSubNo;
            //        ticket.SRSSubNo++;
            //        ticket.SRSTransactionNo = $"{ticket.TicketNumber}";
            //        tran.TransactionNumber = ticket.SRSTransactionNo;
            //        tran.TransactionTypeId = Transaction.TransactionTypeIDList.NO_BILL_PRODUCED_CANCELLED_TRANSACTION;
                    SRSState.PaymentStatus = "FACTURE ANNULÉE";
            //        tran.sevRecord.PaymentStatus = SRSState.PaymentStatus;
                    SRSState.PaymentMethod = "AUCUN PAIEMENT";
            //        tran.sevRecord.PaymentMethod = SRSState.PaymentMethod;

            //        tran.AmountBeforeTax = ticket.GetPlainSum();
            //        tran.sevRecord.AmountBeforeTax = tran.AmountBeforeTax;
            //        tran.GSTAmount = ticket.GetTPSTotal();
            //        tran.QSTAmount = ticket.GetTVQTotal();
            //        tran.AmountAfterTax = ticket.GetSum();
            //        tran.sevRecord.AmountAfterTax = tran.AmountAfterTax;
            //        tran.TipAmount = 0;
            //        tran.BusinessRelationship = Transaction.BusinessRelationshipList.B2C;
            //        tran.IsOnlineOrder = Transaction.OnlineOrderStatus.NOT_ONLINE_ORDER;
            //        tran.TransactionType = Transaction.TransactionTypeList.TEMPORARY_BILL;
            //        tran.PaymentMethod = Transaction.PaymentMethodList.NA;
            //        tran.PrintMode = Transaction.PrintModeList.CANCELLATION;
            //        tran.PrintOption = Transaction.PrintOptionList.NOT_PRINTED;
            //        tran.OperationMode = Transaction.OperationModeList.OPERATING;

            //        string strSRSResult = string.Empty;
            //        ticket.SRSTransactionNo = tran.TransactionNumber;
            //        ticket.SRSSubmitCount++;
            //        strSRSResult = tran.Submit();
            //        if (strSRSResult.Contains("#00#"))
            //        {
            //            SRSState.PSIQRString = tran.RQ_QRCode_String;
            //            SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
            //            DateTime dt;
            //            if (DateTime.TryParseExact(tran.RQ_PSI_DAT_TRANS, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //                SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //            else
                            SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //        }
            //        else
            //        {
            //            if (strSRSResult != string.Empty)
            //            {
            //                var strTmp = strSRSResult.Split('#');
            //                if (strTmp.Length > 2 && !string.IsNullOrEmpty(strTmp[3]))
            //                {
            //                    SRSState.PSIQRString = tran.RQ_QRCode_String;
            //                    SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
            //                    SRSState.PSITransactionDate = tran.RQ_PSI_DAT_TRANS;
            //                }
            //                else
            //                    MessageBox.Show(strSRSResult.Replace("^", "\n"), "SRS message d'erreur");
            //            }
            //        }
            //    }
            //}
            //catch (Exception) { }
        }

        //private void SRS_ADDI_SOB_Submit(Ticket ticket)   //By Tim GU. to cancel unsubmitted temporary bills with ordered items (by clicking the 'Annuler Billet' button)
        //{
        //    Transaction tran = new Transaction();
        //    List<TransactionItem> lstItems = new List<TransactionItem>();
        //    List<TransactionItemDetail> lstPreci = new List<TransactionItemDetail>();

        //    try
        //    {
        //        tran.TransactionNumber = ticket.TicketNumber;
        //        tran.UserName = ticket.LastModifiedUserName;
        //        tran.sevRecord.SRSDeviceIdapprl = SRSState.SRSDeviceIdapprl;

        //        if (ticket.TicketEntities.Count > 0)
        //        {
        //            if (ticket.TicketEntities[0].EntityTypeId == 2)
        //            {
        //                tran.ServiceType = Transaction.ServiceTypeList.TABLE_SERVICE;
        //                tran.TableNumber = ticket.TicketEntities[0].EntityName;
        //            }
        //            else
        //            {
        //                tran.ServiceType = Transaction.ServiceTypeList.COUNTER_SERVICE;
        //                tran.client.NomClint = ticket.TicketEntities[0].EntityName;
        //                try
        //                {
        //                    string[] strTemp = ticket.TicketEntities[0].EntityCustomData.Split('"');
        //                    tran.client.Tel1 = strTemp[7];
        //                }
        //                catch { };
        //            }
        //        }

        //        tran.NumberOfCustomer = ticket.GetCustomerCount();

        //        if (ticket.Orders.Count > 0)
        //        {
        //            for (int i = 0; i < ticket.Orders.Count; i++)
        //            {
        //                if (ticket.Orders[i].CalculatePrice)
        //                {
        //                    if (ticket.Orders[i].OrderTags != string.Empty)
        //                    {
        //                        IList<OrderTagValue> OrderTagValues = JsonHelper.Deserialize<List<OrderTagValue>>(ticket.Orders[i].OrderTags);
        //                        if (OrderTagValues.Count > 0)
        //                        {
        //                            lstPreci.Clear();
        //                            for (int j = 0; j < OrderTagValues.Count; j++)
        //                            {
        //                                string strTax = OrderTagValues[j].Price == 0 ? Transaction.ApplicableTaxList.UNKNOWN : Transaction.ApplicableTaxList.GST_AND_QST;
        //                                string strActi = OrderTagValues[j].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
        //                                lstPreci.Add(new TransactionItemDetail(OrderTagValues[j].Quantity, OrderTagValues[j].TagValue, OrderTagValues[j].Price, OrderTagValues[j].Quantity * OrderTagValues[j].Price, strTax, strActi));
        //                            }
        //                        }
        //                    }
        //                    if (lstPreci.Count > 0)
        //                        lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, Transaction.ActivitySubsectorList.RESTAURANT, lstPreci));
        //                    else
        //                        lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, Transaction.ActivitySubsectorList.RESTAURANT));
        //                }
        //            }
        //            if (ticket.Calculations.Count > 0)
        //            {
        //                for (int i = 0; i < ticket.Calculations.Count; i++)
        //                {
        //                    if (ticket.Calculations[i].Name == "Annulation")
        //                    {
        //                        lstPreci.Clear();
        //                        lstPreci.Add(new TransactionItemDetail(0, "commande annulée", 0, 0, Transaction.ApplicableTaxList.GST_AND_QST, Transaction.ActivitySubsectorList.UNKNOWN));
        //                        lstItems.Add(new TransactionItem(1m, "Annulation", -ticket.Calculations[i].CalculationAmount, -ticket.Calculations[i].CalculationAmount, Transaction.ApplicableTaxList.GST_AND_QST, Transaction.ActivitySubsectorList.RESTAURANT, lstPreci));
        //                    }
        //                }
        //            }

        //            tran.lstItems = lstItems;
        //            SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;

        //            int iSubNo = ticket.SRSSubNo;
        //            ticket.SRSSubNo++;
        //            ticket.SRSTransactionNo = $"{ticket.TicketNumber}";
        //            tran.TransactionNumber = ticket.SRSTransactionNo;
        //            tran.TransactionTypeId = Transaction.TransactionTypeIDList.CANCELLED_BILL;
        //            SRSState.PaymentStatus = "FACTURE ANNULÉE";
        //            tran.sevRecord.PaymentStatus = SRSState.PaymentStatus;
        //            SRSState.PaymentMethod = "AUCUN PAIEMENT";
        //            tran.sevRecord.PaymentMethod = SRSState.PaymentMethod;

        //            tran.AmountBeforeTax = 0;
        //            tran.sevRecord.AmountBeforeTax = 0;
        //            tran.GSTAmount = 0;
        //            tran.QSTAmount = 0;
        //            tran.AmountAfterTax = 0;
        //            tran.sevRecord.AmountAfterTax = 0;
        //            tran.TipAmount = 0;
        //            tran.BusinessRelationship = Transaction.BusinessRelationshipList.B2C;
        //            tran.IsOnlineOrder = Transaction.OnlineOrderStatus.NOT_ONLINE_ORDER;
        //            tran.TransactionType = Transaction.TransactionTypeList.TEMPORARY_BILL;
        //            tran.PaymentMethod = Transaction.PaymentMethodList.NA;
        //            tran.PrintMode = Transaction.PrintModeList.CANCELLATION;
        //            tran.PrintOption = Transaction.PrintOptionList.NOT_PRINTED;
        //            tran.OperationMode = Transaction.OperationModeList.OPERATING;

        //            string strSRSResult = string.Empty;
        //            ticket.SRSTransactionNo = tran.TransactionNumber;
        //            ticket.SRSSubmitCount++;
        //            strSRSResult = tran.Submit();
        //            if (strSRSResult.Contains("#00#"))
        //            {
        //                SRSState.PSIQRString = tran.RQ_QRCode_String;
        //                SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
        //                DateTime dt;
        //                if (DateTime.TryParseExact(tran.RQ_PSI_DAT_TRANS, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
        //                    SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
        //                else
        //                    SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        //            }
        //            else
        //            {
        //                if (strSRSResult != string.Empty)
        //                {
        //                    if (strSRSResult.Contains("#99#"))
        //                    {
        //                        var strTmp = strSRSResult.Split('#');
        //                        if (!string.IsNullOrEmpty(strTmp[2]))
        //                        {
        //                            SRSState.PSIQRString = tran.RQ_QRCode_String;
        //                            SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
        //                            SRSState.PSITransactionDate = tran.RQ_PSI_DAT_TRANS;
        //                        }
        //                        else
        //                            MessageBox.Show(strSRSResult, "SRS Result");
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception) { }
        //}

        private void SRS_ADDI_RPR_Submit(Ticket ticket)    //By Tim GU. SRS Transaction Submission for the Reproduction of a temporary bill.
        {
            //Transaction tran = new Transaction();

            //try
            //{
            //    //int iTranTypeId = 11;
            //    string modImpr = "RPR";

            //    tran.sevRecord = new SEVRecord(ticket.TicketNumber, modImpr);
            //    if (tran.sevRecord.Id != 0)
            //    {
            //        SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;
            //        SRSState.TransactionServer = tran.sevRecord.TransactionServer;
            //        SRSState.PaymentMethod = tran.sevRecord.PaymentMethod;
            //        SRSState.PaymentStatus = tran.sevRecord.PaymentStatus;
                    SRSState.AdditionalInfo = "REPRODUCTION";
            //        SRSState.PrintCopyType = tran.sevRecord.PrintCopyType;
            //        //SRSState.PSIQRString = sEVRecord.QRCodeString;
            //        //SRSState.PSITransactionNumber = sEVRecord.PSITransactionNo;
            //        //DateTime dt;
            //        //if (DateTime.TryParseExact(sEVRecord.PSITransactionDate, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //        //    SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //        //else
            //        //    SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //    }

            //    string strSRSResult = string.Empty;
            //    strSRSResult = tran.Submit(ticket.TicketNumber, modImpr, true);
            //    if (strSRSResult.Contains("#00#"))
            //    {
            //        SRSState.PSIQRString = tran.sevRecord.QRCodeString;
            //        SRSState.PSITransactionNumber = tran.sevRecord.PSITransactionNo;
            //        DateTime dt;
            //        if (DateTime.TryParseExact(tran.sevRecord.PSITransactionDate, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //            SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //        else
            //            SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //    }
            //    else
            //    {
            //        if (strSRSResult != string.Empty)
            //        {
            //            var strTmp = strSRSResult.Split('#');
            //            if (strTmp.Length > 2 && !string.IsNullOrEmpty(strTmp[3]))
            //            {
            //                SRSState.PSIQRString = tran.sevRecord.QRCodeString;
            //                SRSState.PSITransactionNumber = tran.sevRecord.PSITransactionNo;
            //                SRSState.PSITransactionDate = tran.sevRecord.PSITransactionDate;
            //            }
            //            else
            //                MessageBox.Show(strSRSResult.Replace("^", "\n"), "SRS message d'erreur");
            //        }
            //    }
            //}
            //catch (Exception) { }
        }

        private void SRS_ADDI_DUP_Submit(Ticket ticket)    //By Tim GU. SRS Transaction Submission for the Duplication of a temporary bill.
        {
            //Transaction tran = new Transaction();

            //try
            //{
            //    //int iTranTypeId = 11;
            //    string modImpr = "DUP";

            //    tran.sevRecord = new SEVRecord(ticket.TicketNumber, modImpr);
            //    if (tran.sevRecord.Id != 0)
            //    {
            //        SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;
            //        SRSState.TransactionServer = tran.sevRecord.TransactionServer;
            //        SRSState.PaymentMethod = tran.sevRecord.PaymentMethod;
            //        SRSState.PaymentStatus = tran.sevRecord.PaymentStatus;
            //        SRSState.AdditionalInfo = tran.sevRecord.AdditionalInfo;
            //        SRSState.PrintCopyType = tran.sevRecord.PrintCopyType;
            //        //SRSState.PSIQRString = sEVRecord.QRCodeString;
            //        //SRSState.PSITransactionNumber = sEVRecord.PSITransactionNo;
            //        //DateTime dt;
            //        //if (DateTime.TryParseExact(sEVRecord.PSITransactionDate, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //        //    SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //        //else
            //        //    SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //    }

            //    string strSRSResult = string.Empty;
            //    strSRSResult = tran.Submit(ticket.TicketNumber, modImpr, true);
            //    if (strSRSResult.Contains("#00#"))
            //    {
            //        SRSState.PSIQRString = tran.sevRecord.QRCodeString;
            //        SRSState.PSITransactionNumber = tran.sevRecord.PSITransactionNo;
            //        DateTime dt;
            //        if (DateTime.TryParseExact(tran.sevRecord.PSITransactionDate, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //            SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //        else
            //            SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //    }
            //    else
            //    {
            //        if (strSRSResult != string.Empty)
            //        {
            //            var strTmp = strSRSResult.Split('#');
            //            if (strTmp.Length > 2 && !string.IsNullOrEmpty(strTmp[3]))
            //            {
            //                SRSState.PSIQRString = tran.sevRecord.QRCodeString;
            //                SRSState.PSITransactionNumber = tran.sevRecord.PSITransactionNo;
            //                SRSState.PSITransactionDate = tran.sevRecord.PSITransactionDate;
            //            }
            //            else
            //                MessageBox.Show(strSRSResult.Replace("^", "\n"), "SRS message d'erreur");
            //        }
            //    }
            //}
            //catch (Exception) { }
        }

        private void SRS_RFER_Submit(Ticket ticket, PrintJob printJob)    //By Tim GU. SRS Transaction Submission for Reproduction/Duplication of already paid closing receipts.
        {
            //Transaction tran = new Transaction();

            //try
            //{
            //    //int iTranTypeId = 11;
            //    string modImpr = "RPR";

            //    if (printJob.Id == 4)   //Reprint Customer Copy
            //    {
            //        //iTranTypeId = Transaction.TransactionTypeIDList.REPRODUCE_BILL;
            //        modImpr = "RPR";
            //    }
                    
            //    else
            //    {
            //        if (printJob.Id == 5)   //Print Merchant Copy
            //        {
            //            //iTranTypeId = Transaction.TransactionTypeIDList.DUPLICATE_BILL;
            //            modImpr = "DUP";
            //        }
                        
            //    }

            //    tran.sevRecord = new SEVRecord(ticket.TicketNumber);
            //    if (tran.sevRecord.Id != 0)
            //    {
            //        SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;
            //        SRSState.TransactionServer = tran.sevRecord.TransactionServer;
            //        SRSState.PaymentMethod = tran.sevRecord.PaymentMethod;
            //        SRSState.PaymentStatus = tran.sevRecord.PaymentStatus;
            //        SRSState.AdditionalInfo = tran.sevRecord.AdditionalInfo;
            //        SRSState.PrintCopyType = tran.sevRecord.PrintCopyType;
            //        //SRSState.PSIQRString = sEVRecord.QRCodeString;
            //        //SRSState.PSITransactionNumber = sEVRecord.PSITransactionNo;
            //        //DateTime dt;
            //        //if (DateTime.TryParseExact(sEVRecord.PSITransactionDate, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //        //    SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //        //else
            //        //    SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //    }

            //    string strSRSResult = string.Empty;
            //    strSRSResult = tran.Submit(ticket.TicketNumber, modImpr);
            //    if (strSRSResult.Contains("#00#"))
            //    {
            //        SRSState.PSIQRString = tran.sevRecord.QRCodeString;
            //        SRSState.PSITransactionNumber = tran.sevRecord.PSITransactionNo;
            //        DateTime dt;
            //        if (DateTime.TryParseExact(tran.sevRecord.PSITransactionDate, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            //            SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
            //        else
            //            SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //    }
            //    else
            //    {
            //        if (strSRSResult != string.Empty)
            //        {
            //            var strTmp = strSRSResult.Split('#');
            //            if (strTmp.Length > 2 && !string.IsNullOrEmpty(strTmp[3]))
            //            {
            //                SRSState.PSIQRString = tran.sevRecord.QRCodeString;
            //                SRSState.PSITransactionNumber = tran.sevRecord.PSITransactionNo;
            //                SRSState.PSITransactionDate = tran.sevRecord.PSITransactionDate;
            //            }
            //            else
            //                MessageBox.Show(strSRSResult.Replace("^", "\n"), "SRS message d'erreur");
            //        }
            //    }
            //}
            //catch (Exception) { }
        }

        private string SRS_REVERSE_RFER_Submit(Ticket ticket)
        {
            //Transaction tran = new Transaction();

            //return tran.Reverse(ticket.TicketNumber, ticket.SRSSubNo);
            return "#00#";
        }

        private void SRS_ADDI_Submit(Ticket ticket, PrintJob printJob, string formImpr)     //By Tim GU. SRS Transaction Submission for Original Temporary Bills or Revised Temporary Bills.
        {
           //Transaction tran = new Transaction();
           // List<TransactionItem> lstItems = new List<TransactionItem>();            

           // try
           // {
           //     tran.TransactionNumber = ticket.TicketNumber;
           //     tran.UserName = ticket.LastModifiedUserName;
           //     tran.sevRecord.SRSDeviceIdapprl = SRSState.SRSDeviceIdapprl;

           //     if (ticket.TicketEntities.Count > 0)
           //     {
           //         if (ticket.TicketEntities[0].EntityTypeId == 2)
           //         {
           //             tran.ServiceType = Transaction.ServiceTypeList.TABLE_SERVICE;
           //             tran.TableNumber = ticket.TicketEntities[0].EntityName;
           //         }
           //         else
           //         {
           //             tran.ServiceType = Transaction.ServiceTypeList.COUNTER_SERVICE;
           //             tran.client.NomClint = ticket.TicketEntities[0].EntityName;
           //             try
           //             {
           //                 string[] strTemp = ticket.TicketEntities[0].EntityCustomData.Split('"');
           //                 tran.client.Tel1 = strTemp[7];
           //             }
           //             catch { };
           //         }                        
           //     }

           //     tran.NumberOfCustomer = ticket.GetCustomerCount();
                    
           //     if (ticket.Orders.Count > 0)
           //     {
           //         for (int i = 0; i < ticket.Orders.Count; i++)
           //         {
           //             List<TransactionItemDetail> lstDetail = new List<TransactionItemDetail>();
           //             if (ticket.Orders[i].CalculatePrice)
           //             {
           //                 if (ticket.Orders[i].OrderTags != null)
           //                 {
           //                     IList<OrderTagValue> OrderTagValues = JsonHelper.Deserialize<List<OrderTagValue>>(ticket.Orders[i].OrderTags);
           //                     if (OrderTagValues.Count > 0)
           //                     {
           //                         for (int j = 0; j < OrderTagValues.Count; j++)
           //                         {
           //                             string strTax = OrderTagValues[j].Price == 0 ? Transaction.ApplicableTaxList.UNKNOWN : Transaction.ApplicableTaxList.GST_AND_QST;
           //                             string strActi = OrderTagValues[j].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
           //                             lstDetail.Add(new TransactionItemDetail(OrderTagValues[j].Quantity, OrderTagValues[j].TagValue, OrderTagValues[j].Price, OrderTagValues[j].Quantity * OrderTagValues[j].Price, strTax, strActi));
           //                         }
           //                     }
           //                 }
           //                 string strACTI = ticket.Orders[i].Price == 0 ? Transaction.ActivitySubsectorList.UNKNOWN : Transaction.ActivitySubsectorList.RESTAURANT;
           //                 if (lstDetail.Count > 0)
           //                     lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI, lstDetail));
           //                 else
           //                     lstItems.Add(new TransactionItem(ticket.Orders[i].Quantity, ticket.Orders[i].Description, ticket.Orders[i].Price, ticket.Orders[i].Quantity * ticket.Orders[i].Price, Transaction.ApplicableTaxList.GST_AND_QST, strACTI));
           //             }
           //         }
                
           //         tran.lstItems = lstItems;
           //         SRSState.Clear();
           //         SRSState.SRSTransactionTime = tran.sevRecord.SRSTransactionTime;

           //         if (ticket.SRSSubmitCount == 0)
           //         {
           //             ticket.SRSTransactionNo = ticket.TicketNumber;
           //             tran.TransactionTypeId = Transaction.TransactionTypeIDList.ORIGINAL_TEMPORARY_BILL;
           //             SRSState.PaymentStatus = "FACTURE ORIGINALE";
           //             tran.sevRecord.PaymentStatus = SRSState.PaymentStatus;  
           //             if(!string.IsNullOrEmpty(ticket.RefSRSTransactionNo))
           //             {
           //                 if (ticket.RefSRSTransactionNo.Contains(","))
           //                 {
           //                     var refs = ticket.RefSRSTransactionNo.Split(',');
           //                     foreach(var strRef in refs)
           //                     {
           //                         if(strRef != string.Empty)
           //                             tran.AddRef(strRef);
           //                     }
           //                 }
           //                 else
           //                     tran.AddRef(ticket.RefSRSTransactionNo);
           //             }                            
           //         }
           //         else
           //         {
           //             int iSubNo = ticket.SRSSubNo;
           //             string strRefNo = iSubNo == 0 ? ticket.TicketNumber : $"{ticket.TicketNumber}-{ticket.SRSSubNo}";
           //             tran.AddRef(strRefNo);
           //             ticket.SRSSubNo++;
           //             ticket.SRSTransactionNo = $"{ticket.TicketNumber}-{ticket.SRSSubNo}";
           //             tran.TransactionNumber = ticket.SRSTransactionNo;
           //             tran.TransactionTypeId = Transaction.TransactionTypeIDList.REVISED_TEMPORARY_BILL;
           //             SRSState.PaymentStatus = "FACTURE RÉVISÉE";
           //             tran.sevRecord.PaymentStatus = SRSState.PaymentStatus;
           //             SRSState.AdditionalInfo = "Remplace 1 facture";
           //             tran.sevRecord.AdditionalInfo = SRSState.AdditionalInfo;
           //         }

           //         tran.AmountBeforeTax = ticket.GetPlainSum();
           //         tran.sevRecord.AmountBeforeTax = tran.AmountBeforeTax;
           //         tran.GSTAmount = ticket.GetTPSTotal();
           //         tran.QSTAmount = ticket.GetTVQTotal();
           //         tran.AmountAfterTax = ticket.GetSum();
           //         tran.sevRecord.AmountAfterTax = tran.AmountAfterTax;
           //         tran.BusinessRelationship = Transaction.BusinessRelationshipList.B2C;
           //         tran.IsOnlineOrder = Transaction.OnlineOrderStatus.NOT_ONLINE_ORDER;
           //         tran.TransactionType = Transaction.TransactionTypeList.TEMPORARY_BILL;
           //         tran.PaymentMethod = Transaction.PaymentMethodList.NA;
           //         tran.PrintMode = Transaction.PrintModeList.BILL_OR_DOCUMENT_NOT_INCLUDED;
           //         tran.PrintOption = formImpr;
           //         tran.OperationMode = Transaction.OperationModeList.OPERATING;

           //         string strSRSResult = string.Empty;
           //         ticket.SRSSubmitCount++;
           //         strSRSResult = tran.Submit();                    
           //         if (strSRSResult.Contains("#00#"))
           //         {
           //             SRSState.PSIQRString = tran.RQ_QRCode_String;
           //             SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
           //             DateTime dt;
           //             if (DateTime.TryParseExact(tran.RQ_PSI_DAT_TRANS, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
           //                 SRSState.PSITransactionDate = dt.ToString("yyyy-MM-dd HH:mm:ss");
           //             else
           //                 SRSState.PSITransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

           //             if(printJob.Id == 9)
           //             {
           //                 MessageBox.Show("Transmis avec succès à RQ!");
           //             }
           //         }
           //         else
           //         {
           //             if (strSRSResult != string.Empty)
           //             {
           //                 var strTmp = strSRSResult.Split('#');
           //                 if (strTmp.Length>2 && !string.IsNullOrEmpty(strTmp[3]))
           //                 {
           //                     SRSState.PSIQRString = tran.RQ_QRCode_String;
           //                     SRSState.PSITransactionNumber = tran.RQ_PSI_NO_TRANS;
           //                     SRSState.PSITransactionDate = tran.RQ_PSI_DAT_TRANS;
           //                 }
           //                 else
           //                     MessageBox.Show(strSRSResult.Replace("^","\n"), "SRS message d'erreur");                      
           //             }
           //         }
           //     }
           // }
           // catch (Exception) { }
        }
        #endregion  //Region added by Tim GU
    }
}
