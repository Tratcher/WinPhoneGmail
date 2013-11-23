using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Reflection;

namespace WinPhone.Mail
{
    public partial class AboutPage : PhoneApplicationPage
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Set version:
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            object[] objs = currentAssembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
            AssemblyFileVersionAttribute attrib = (AssemblyFileVersionAttribute)objs[0];
            VersionField.Text = attrib.Version.ToString();

            base.OnNavigatedTo(e);
        }
    }
}