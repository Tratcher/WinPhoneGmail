
// #define DEBUG_AGENT

using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using System;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using WinPhone.Mail.Gmail.Shared;
using WinPhone.Mail.Gmail.Shared.Accounts;
using WinPhone.Mail.Gmail.Shared.Storage;
using WinPhone.Mail.Protocols.Gmail;

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
                bool notify = false;
                DateTime lastAppActivationTime = AppSettings.LastAppActivationTime;
                foreach (Account account in accountManager.Accounts)
                {
                    // TODO: Per label frequency?
                    if (account.Info.Frequency == Constants.Sync.Manual)
                    {
                        continue;
                    }

                    int accountNewMail = 0;
                    // TODO: Messages may be double counted across labels. Deduplicate by GUID?
                    foreach (LabelInfo labelInfo in await account.GetLabelsAsync(forceSync: false))
                    {
                        if (labelInfo.Store)
                        {
                            // Check the sync schedule to see if it's time to perform a sync
                            bool forceSync = account.Info.Frequency < DateTime.Now - labelInfo.LastSync;

                            // Get the messages, from online or storage
                            Label label = await account.GetLabelAsync(forceSync);

                            // We count messages because we want to be notified if new messages arrive on the same conversation.
                            // The number of unread messages that have arrived with a date later than when we last opened the app.
                            foreach (ConversationThread conversation in label.Conversations)
                            {
                                accountNewMail += conversation.Messages
                                        .Where(message => !message.Seen)
                                        .Where(message => message.Date > lastAppActivationTime)
                                        .Count();
                            }
                        }
                    }

                    int accountPriorNewMail = account.Info.NewMailCount;
                    account.Info.NewMailCount = accountNewMail;

                    if (account.Info.Notifications == NotificationOptions.FirstOnly
                        && accountPriorNewMail == 0 && accountNewMail > 0)
                    {
                        notify = true;
                    }
                    // TODO: This check is inaccurate if we go on another system and read some messages and then receive more. (e.g. -2, +2)
                    else if (account.Info.Notifications == NotificationOptions.Always
                        && accountNewMail > accountPriorNewMail)
                    {
                        notify = true;
                    }

                    newMailCount += accountNewMail;

                    await account.LogoutAsync();
                }

                accountManager.SaveAccounts();

                if (notify)
                {
                    // The toast will not be shown if the foreground application is running.
                    ShellToast toast = new ShellToast();
                    toast.Title = "Gmail";
                    toast.Content = "You have " + newMailCount + " new messages";
                    toast.Show();
                }
#if DEBUG_AGENT
                else
                {
                    ShellToast toast = new ShellToast();
                    toast.Title = "Gmail";
                    toast.Content = "You still have " + newMailCount + " new messages";
                    toast.Show();
                }
#endif

                // Update the live tile and lock screen
                ShellTile tile = ShellTile.ActiveTiles.FirstOrDefault();
                if (tile != null)
                {
                    IconicTileData data = new IconicTileData();
                    data.Count = newMailCount;
                    tile.Update(data);

                    // TODO: Include message snyppits for new mail on large tile and lock screen.
                }

#if DEBUG_AGENT
                // If debugging is enabled, launch the agent again in one minute.
                ScheduledActionService.LaunchForTest(task.Name, TimeSpan.FromSeconds(60));
#endif
            }
            catch(Exception ex)
            {
#if DEBUG
                ShellToast toast = new ShellToast();
                toast.Title = ex.GetType().Name;
                toast.Content = ex.Message;
                toast.Show();
#else
                throw;
#endif
            }

            NotifyComplete();
        }
    }
}