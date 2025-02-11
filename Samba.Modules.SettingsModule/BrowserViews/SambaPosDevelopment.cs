using System;
using System.Linq;
using Samba.Localization.Properties;

namespace Samba.Modules.SettingsModule.BrowserViews
{
    class SambaPosDevelopment : BrowserViewModel
    {
        public SambaPosDevelopment()
        {
            Header = string.Format("SambaPOSSE {0}", Resources.Development);
            Url = "";
        }
    }
}
