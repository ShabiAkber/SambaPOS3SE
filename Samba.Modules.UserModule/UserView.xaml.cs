using System;
using System.Text;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using Samba.Infrastructure.ExceptionReporter;

namespace Samba.Modules.UserModule
{
    /// <summary>
    /// Interaction logic for UserView.xaml
    /// </summary>
    public partial class UserView : UserControl
    {
        public UserView()
        {
            InitializeComponent();
            PasswordTextBox.GotFocus += PasswordTextBoxGotFocus;
        }

        void PasswordTextBoxGotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if(PasswordTextBox.Text.Contains("*"))
            PasswordTextBox.Clear();
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (UserNameTextBox.Text.Trim() == string.Empty)   
                return;
        }
    }
}
