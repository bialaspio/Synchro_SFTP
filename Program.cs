using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using WinSCP;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

class Example
{
    public static async Task<int> Main()
    {
        try
        {
            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("appsettings.json"));

            Console.WriteLine("Synchronzacja Goleniów\n");
            int sent_mail;
            var teraz = DateTime.Now.TimeOfDay;

            while (true)
            {
                await Task.Delay(1000);
                teraz = DateTime.Now.TimeOfDay;
                sent_mail = 0;

                if (teraz.Hours == 5 && teraz.Minutes == 43 && teraz.Seconds == 2)
                {
                    DateTime teraz_mail = DateTime.Now;
                    string mail_body = "Synchronzacja Goleniów z " + teraz_mail + "\n";
                    Console.WriteLine("Ostatnia synchronizacja plików :" + teraz_mail + "\n");

                    foreach (var ftpSetting in config.FtpSettings)
                    {
                        await PobierzDaneAsync(ftpSetting.Host, ftpSetting.User, ftpSetting.Password, ftpSetting.LocalPath, ftpSetting.RemotePath, config.SshHostKeyFingerprint, mail_body, sent_mail);
                    }

                    if (sent_mail > 1)
                    {
                        var email = new MimeMessage();
                        email.From.Add(MailboxAddress.Parse(config.EmailSettings.FromEmail));
                        foreach (var toEmail in config.EmailSettings.ToEmails)
                        {
                            email.To.Add(MailboxAddress.Parse(toEmail));
                        }
                        email.Subject = "Synchronizacja Goleniów";
                        email.Body = new TextPart(TextFormat.Plain) { Text = mail_body };

                        using var smtp = new SmtpClient();
                        await smtp.ConnectAsync(config.EmailSettings.SmtpServer, config.EmailSettings.SmtpPort, SecureSocketOptions.SslOnConnect);
                        await smtp.AuthenticateAsync(config.EmailSettings.SmtpUser, config.EmailSettings.SmtpPassword);
                        await smtp.SendAsync(email);
                        await smtp.DisconnectAsync(true);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: {0}", e);
            return 1;
        }
    }

    public static async Task PobierzDaneAsync(string host, string user, string paswd, string path_local, string path_FTP, string sshHostKeyFingerprint, string mail_body, int sent_mail)
    {
        int flaga_con = 0;
        var sessionOptions = new SessionOptions
        {
            Protocol = Protocol.Sftp,
            HostName = host,
            UserName = user,
            Password = paswd,
            SshHostKeyFingerprint = sshHostKeyFingerprint,
        };

        using (var session_m = new Session())
        {
            try
            {
                await Task.Run(() => session_m.Open(sessionOptions));
                flaga_con = 1;
                mail_body += "\n\nPobrane pliki:\n";
            }
            catch (Exception e)
            {
                flaga_con = 0;
                Console.WriteLine("Error: {0}", e);
                mail_body += "NIE UDAŁO SIE POŁĄCZYĆ Z SERWEREM FTP\n";
            }

            if (flaga_con == 1)
            {
                try
                {
                    var synchronizationResult = await Task.Run(() => session_m.SynchronizeDirectories(SynchronizationMode.Local, path_local, path_FTP, false, false, SynchronizationCriteria.Size));
                    mail_body += "\t" + user + "\n";
                    synchronizationResult.Check();

                    foreach (var a in synchronizationResult.Downloads)
                    {
                        sent_mail++;
                        Console.WriteLine(a.Destination);
                        mail_body += "\t" + a.Destination + "\n";
                    }
                }
                catch
                {
                    mail_body += "ERR - pobieranie z :" + user + "\n";
                    Console.WriteLine("ERR - pobieranie z:" + user);
                }
            }
        }
    }

    public static async Task WyslijDaneAsync(string host, string user, string paswd, string path_local_1, string path_local_2, string path_FTP, string sshHostKeyFingerprint, string mail_body, int sent_mail)
    {
        int flaga_con = 0;
        var sessionOptions = new SessionOptions
        {
            Protocol = Protocol.Sftp,
            HostName = host,
            UserName = user,
            Password = paswd,
            SshHostKeyFingerprint = sshHostKeyFingerprint,
        };

        using (var session_m_upload = new Session())
        {
            try
            {
                await Task.Run(() => session_m_upload.Open(sessionOptions));
                flaga_con = 1;
                mail_body += "\n\nWyslane pliki:\n";
            }
            catch (Exception e)
            {
                flaga_con = 0;
                Console.WriteLine("Error: {0}", e);
                mail_body += "NIE UDAŁO SIE POŁĄCZYĆ Z SERWEREM FTP\n";
            }

            if (flaga_con == 1)
            {
                try
                {
                    var transferOptions = new TransferOptions { PreserveTimestamp = false };
                    var synchronizationResult = await Task.Run(() => session_m_upload.SynchronizeDirectories(SynchronizationMode.Remote, path_local_1, path_FTP, false, false, SynchronizationCriteria.Size, transferOptions));
                    mail_body += "\tDo " + user + ":\n";
                    mail_body += "\t\tZ " + path_local_1 + ":\n";
                    synchronizationResult.Check();

                    foreach (var a in synchronizationResult.Uploads)
                    {
                        sent_mail++;
                        Console.WriteLine(a.Destination);
                        mail_body += "\t\t" + a.Destination + "\n";
                    }

                    mail_body += "\n\t\tZ " + path_local_2 + ":\n";
                    synchronizationResult = await Task.Run(() => session_m_upload.SynchronizeDirectories(SynchronizationMode.Remote, path_local_2, path_FTP, false, false, SynchronizationCriteria.Size, transferOptions));
                    synchronizationResult.Check();

                    foreach (var a in synchronizationResult.Uploads)
                    {
                        sent_mail++;
                        Console.WriteLine(a.Destination);
                        mail_body += "\t\t" + a.Destination + "\n";
                    }
                }
                catch
                {
                    mail_body += "ERR - wyslanie do :" + user + "\n";
                    Console.WriteLine("ERR - wyslanie do :" + user);
                }
            }
        }
    }

}

public class Config
{
    public required FtpSetting[] FtpSettings { get; set; }
    public required EmailSettings EmailSettings { get; set; }
    public required string SshHostKeyFingerprint { get; set; }
}

public class FtpSetting
{
    public required string Host { get; set; }
    public required string User { get; set; }
    public required string Password { get; set; }
    public required string LocalPath { get; set; }
    public required string RemotePath { get; set; }
}

public class EmailSettings
{
    public required string SmtpServer { get; set; }
    public required int SmtpPort { get; set; }
    public required string SmtpUser { get; set; }
    public required string SmtpPassword { get; set; }
    public required string FromEmail { get; set; }
    public required string[] ToEmails { get; set; }
}

