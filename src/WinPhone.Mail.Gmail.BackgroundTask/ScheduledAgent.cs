
// #define DEBUG_AGENT

using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using System;
using System.Diagnostics;
using System.Windows;

namespace WinPhone.Mail.Gmail.BackgroundTask
{
    public class ScheduledAgent : ScheduledTaskAgent
    {
        /// <remarks>
        /// ScheduledAgent constructor, initializes the UnhandledException handler
        /// </remarks>
        static ScheduledAgent()
        {
            // Subscribe to the managed exception handler
            Deployment.Current.Dispatcher.BeginInvoke(delegate
            {
                Application.Current.UnhandledException += UnhandledException;
            });
        }

        /// Code to execute on Unhandled Exceptions
        private static void UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        /// <summary>
        /// Agent that runs a scheduled task
        /// </summary>
        /// <param name="task">
        /// The invoked task
        /// </param>
        /// <remarks>
        /// This method is called when a periodic or resource intensive task is invoked
        /// </remarks>
        protected override void OnInvoke(ScheduledTask task)
        {
            /*
            string toastMessage = "";

            // If your application uses both PeriodicTask and ResourceIntensiveTask
            // you can branch your application code here. Otherwise, you don't need to.
            if (task is PeriodicTask)
            {
                // Execute periodic task actions here.
                toastMessage = "Periodic task running.";
            }
            else
            {
                // TODO: Consider scheduling a resource intensive task for a daily sync of historical mail.

                // Execute resource-intensive task actions here.
                toastMessage = "Resource-intensive task running.";
            }*/

            // TODO: Check the sync schedule to see if it's time to perform a sync

            // TODO: Check for new mail
            // TODO: Extract the Account and Storage components into a shared library that can be called from the
            // scheduled task and the main app.
            // http://www.31a2ba2a-b718-11dc-8314-0800200c9a66.com/2011/11/simple-wp7-mango-app-for-background.html
                        
            // Launch a toast to show that the agent is running.
            // The toast will not be shown if the foreground application is running.
            ShellToast toast = new ShellToast();
            toast.Title = "New messages are available";
            toast.Content = "You have 3 new messages";
            toast.Show();

            // TODO: Update the live tile

            // If debugging is enabled, launch the agent again in one minute.
#if DEBUG_AGENT
  ScheduledActionService.LaunchForTest(task.Name, TimeSpan.FromSeconds(60));
#endif

            NotifyComplete();
        }
    }
}