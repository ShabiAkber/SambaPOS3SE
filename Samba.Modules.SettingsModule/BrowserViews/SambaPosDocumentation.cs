using System;
using System.Linq;
using Samba.Localization.Properties;

namespace Samba.Modules.SettingsModule.BrowserViews
{
    class SambaPosDocumentation : BrowserViewModel
    {
        public SambaPosDocumentation()
        {
            Header = string.Format("SambaPOSSE {0}", Resources.Documentation);
            Url = "";
        }
    }
}
