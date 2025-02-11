using System;
using System.Linq;
using Samba.Localization.Properties;

namespace Samba.Modules.SettingsModule.BrowserViews
{
    class SambaPosForum : BrowserViewModel
    {
        public SambaPosForum()
        {
            Header = string.Format("SambaPOSSE {0}", Resources.Forum);
            Url = "";
        }
    }
}
