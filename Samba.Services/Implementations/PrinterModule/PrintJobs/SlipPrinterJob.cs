using System.Linq;
using System.Windows.Documents;
using Samba.Domain.Models.Settings;
using Samba.Services.Implementations.PrinterModule.Formatters;
using Samba.Services.Implementations.PrinterModule.Tools;
using System.Management;
using System;

namespace Samba.Services.Implementations.PrinterModule.PrintJobs
{
    public class SlipPrinterJob : AbstractPrintJob
    {
        public SlipPrinterJob(Printer printer)
            : base(printer)
        {
        }

        public override void DoPrint(string[] lines)
        {
            var printer = new LinePrinter(Printer.ShareName, Printer.CharsPerLine, Printer.CodePage);
            printer.StartDocument();

            var formatters = new FormattedDocument(lines, Printer.CharsPerLine).GetFormatters().ToList();
            foreach (var formatter in formatters)
            {
                SendToPrinter(printer, formatter);
            }
            if (formatters.Count() > 1)
                printer.Cut();
            printer.EndDocument();
        }

        public override void DoPrint(FlowDocument document)
        {
            DoPrint(PrinterTools.FlowDocumentToSlipPrinterFormat(document, Printer.CharsPerLine));
        }

        private static void SendToPrinter(LinePrinter printer, ILineFormatter line)
        {
            var data = line.GetFormattedLine();

            if (!data.StartsWith("<"))
                printer.WriteLine(data, line.FontHeight, line.FontWidth, LineAlignment.Left);
            else if (line.Tag.TagName == "eb")
                printer.EnableBold();
            else if (line.Tag.TagName == "db")
                printer.DisableBold();
            else if (line.Tag.TagName == "ec")
                printer.EnableCenter();
            else if (line.Tag.TagName == "el")
                printer.EnableLeft();
            else if (line.Tag.TagName == "er")
                printer.EnableRight();
            else if (line.Tag.TagName == "bmp")
                printer.PrintBitmap(RemoveTag(data));
            else if (line.Tag.TagName == "qr")
                printer.PrintQrCode(RemoveTag(data), line.FontHeight, line.FontWidth);
            else if (line.Tag.TagName == "bar")
                printer.PrintBarCode(RemoveTag(data), line.FontHeight, line.FontWidth);
            else if (line.Tag.TagName == "cut")
                printer.Cut();
            else if (line.Tag.TagName == "beep")
                printer.Beep();
            else if (line.Tag.TagName == "drawer")
                printer.OpenCashDrawer();
            else if (line.Tag.TagName == "b")
                printer.Beep((char)line.FontHeight, (char)line.FontWidth);
            else if (line.Tag.TagName == ("xct"))
                printer.ExecCommand(RemoveTag(data));
        }

        private bool IsPrinterOnline()
        {
            bool bRet = true;

            ConnectionOptions oCon = new ConnectionOptions();
            ManagementScope oMs = new ManagementScope(@"\root\cimv2", oCon);
            ObjectQuery oQuery = new ObjectQuery("select PrinterState from Win32_Printer where Name like \"EPSON % Receipt\"");
            ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(oMs, oQuery);
            ManagementObjectCollection oReturnCollection = oSearcher.Get();

            if(oReturnCollection.Count > 0)
            {
                int status = -1;

                foreach (ManagementObject oReturn in oReturnCollection)
                {
                    //PrinterState returns a hex value but we use a string/int value
                    //
                    // state value
                    //------------------------------------------------
                    // Online 0
                    // Lid Open 4194432
                    // Out of paper 144
                    // Out of paper/Lid open 4194448
                    // Printing 1024
                    // Initializing 32768
                    // Manual Feed in Progress 160
                    // Offline 4096
                    status = Convert.ToInt32(oReturn["PrinterState"]);
                    bRet &= status == 0 || status == 1024 || status == 32768;
                }
            }

            return bRet;            
        }
    }
}
