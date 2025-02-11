using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Entities;
using Samba.Domain.Models.Menus;
using Samba.Domain.Models.Settings;
using Samba.Domain.Models.Tickets;
using Samba.Infrastructure.Helpers;
using Samba.Infrastructure.Settings;
using Samba.Localization.Properties;
using Samba.Persistance;

namespace Samba.Services.Implementations.PrinterModule.ValueChangers
{
    [Export]
    public class FunctionRegistry
    {
        private readonly IAccountDao _accountDao;
        private readonly IDepartmentService _departmentService;
        private readonly ISettingService _settingService;
        private readonly ICacheService _cacheService;
        private readonly IEntityService _entityService;

        public IDictionary<Type, ArrayList> Functions = new Dictionary<Type, ArrayList>();
        public IDictionary<string, string> Descriptions = new Dictionary<string, string>();

        [ImportingConstructor]
        public FunctionRegistry(IAccountDao accountDao, IDepartmentService departmentService, ISettingService settingService,
            ICacheService cacheService, IEntityService entityService)
        {
            _accountDao = accountDao;
            _departmentService = departmentService;
            _settingService = settingService;
            _cacheService = cacheService;
            _entityService = entityService;
        }

        public void RegisterFunctions()
        {
            //GENERIC
            //RegisterFunction(TagNames.Date, (x, d) => DateTime.Now.ToShortDateString(), Resources.DayDate);   //Commented by Tim GU
            //RegisterFunction(TagNames.Time, (x, d) => DateTime.Now.ToShortTimeString(), Resources.DayTime);   //Commented by Tim GU
            RegisterFunction(TagNames.Date, (x, d) => DateTime.Now.ToString("yyyy-MM-dd"), Resources.DayDate);   //Added by Tim GU
            RegisterFunction(TagNames.Time, (x, d) => DateTime.Now.ToString("HH:mm:ss"), Resources.DayTime);   //Added by Tim GU
            RegisterFunction(TagNames.BusinessName, (x, d) => BusinessInfo.BusinessName, "Business Name");   //Added by Tim GU  
            RegisterFunction(TagNames.TPSNo, (x, d) => BusinessInfo.TPSNumber, "Business GST Number");   //Added by Tim GU 
            RegisterFunction(TagNames.TVQNo, (x, d) => BusinessInfo.TVQNumber, "Business QST Number");   //Added by Tim GU 
            RegisterFunction(TagNames.Civiq, (x, d) => BusinessInfo.CiviqNumber, "Civic Number");   //Added by Tim GU 
            RegisterFunction(TagNames.Street, (x, d) => BusinessInfo.Street, "Street");   //Added by Tim GU 
            RegisterFunction(TagNames.City, (x, d) => BusinessInfo.City, "City");   //Added by Tim GU
            RegisterFunction(TagNames.Province, (x, d) => BusinessInfo.Province, "Province");   //Added by Tim GU
            RegisterFunction(TagNames.PostCode, (x, d) => BusinessInfo.PostCode, "Post Code");   //Added by Tim GU
            RegisterFunction(TagNames.BusinessPhoneNumber, (x, d) => BusinessInfo.PhoneNumber, "Business Phone Number");   //Added by Tim GU
            RegisterFunction(TagNames.PaymentMethod,(x, d) => SRSState.PaymentMethod, "Payment Method");   //Added by Tim GU
            RegisterFunction(TagNames.PaymentStatus, (x, d) => SRSState.PaymentStatus, "SRS Payment Status");   //Added by Tim GU
            RegisterFunction(TagNames.AdditionalInfo, (x, d) => SRSState.AdditionalInfo, "SRS Additional Information");   //Added by Tim GU
            RegisterFunction(TagNames.PrintCopyType, (x, d) => SRSState.PrintCopyType, "SRS Print Copy Type");   //Added by Tim GU
            RegisterFunction(TagNames.QRString, (x, d) => SRSState.PSIQRString, "SRS PSI QR String");   //Added by Tim GU
            RegisterFunction(TagNames.PSITransactionDate, (x, d) => SRSState.PSITransactionDate, "SRS PSI Transaction Date");   //Added by Tim GU
            RegisterFunction(TagNames.PSITransactionNo, (x, d) => SRSState.PSITransactionNumber, "SRS PSI Transaction Number");   //Added by Tim GU
            RegisterFunction(TagNames.DeviceIDapprl, (x, d) => SRSState.SRSDeviceIdapprl, "SRS Device ID");   //Added by Tim GU
            RegisterFunction(TagNames.TransactionServer, (x, d) => SRSState.TransactionServer, "Transaction Server Name");   //Added by Tim GU
            RegisterFunction(TagNames.SRSTransactionTime, (x, d) => SRSState.SRSTransactionTime, "SRS Transaction Time");   //Added by Tim GU
            RegisterFunction("{SETTING:([^}]+)}", (x, d) => _settingService.ReadSetting(d).StringValue, Resources.SettingValue);
            RegisterFunction("{RANDOM}", (x, d) => Utility.GetDateBasedUniqueString(), "Date based Random Number");
            RegisterFunction("{RANDOM:([^}]+)}", (x, d) => GenerateRandomNumber(SafeInt(d, 8), false), "Random Number");
            RegisterFunction("{RANDOMC:([^}]+)}", (x, d) => GenerateRandomNumber(SafeInt(d, 8), true), "Random Number with check digit");

            //TICKETS
            RegisterFunction<Ticket>(TagNames.TicketDate, (x, d) => x.Date.ToShortDateString(), string.Format(Resources.Date_f, Resources.Ticket));
            RegisterFunction<Ticket>(TagNames.TicketTime, (x, d) => x.Date.ToShortTimeString(), string.Format(Resources.Time, Resources.Ticket));
            RegisterFunction<Ticket>("{LAST ORDER TIME}", (x, d) => x.LastOrderDate.ToShortTimeString(), string.Format(Resources.Time, Resources.LastOrder));
            RegisterFunction<Ticket>("{CREATION MINUTES}", (x, d) => x.GetTicketCreationMinuteStr(), Resources.TicketDuration);
            RegisterFunction<Ticket>("{LAST ORDER MINUTES}", (x, d) => x.GetTicketLastOrderMinuteStr(), Resources.LastOrderDuration);
            RegisterFunction<Ticket>(TagNames.TicketId, (x, d) => x.Id.ToString("#"), Resources.UniqueTicketId);
            RegisterFunction<Ticket>(TagNames.TicketNo, (x, d) => x.TicketNumber, Resources.TicketNumber);
            RegisterFunction<Ticket>(TagNames.TransNo, (x, d) => x.SRSTransactionNo, "SRS Transaction Number");   //Added by Tim GU
            //RegisterFunction<Ticket>(TagNames.OrderNo, (x, d) => x.Orders.Last().OrderNumber.ToString(), Resources.LineOrderNumber);
            RegisterFunction<Ticket>(TagNames.UserName, (x, d) => x.Orders.Last().CreatingUserName, Resources.UserName);
            RegisterFunction<Ticket>(TagNames.Department, (x, d) => GetDepartmentName(x.DepartmentId), Resources.Department);
            RegisterFunction<Ticket>(TagNames.Note, (x, d) => x.Note, Resources.TicketNote);
            RegisterFunction<Ticket>(TagNames.PlainTotal, (x, d) => x.GetPlainSum().ToString("N", LocalSettings.Culture), Resources.TicketSubTotal, x => x.GetSum() != x.GetPlainSum());
            RegisterFunction<Ticket>(TagNames.DiscountTotal, (x, d) => x.GetPreTaxServicesTotal().ToString("N", LocalSettings.Culture), Resources.DiscountTotal);
            RegisterFunction<Ticket>(TagNames.BeforeTax, (x, d) => x.GetBeforeTax().ToString("N", LocalSettings.Culture), "Before Tax");
            RegisterFunction<Ticket>(TagNames.TaxTotal, (x, d) => x.CalculateTax(x.GetPlainSum(), x.GetPreTaxServicesTotal()).ToString("N", LocalSettings.Culture), Resources.TaxTotal);
            RegisterFunction<Ticket>(TagNames.AfterTax, (x, d) => x.GetAfterTax().ToString("N", LocalSettings.Culture), "After Tax");
            RegisterFunction<Ticket>(TagNames.TicketTotal, (x, d) => x.GetSum().ToString("N", LocalSettings.Culture), Resources.TicketTotal, x => x.GetSum() != x.GetAfterTax() );
            RegisterFunction<Ticket>(TagNames.PaymentTotal, (x, d) => x.GetPaymentAmount().ToString("N", LocalSettings.Culture), Resources.PaymentTotal);
            RegisterFunction<Ticket>(TagNames.Balance, (x, d) => x.GetRemainingAmount().ToString("N", LocalSettings.Culture), Resources.Balance, x => x.GetRemainingAmount() != x.GetSum());
            RegisterFunction<Ticket>(TagNames.TotalText, (x, d) => HumanFriendlyInteger.CurrencyToWritten(x.GetSum()), Resources.TextWrittenTotalValue);
            RegisterFunction<Ticket>(TagNames.Totaltext, (x, d) => HumanFriendlyInteger.CurrencyToWritten(x.GetSum(), true), Resources.TextWrittenTotalValue);
            RegisterFunction<Ticket>("{TICKET TAG:([^}]+)}", (x, d) => x.GetTagValue(d), Resources.TicketTag);
            RegisterFunction<Ticket>("{TICKET STATE:([^}]+)}", (x, d) => x.GetStateStr(d), Resources.TicketState);
            RegisterFunction<Ticket>("{TICKET STATE QUANTITY:([^}]+)}", (x, d) => x.GetStateQuantityStr(d), "Ticket State Quantity");
            RegisterFunction<Ticket>("{TICKET STATE MINUTES:([^}]+)}", (x, d) => x.GetStateMinuteStr(d), "Ticket State Duration");

            RegisterFunction<Ticket>("{CALCULATION TOTAL:([^}]+)}", (x, d) => x.GetCalculationTotal(d).ToString("N", LocalSettings.Culture), string.Format(Resources.Total_f, Resources.Calculation), x => x.Calculations.Count > 0);
            RegisterFunction<Ticket>("{ENTITY NAME:([^}]+)}", (x, d) => x.GetEntityName(_cacheService.GetEntityTypeIdByEntityName(d)), string.Format(Resources.Name_f, Resources.Entity));
            RegisterFunction<Ticket>("{ENTITY DATA:([^}]+)}", GetEntityFieldValue, "Entity Custom Data");
            RegisterFunction<Ticket>("{ENTITY BALANCE:([^}]+)}", GetEntityBalance, "Entity Account Balance");
            RegisterFunction<Ticket>("{ORDER STATE TOTAL:([^}]+)}", (x, d) => x.GetOrderStateTotal(d).ToString("N", LocalSettings.Culture), string.Format(Resources.Total_f, Resources.OrderState), x => x.GetOrderStateTotal("Gift") != 0);
            RegisterFunction<Ticket>("{ORDER STATE QUANTITY TOTAL:([^}]+)}", (x, d) => x.GetOrderStateQuantityTotal(d).ToString(LocalSettings.QuantityFormat), string.Format(Resources.Total_f, "Order State Quantity"));
            RegisterFunction<Ticket>("{SERVICE TOTAL}", (x, d) => x.GetPostTaxServicesTotal().ToString("N", LocalSettings.Culture), string.Format(Resources.Total_f, Resources.Service));
            RegisterFunction<Ticket>("{EXCHANGE RATE:([^}]+)}", (x, d) => GexExchangeRate(d), Resources.ExchangeRate);
            RegisterFunction<Ticket>("{TICKET QUANTITY SUM}", (x, d) => x.Orders.Sum(y => y.Quantity).ToString(LocalSettings.QuantityFormat));
            RegisterFunction<Ticket>("{ORDER COUNT}", (x, d) => x.Orders.Count.ToString(CultureInfo.InvariantCulture), "Order Count");

            //ORDERS
            RegisterFunction<Order>(TagNames.Quantity, (x, d) => x.Quantity.ToString(LocalSettings.QuantityFormat), Resources.LineItemQuantity);
            RegisterFunction<Order>(TagNames.Name, (x, d) => x.MenuItemName + x.GetPortionDesc(), Resources.LineItemName);
            RegisterFunction<Order>(TagNames.Price, (x, d) => x.Price.ToString("N", LocalSettings.Culture), Resources.LineItemPrice);
            RegisterFunction<Order>(TagNames.Total, (x, d) => x.GetPrice().ToString("N", LocalSettings.Culture), Resources.LineItemTotal);
            RegisterFunction<Order>(TagNames.TotalAmount, (x, d) => x.GetValue().ToString("N", LocalSettings.Culture), Resources.LineItemTotalAndQuantity);
            RegisterFunction<Order>(TagNames.Cents, (x, d) => (x.Price * 100).ToString(LocalSettings.QuantityFormat), Resources.LineItemPriceCents);
            RegisterFunction<Order>(TagNames.LineAmount, (x, d) => x.GetTotal().ToString("N", LocalSettings.Culture), Resources.LineItemTotalWithoutGifts);
            RegisterFunction<Order>(TagNames.OrderNo, (x, d) => x.OrderNumber.ToString("#"), Resources.LineOrderNumber);
            RegisterFunction<Order>("{ORDER DATE}", (x, d) => x.CreatedDateTime.ToShortDateString(), string.Format(Resources.Date_f, Resources.Order));
            RegisterFunction<Order>("{ORDER TIME}", (x, d) => x.CreatedDateTime.ToShortTimeString(), string.Format(Resources.Time_f, Resources.Order));
            RegisterFunction<Order>(TagNames.PriceTag, (x, d) => x.PriceTag, Resources.LinePriceTag);
            RegisterFunction<Order>("{ORDER TAG:([^}]+)}", (x, d) => x.GetOrderTagValue(d).TagValue, Resources.OrderTagValue);
            RegisterFunction<Order>("{ORDER STATE:([^}]+)}", (x, d) => x.GetStateValue(d).State, Resources.OrderState);
            RegisterFunction<Order>("{ORDER STATE VALUE:([^}]+)}", (x, d) => x.GetStateValue(d).StateValue, Resources.OrderStateValue);
            RegisterFunction<Order>("{ORDER STATE MINUTES:([^}]+)}", (x, d) => x.GetStateMinuteStr(d), "Order State Duration");
            RegisterFunction<Order>("{ORDER TAX RATE:([^}]+)}", (x, d) => x.GetTaxValue(d).TaxRate.ToString(LocalSettings.QuantityFormat), Resources.TaxRate);
            RegisterFunction<Order>("{ORDER TAX TEMPLATE NAMES}", (x, d) => string.Join(", ", x.GetTaxValues().Select(y => y.TaxTemplateName)), string.Format(Resources.List_f, Resources.TaxTemplate));
            RegisterFunction<Order>("{ITEM ID}", (x, d) => x.MenuItemId.ToString("#"));
            RegisterFunction<Order>("{BARCODE}", (x, d) => GetMenuItem(x.MenuItemId).Barcode);
            RegisterFunction<Order>("{GROUP CODE}", (x, d) => GetMenuItem(x.MenuItemId).GroupCode);
            RegisterFunction<Order>("{ITEM TAG}", (x, d) => GetMenuItem(x.MenuItemId).Tag);

            //ORDER TAG VALUES
            RegisterFunction<OrderTagValue>(TagNames.OrderTagPrice, (x, d) => x.AddTagPriceToOrderPrice ? x.Price.ToString("N", LocalSettings.Culture) : "", Resources.OrderTagPrice, x => x.Price != 0 );   //Bug fixed by Tim GU
            RegisterFunction<OrderTagValue>(TagNames.OrderTagQuantity, (x, d) => x.Quantity.ToString(LocalSettings.QuantityFormat), Resources.OrderTagQuantity);
            RegisterFunction<OrderTagValue>(TagNames.OrderTagName, (x, d) => x.TagValue, Resources.OrderTagName, x => !string.IsNullOrEmpty(x.TagValue));

            //TICKET RESOURCES
            RegisterFunction<TicketEntity>("{ENTITY NAME}", (x, d) => x.EntityName, string.Format(Resources.Name_f, Resources.Entity));
            RegisterFunction<TicketEntity>("{ENTITY BALANCE}", (x, d) => _accountDao.GetAccountBalance(x.AccountId).ToString("N", LocalSettings.Culture), Resources.AccountBalance, x => x.AccountId > 0);
            RegisterFunction<TicketEntity>("{ENTITY DATA:([^}]+)}", (x, d) => x.GetCustomData(d), Resources.CustomFields);

            //ENTITIES
            RegisterFunction<Entity>("{ENTITY NAME}", (x, d) => x.Name);
            RegisterFunction<Entity>("{ENTITY BALANCE}", (x, d) => _accountDao.GetAccountBalance(x.AccountId).ToString("N", LocalSettings.Culture), "", x => x.AccountId > 0);
            RegisterFunction<Entity>("{ENTITY DATA:([^}]+)}", (x, d) => x.GetCustomData(d));
            RegisterFunction<Entity>("{ENTITY STATE:([^}]+)}", GetEntityState);
            RegisterFunction<Entity>("{ENTITY STATE QUANTITY:([^}]+)}", (x, d) => GetEntityStateQuantity(x, d).ToString(CultureInfo.InvariantCulture));

            //CALCULATIONS
            RegisterFunction<Calculation>("{CALCULATION NAME}", (x, d) => x.Name, string.Format(Resources.Name_f, Resources.Calculation));
            RegisterFunction<Calculation>("{CALCULATION AMOUNT}", (x, d) => x.Amount.ToString(LocalSettings.QuantityFormat), string.Format(Resources.Amount_f, Resources.Calculation));
            RegisterFunction<Calculation>("{CALCULATION TOTAL}", (x, d) => x.CalculationAmount.ToString("N", LocalSettings.Culture), string.Format(Resources.Total_f, Resources.Calculation), x => x.CalculationAmount != 0);

            //PAYMENTS
            //RegisterFunction<Payment>("{PAYMENT AMOUNT}", (x, d) => x.Amount.ToString(LocalSettings.CurrencyFormat), string.Format(Resources.Amount_f, Resources.Payment), x => x.Amount > 0);
            RegisterFunction<Payment>("{PAYMENT AMOUNT}", (x, d) => x.Amount.ToString("N", LocalSettings.Culture), string.Format(Resources.Amount_f, Resources.Payment), x => x.Amount != 0);  //Condition modified by Tim GU, from x => x.Amount > 0, in order to support Refund
            RegisterFunction(TagNames.PaymentVersActu, (x, d) => Instalment.VersActu.ToString("N", LocalSettings.Culture), "Payment Amount Current Instalment");
            RegisterFunction(TagNames.PaymentVersAnt, (x, d) => Instalment.VersAnt.ToString("N", LocalSettings.Culture), "Paid Amount Previous Instalments");
            RegisterFunction(TagNames.PaymentSolde, (x, d) => Instalment.Sold.ToString("N", LocalSettings.Culture), "Amount Due After Current Instalment");
            RegisterFunction<Payment>("{PAYMENT NAME}", (x, d) => x.Name, string.Format(Resources.Name_f, Resources.Payment));

            //CHANGE PAYMENTS
            RegisterFunction<ChangePayment>("{CHANGE PAYMENT AMOUNT}", (x, d) => x.Amount.ToString("N", LocalSettings.Culture), string.Format(Resources.Amount_f, Resources.ChangePayment), x => x.Amount > 0);
            RegisterFunction<ChangePayment>("{CHANGE PAYMENT NAME}", (x, d) => x.Name, string.Format(Resources.Name_f, Resources.ChangePayment));

            //TAXES
            //RegisterFunction<TaxValue>("{TAX AMOUNT}", (x, d) => x.TaxAmount.ToString(LocalSettings.CurrencyFormat), Resources.TaxAmount, x => x.TaxAmount >= 0);  //Modified by Tim GU from x => x.TaxAmount > 0
            RegisterFunction<TaxValue>("{TAX AMOUNT}", (x, d) => x.TaxAmount.ToString("N", LocalSettings.Culture), Resources.TaxAmount);  //Condition removed by Tim GU, in order to support Refund
            RegisterFunction<TaxValue>("{TAX RATE}", (x, d) => x.Amount.ToString(LocalSettings.QuantityFormat), Resources.TaxRate, x => x.Amount > 0);
            RegisterFunction<TaxValue>("{TAXABLE AMOUNT}", (x, d) => x.OrderAmount.ToString("N", LocalSettings.Culture), Resources.TaxableAmount, x => x.OrderAmount > 0);
            RegisterFunction<TaxValue>("{TOTAL TAXABLE AMOUNT}", (x, d) => x.TotalAmount.ToString("N", LocalSettings.Culture), Resources.TotalTaxableAmount, x => x.TotalAmount > 0);
            RegisterFunction<TaxValue>("{TAX NAME}", (x, d) => x.Name, string.Format(Resources.Name_f, Resources.TaxTemplate));

            //ACCOUNT TRANSACTON DOCUMENTS
            RegisterFunction<AccountTransactionDocument>("{DOCUMENT DATE}", (x, d) => x.Date.ToShortDateString(), "Document Date");
            RegisterFunction<AccountTransactionDocument>("{DOCUMENT TIME}", (x, d) => x.Date.ToShortTimeString(), "Document Time");
            RegisterFunction<AccountTransactionDocument>("{DESCRIPTION}", (x, d) => x.Name, "Document Description");
            RegisterFunction<AccountTransactionDocument>("{DOCUMENT BALANCE}", (x, d) => x.AccountTransactions.Sum(y => y.Balance).ToString("N", LocalSettings.Culture), "Document Balance");

            //ACCOUNT TRANSACTIONS
            RegisterFunction<AccountTransaction>("{DESCRIPTION}", (x, d) => x.Name, "Transaction Description");
            RegisterFunction<AccountTransaction>("{AMOUNT}", (x, d) => x.Amount.ToString("N", LocalSettings.Culture), "Transaction Amount");
            RegisterFunction<AccountTransaction>("{EXCHANGE RATE}", (x, d) => x.ExchangeRate.ToString(LocalSettings.QuantityFormat), "Transaction Exchange Rate");
            RegisterFunction<AccountTransaction>("{TRANSACTION TYPE NAME}", (x, d) => GetTransactionTypeName(x.AccountTransactionTypeId), "Transaction Type Name");

            RegisterFunction<AccountTransaction>("{SOURCE ACCOUNT TYPE}", (x, d) => GetAccountTypeName(x.SourceTransactionValue.AccountTypeId));
            RegisterFunction<AccountTransaction>("{SOURCE ACCOUNT}", (x, d) => GetAccountName(x.SourceTransactionValue.AccountId));
            RegisterFunction<AccountTransaction>("{SOURCE DEBIT}", (x, d) => x.SourceTransactionValue.Debit.ToString("N", LocalSettings.Culture));
            RegisterFunction<AccountTransaction>("{SOURCE CREDIT}", (x, d) => x.SourceTransactionValue.Credit.ToString("N", LocalSettings.Culture));
            RegisterFunction<AccountTransaction>("{SOURCE AMOUNT}", (x, d) => Math.Abs(x.SourceTransactionValue.Debit - x.SourceTransactionValue.Credit).ToString("N", LocalSettings.Culture));
            RegisterFunction<AccountTransaction>("{SOURCE BALANCE}", (x, d) => GetAccountBalance(x.SourceTransactionValue.AccountId).ToString("N", LocalSettings.Culture));

            RegisterFunction<AccountTransaction>("{TARGET ACCOUNT TYPE}", (x, d) => GetAccountTypeName(x.TargetTransactionValue.AccountTypeId));
            RegisterFunction<AccountTransaction>("{TARGET ACCOUNT}", (x, d) => GetAccountName(x.TargetTransactionValue.AccountId));
            RegisterFunction<AccountTransaction>("{TARGET DEBIT}", (x, d) => x.TargetTransactionValue.Debit.ToString("N", LocalSettings.Culture));
            RegisterFunction<AccountTransaction>("{TARGET CREDIT}", (x, d) => x.TargetTransactionValue.Credit.ToString("N", LocalSettings.Culture));
            RegisterFunction<AccountTransaction>("{TARGET AMOUNT}", (x, d) => Math.Abs(x.TargetTransactionValue.Debit - x.TargetTransactionValue.Credit).ToString("N", LocalSettings.Culture));
            RegisterFunction<AccountTransaction>("{TARGET BALANCE}", (x, d) => GetAccountBalance(x.TargetTransactionValue.AccountId).ToString("N", LocalSettings.Culture));
        }

        private string GenerateRandomNumber(int length, bool addCheckDigit)
        {
            var result = Utility.RandomString(length, "ABCDEFGHJKLMNPQRSTUVWZYZ123456789");
            if (addCheckDigit) result = Utility.GenerateCheckDigit(result) + result;
            return result;
        }

        private int SafeInt(string s, int defaultValue)
        {
            int result;
            return int.TryParse(s, out result) ? result : defaultValue;
        }

        private int GetEntityStateQuantity(Entity entity, string stateName)
        {
            return _entityService.GetStateQuantity(entity, stateName);
        }

        private string GetEntityState(Entity entity, string stateName)
        {
            return _entityService.GetStateValue(entity, stateName);
        }

        private string GexExchangeRate(string name)
        {
            var fc = _cacheService.GetForeignCurrencies()
                         .FirstOrDefault(y => y.Name == name);
            return fc != null ? fc.ExchangeRate.ToString("N", LocalSettings.Culture) : "";
        }

        private decimal GetAccountBalance(int accountId)
        {
            return _accountDao.GetAccountBalance(accountId);
        }

        private string GetAccountName(int accountId)
        {
            return _accountDao.GetAccountNameById(accountId);
        }

        private string GetAccountTypeName(int accountTypeId)
        {
            return _cacheService.GetAccountTypeById(accountTypeId).Name;
        }

        private string GetTransactionTypeName(int accountTransactionTypeId)
        {
            return _cacheService.GetAccountTransactionTypeById(accountTransactionTypeId).Name;
        }

        private string GetEntityFieldValue(Ticket ticket, string data)
        {
            if (!data.Contains(':')) return "";
            var parts = data.Split(':');
            var et = _cacheService.GetEntityTypeIdByEntityName(parts[0]);
            return ticket.GetEntityFieldValue(et, parts[1]);
        }

        private string GetEntityBalance(Ticket ticket, string data)
        {
            var parts = data.Split(':');
            var etId = _cacheService.GetEntityTypeIdByEntityName(parts[0]);
            var te = ticket.TicketEntities.FirstOrDefault(x => x.EntityTypeId == etId);
            if (te != null && te.AccountId > 0)
            {
                var balance = GetAccountBalance(te.AccountId);
                return balance.ToString("N", LocalSettings.Culture);
            }
            return "";
        }

        public void RegisterFunction(string tag, Func<object, string, string> function, string desc)
        {
            RegisterFunction<object>(tag, function, desc);
        }

        public void RegisterFunction<TModel>(string tag, Func<TModel, string, string> function, string desc = "", Func<TModel, bool> condition = null)
        {
            if (!Functions.ContainsKey(typeof(TModel)))
            {
                Descriptions.Add("-- " + UpperWhitespace(typeof(TModel).Name) + " Value Tags --", "");
                Functions.Add(typeof(TModel), new ArrayList());
            }
            Functions[typeof(TModel)].Add(new FunctionData<TModel> { Tag = tag, Func = function, Condition = condition });

            if (string.IsNullOrEmpty(desc))
            {
                desc = tag.Trim(new[] { '{', '}' });
                desc = desc.Replace(":([^}]+)", ":X}");
                desc = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(desc.ToLower());
            }

            if (!string.IsNullOrEmpty(desc))
            {
                var key = tag.Replace(":([^}]+)", ":X}");
                if (!Descriptions.ContainsKey(key))
                    Descriptions.Add(key, desc);
            }
        }

        private static string UpperWhitespace(string value)
        {
            return string.Join("", value.Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).Trim();
        }

        public string ExecuteFunctions<TModel>(string content, TModel model, PrinterTemplate printerTemplate)
        {
            content = Functions[typeof(object)]
                .Cast<FunctionData<object>>()
                .Aggregate(content, (current, func) => (func.GetResult(model, current, printerTemplate)));
            if (!Functions.ContainsKey(typeof(TModel))) return content;
            return Functions[typeof(TModel)]
                .Cast<FunctionData<TModel>>()
                .Aggregate(content, (current, func) => (func.GetResult(model, current, printerTemplate)));
        }

        private string GetDepartmentName(int departmentId)
        {
            var dep = _departmentService.GetDepartment(departmentId);
            return dep != null ? dep.Name : Resources.UndefinedWithBrackets;
        }

        private MenuItem GetMenuItem(int menuItemId)
        {
            var mi = _cacheService.GetMenuItem(x => x.Id == menuItemId);
            return mi ?? (MenuItem.All);
        }
        
    }
}
