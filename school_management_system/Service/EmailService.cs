using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace school_management_system.Service;




public class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    /// <summary>StartTls (port 587, default), Ssl (port 465) or None (local test servers only).</summary>
    public string Security { get; set; } = "StartTls";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Scholar SHS";
    /// <summary>Public address of the site, used for the sign-in link in emails.</summary>
    public string LoginUrl { get; set; } = "https://localhost:5001";
}
public record MailResult(bool Sent, string Message);

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string html, string text);
}

public class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{

    public async Task SendAsync(string to, string subject, string html, string text)
    {
        var o = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();

        var security = o.Security.ToLowerInvariant() switch
        {
            "ssl" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls
        };

        using var client = new SmtpClient { Timeout = 15000 };
        await client.ConnectAsync(o.Host, o.Port, security);
        if (!string.IsNullOrWhiteSpace(o.Username)) await client.AuthenticateAsync(o.Username, o.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

}

public class CredentialMailer(IEmailSender sender, IOptions<EmailOptions> options, ILogger<CredentialMailer> log)
{
    public Task<MailResult> SendAccountAsync(string? to, AccountCredentials c, string role, bool isReset = false)
    {
        var o = options.Value;
        Func<string, string> h = Enc;
        var link = LoginLink();
        var idLabel = role == "student" ? "Student ID" : "Teacher ID";
        var subject = isReset ? "Your new Scholar SHS password" : "Your Scholar SHS account";
        var intro = isReset ? "A new temporary password was created for your account." : $"An account has been created for you as a {role}.";

        var text = $"""
            Hello {c.FullName},

            {intro}

            {idLabel}: {c.Number}
            Sign-in email: {c.Email}
            Temporary password: {c.Password}

            Sign in here: {link}
            You will be asked to choose a new password the first time you sign in.
            If you were not expecting this message, please contact the school office.
            """;
        var html = Wrap($"""
            <p>Hello {h(c.FullName)},</p>
            <p>{h(intro)}</p>
            {Table((idLabel, c.Number), ("Sign-in email", c.Email), ("Temporary password", c.Password))}
            <p><a href="{h(link)}">Sign in to Scholar SHS</a></p>
            <p>You will be asked to choose a new password the first time you sign in.</p>
            """);
        return TrySendAsync(to, subject, html, text);
    }

    public Task<MailResult> SendHallMasterAsync(string? to, string fullName, string hallName, string loginEmail, string? tempPassword)
    {
        Func<string, string> h = Enc;
        var link = LoginLink();
        var subject = $"You are now hall master of {hallName}";
        var passwordLine = tempPassword != null ? $"Temporary password: {tempPassword}" : "Password: your current password is unchanged.";

        var text = $"""
            Hello {fullName},

            You have been assigned as hall master of {hallName}.

            Sign-in email: {loginEmail}
            {passwordLine}

            Sign in here: {link}
            Sign out and in again if you are already signed in, then open the Hall Master portal from the sidebar.
            {(tempPassword != null ? "You will be asked to choose a new password when you sign in." : "")}
            """;
        var rows = new List<(string, string)> { ("Sign-in email", loginEmail) };
        rows.Add(tempPassword != null ? ("Temporary password", tempPassword) : ("Password", "Your current password is unchanged"));
        var html = Wrap($"""
            <p>Hello {h(fullName)},</p>
            <p>You have been assigned as hall master of <b>{h(hallName)}</b>.</p>
            {Table(rows.ToArray())}
            <p><a href="{h(link)}">Sign in to Scholar SHS</a></p>
            <p>If you are already signed in, sign out and in again, then open the Hall Master portal from the sidebar.
            {(tempPassword != null ? "You will be asked to choose a new password when you sign in." : "")}</p>
            """);
        return TrySendAsync(to, subject, html, text);
    }
    private async Task<MailResult> TrySendAsync(string? to, string subject, string html, string text)
    {
        if (!options.Value.Enabled)
            return new MailResult(false, "Email is not switched on, so nothing was emailed. Give these details to the person directly.");
        if (string.IsNullOrWhiteSpace(to))
            return new MailResult(false, "No email address is on file for this person, so nothing was emailed. Give these details to them directly.");
        if (!MailboxAddress.TryParse(to.Trim(), out _))
            return new MailResult(false, $"“{to}” is not a valid email address, so nothing was emailed.");

        try
        {
            await sender.SendAsync(to.Trim(), subject, html, text);
            return new MailResult(true, $"The details were emailed to {to.Trim()}.");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not send credential email to {Recipient}", to);   // never log the message body
            var reason = ex.Message.Length > 140 ? ex.Message[..140] + "…" : ex.Message;
            return new MailResult(false, $"The email could not be sent ({reason}). Give these details to the person directly.");
        }
    }

    private static string Enc(string v) => WebUtility.HtmlEncode(v);

    private string LoginLink() => options.Value.LoginUrl.TrimEnd('/') + "/Account/Login";

    private static string Table(params (string Label, string Value)[] rows)
    {
        Func<string, string> h = Enc;
        var body = string.Concat(rows.Select(r =>
            $"<tr><td style=\"padding:6px 14px 6px 0;color:#666\">{h(r.Label)}</td><td style=\"padding:6px 0\"><b style=\"font-family:Consolas,monospace\">{h(r.Value)}</b></td></tr>"));
        return $"<table style=\"border-collapse:collapse;margin:12px 0\">{body}</table>";
    }

    private static string Wrap(string inner) =>
        $"<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#1f2328;max-width:560px\">{inner}" +
        "<p style=\"color:#888;font-size:12px;margin-top:24px\">Scholar SHS. If you were not expecting this message, please contact the school office.</p></div>";




}