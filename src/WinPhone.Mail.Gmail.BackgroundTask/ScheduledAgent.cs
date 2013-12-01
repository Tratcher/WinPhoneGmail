
// #define DEBUG_AGENT

using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using System;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using WinPhone.Mail.Gmail.Shared;
using WinPhone.Mail.Gmail.Shared.Accounts;

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
        protected override async void OnInvoke(ScheduledTask task)
        {
            try
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


                AccountManager accountManager = new AccountManager();
                int newMailCount = 0;
                foreach (Account account in accountManager.Accounts)
                {
                    // TODO: Check the sync schedule to see if it's time to perform a sync

                    // Check for new mail

                    // TODO: Sync all labels. For now we'll just sync the inbox.
                    Label label = await account.GetLabelAsync(forceSync: true);

                    // TODO: What is the official way to count new mail?
                    // The number of unread messages that have arrived that have a date later than when we last opened the app.
                    newMailCount += label.Conversations.Where(conversation => conversation.HasUnread).Count();

                    await account.LogoutAsync();
                }

                if (newMailCount > 0)
                {
                    // Launch a toast to show that the agent is running.
                    // The toast will not be shown if the foreground application is running.
                    ShellToast toast = new ShellToast();
                    toast.Title = "New messages are available";
                    toast.Content = "You have " + newMailCount + " new messages";
                    toast.Show();
                }
#if DEBUG_AGENT
                else
                {
                    ShellToast toast = new ShellToast();
                    toast.Title = "No new messages";
                    toast.Content = "You have " + newMailCount + " new messages";
                    toast.Show();
                }
#endif

                // Update the live tile
                ShellTile tile = ShellTile.ActiveTiles.FirstOrDefault();
                if (tile != null)
                {
                    IconicTileData data = new IconicTileData();
                    data.Count = newMailCount;
                    tile.Update(data);

                    // TODO: Include message snyppits for new mail on large tile.
                }

                // TODO: Update lock screen count, content

                // If debugging is enabled, launch the agent again in one minute.
#if DEBUG_AGENT
                ScheduledActionService.LaunchForTest(task.Name, TimeSpan.FromSeconds(60));
#endif
            }
            catch(Exception ex)
            {
                ShellToast toast = new ShellToast();
                toast.Title = ex.GetType().Name;
                toast.Content = ex.Message;
                toast.Show();

                throw;
            }

            NotifyComplete();
        }
    }
}