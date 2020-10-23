using System;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading.Tasks;

namespace spicam.utils
{
    /// <summary>
    /// Utilities for sending and throttling email notifications.
    /// </summary>
    public static class MailClient
    {
        /// <summary>
        /// When the mail server imposes a 24-hour message-count restriction, this tracks the
        /// 24 hour period (which is only accurate if spicam has been running continuously).
        /// GMail in particular has a 100 email limit for a 24 hour period.
        /// </summary>
        public static DateTime DailyLimitBaseline { get; private set; } = DateTime.Now;

        /// <summary>
        /// The number of emails sent since <see cref="DailyLimitBaseline"/>.
        /// </summary>
        public static int EmailCountToday { get; private set; } = 0;

        /// <summary>
        /// Used to implement the notification cooldown period.
        /// </summary>
        public static DateTime LastEmailSent { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Sends a notification email with snapshot attachments after applying any required
        /// throttle settings. Also moves any snapshots to storage.
        /// </summary>
        public static void ProcessNotifications(string detectionTimestamp)
        {
            if (!ShouldSend())
            {
                // If we aren't sending a message, move the snapshots to storage
                FileProcessing.MoveSnapshotsToStorage();
                return;
            }

            _ = Task.Run(async () =>
            {
                var cfg = AppConfig.Get;
                var email = AppConfig.Get.Email;
                try
                {
                    var snapshots = Directory.GetFiles(cfg.LocalPath, "*.jpg");

                    // Even though Microsoft recommends against using the framework SmtpClient class,
                    // it's adequate for our use hitting a simple unsecured purely-local SMTP relay
                    // to a more secure public-Internet mail server such as GMail.

                    // Prep the basic message
                    using var smtp = new SmtpClient(email.Server, email.Port);
                    var from = new MailAddress(email.From, email.From);
                    var to = new MailAddress(email.To, email.To);
                    var message = new MailMessage(from, to);
                    message.Subject = $"Motion: {cfg.Name}";
                    message.Body = $"Motion detected by spicam {cfg.Name} at {detectionTimestamp}.";

                    // Add any snapshot attachments
                    foreach (var snapshot in snapshots)
                    {
                        var attachment = new Attachment(snapshot, MediaTypeNames.Image.Jpeg);
                        attachment.ContentDisposition.CreationDate = new FileInfo(snapshot).CreationTime;
                        message.Attachments.Add(attachment);
                    }

                    // Send the email
                    await smtp.SendMailAsync(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nException of type {ex.GetType().Name} sending notification email\n{ex.Message}");
                    if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                    Console.WriteLine($"\n{ex.StackTrace}");
                }
                finally
                {
                    // Move the snapshots to storage after the message is sent
                    FileProcessing.MoveSnapshotsToStorage();
                }
            });
        }

        /// <summary>
        /// Checks various restrictions.
        /// </summary>
        private static bool ShouldSend()
        {
            var mail = AppConfig.Get.Email;

            if (string.IsNullOrWhiteSpace(mail.Server)) return false;

            if (mail.CooldownSeconds > 0 && (DateTime.Now - LastEmailSent).TotalSeconds < mail.CooldownSeconds) return false;
            
            if (mail.Max24Hrs > 0)
            {
                if ((DateTime.Now - DailyLimitBaseline).TotalHours >= 24)
                {
                    DailyLimitBaseline = DailyLimitBaseline.AddHours(24);
                    EmailCountToday = 0;
                }
                else
                {
                    if (EmailCountToday > mail.Max24Hrs) return false;
                }
            }

            return true;
        }

    }
}
