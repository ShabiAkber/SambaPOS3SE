using System;
using System.Linq;
using Samba.Localization.Properties;

namespace Samba.Modules.SettingsModule.BrowserViews
{
    class SambaPosWebsite : BrowserViewModel
    {
        public SambaPosWebsite()
        {
            Header = string.Format("SambaPOSSE {0}", Resources.Website);
            Url = "";
        }
    }
}
