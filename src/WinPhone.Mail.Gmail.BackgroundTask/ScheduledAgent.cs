
// #define DEBUG_AGENT

using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
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
            // This is invoked at most every 30 minutes. It will run for at most 25 seconds.
            // We are limited to 20mb of RAM (or 11 on low-end devices).
            // http://msdn.microsoft.com/en-us/library/windowsphone/develop/hh202942(v=vs.105).aspx
            //
            // All this means that we have to sync mail very efficiently.
            // - Download messages and save them to disk one at a time rather than keeping the whole
            //   list in memory.
            // - Download messages in parts and save them seperately (Flags, headers, labels,
            //   body parts / attachments).  This also means we don't have to keep loading and re-saving
            //   messages when only the labels or flags change.
            // - Consider streaming larger sections directly to disk.
            // - Watch the time, don't start a new operation if we're running low. Getting cut off in 
            //   the middle can leave us with corrupted data on disk. If there's too much mail we can
            //   download more of it next time. Use a CancelationToken w/CancelAfter to track the time.
            //   When called from the UI the CancelationToken can be used for a manual cancel instead.
            //   Check the time before any save operation. Save only at points where a later sync could resume.
            //   It doesn't matter if network operations get interupted, and it's harder to guess how long they'll take.
            //

            CancellationTokenSource cts = new CancellationTokenSource();
            try
            {
#if DEBUG_AGENT
                cts.Token.Register(() =>
                {
                        ShellToast toast = new ShellToast();
                        toast.Title = "Gmail";
                        toast.Content = "Task cancelled.";
                        toast.Show();
                });
#endif

                if (task is PeriodicTask)
                {
#if DEBUG_AGENT
                    ShellToast toast = new ShellToast();
                    toast.Title = "Gmail";
                    toast.Content = "Periodic-task running.";
                    toast.Show();
#endif
                    // Time limit 25 seconds, round down to avoid corrupting data on disk.
                    // Give us time to do notifications, even if it's only for partial data.
                    cts.CancelAfter(TimeSpan.FromSeconds(20));
                }
                else
                {
                    // TODO: Consider scheduling a resource intensive task for a full daily sync.
                    // Time limit 10 minutes, round down to avoid corrupting data on disk.
                    cts.CancelAfter(new TimeSpan(0, 9, 45));
#if DEBUG_AGENT
                    ShellToast toast = new ShellToast();
                    toast.Title = "Gmail";
                    toast.Content = "Resource-intensive task running.";
                    toast.Show();
#endif
                }

                AccountManager accountManager = new AccountManager();
                Tuple<int, bool> syncResults = await accountManager.SyncAllMailAsync(cts.Token);
                int newMailCount = syncResults.Item1;
                bool notify = syncResults.Item2;

                if (task is ResourceIntensiveTask)
                {
                    // TODO: Verbose GC messages no longer referenced by any label.
                }

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