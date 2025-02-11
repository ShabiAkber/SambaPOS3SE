using System;
using System.Linq;
using Samba.Localization.Properties;

namespace Samba.Modules.SettingsModule.BrowserViews
{
    class SambaPosWiki : BrowserViewModel
    {
        public SambaPosWiki()
        {
            Header = string.Format("SambaPOSSE {0}", Resources.Wiki);
            Url = "";
        }
    }
}
