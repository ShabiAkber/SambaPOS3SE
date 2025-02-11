using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Samba.Infrastructure.ExceptionReporter;

namespace Samba.Domain.Models.Settings
{
    public static class SRSState   //This entire class is added by Tim GU
    {
        public static string SRSTransactionTime {  get; set; }
        public static string TransactionServer {  get; set; }
        public static string PaymentMethod {  get; set; }
        public static string PaymentStatus { get; set; }        
        public static string AdditionalInfo { get; set; } = string.Empty;
        public static string PrintCopyType { get; set; } = string.Empty;
        public static string PSIQRString { get; set; }
        public static string PSITransactionDate { get; set;}
        public static string PSITransactionNumber { get; set;}
        public static string SRSDeviceIdapprl { get; set; } = "0000-0000-0000";

        public static class PaymentMethodLableList
        {
            public const string CASH_PAYMENT = "ARGENT COMPTANT";
            public const string CREDIT_CARD = "CARTE DE CRÉDIT";
            public const string DEBIT_CARD = "CARTE DE DÉBIT";
            public const string CHECK = "CHÈQUE";
            public const string GIFT_CARD = "CERTIFICAT-CADEAU";
            public const string CHARGE_TO_ACCOUNT = "IMPUTER AU COMPTE";
            public const string MIXED_PAYMENT = "PAIEMENT MIXTE";
            public const string CRYPTO_CURRENCY = "CRYPTOMONNAIE";
            public const string LOYALTY_PROGRAM = "PROGRAMME DE FIDÉLISATION";
            public const string E_TRANSFER = "TRANSFERT ÉLECTRONIQUE DE FONDS";
            public const string DIGITAL_WALLET = "PORTEFEUILLE ÉLECTRONIQUE";
            public const string OTHER_PAYMENT_METHOD = "AUTRE MODE DE PAIEMENT";
            public const string NO_PAYMENT = "AUCUN PAIEMENT";
            public const string COUPON = "COUPON";
        }

        static SRSState()
        {
            if(SRSDeviceIdapprl == "0000-0000-0000")
            {
                try
                {
                    using (RegistryKey rke = Registry.CurrentUser.CreateSubKey(@"Software\Allagma\SRS"))
                    {
                        SRSDeviceIdapprl = rke.GetValue("IDAPPRL", "0000-0000-0000").ToString();
                        rke.Close();
                    }
                }
                catch (Exception)
                {
                }
            }            
        }

        public static void Clear()
        {
            SRSTransactionTime = string.Empty;
            TransactionServer = string.Empty;
            PaymentMethod = string.Empty;
            PaymentStatus = string.Empty;
            AdditionalInfo = string.Empty;
            PrintCopyType = string.Empty;
            PSIQRString = string.Empty;
            PSITransactionDate = string.Empty;
            PSITransactionNumber = string.Empty;
        }
    }
}
