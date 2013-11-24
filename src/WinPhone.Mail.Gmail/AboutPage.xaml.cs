using Microsoft.Phone.Controls;
using System.Reflection;
using System.Windows.Navigation;

namespace WinPhone.Mail.Gmail
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