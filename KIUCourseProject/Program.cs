using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    private static readonly string RootDirectory = "webroot";
    private static readonly string[] AllowedExtensions = { ".html", ".css", ".js" };
    private static readonly int Port = 8080;

    static void Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, Port);
        server.Start();
        Console.WriteLine($"Server started on port {Port}. Serving files from {Path.GetFullPath(RootDirectory)}");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Thread thread = new Thread(() => HandleClient(client));
            thread.Start();
        }
    }

    private static void HandleClient(TcpClient client)
    {
        using NetworkStream stream = client.GetStream();
        using StreamReader reader = new StreamReader(stream);
        using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

        try
        {
            string requestLine = reader.ReadLine();
            if (string.IsNullOrEmpty(requestLine))
                return;

            string[] tokens = requestLine.Split(' ');
            if (tokens.Length < 2)
                return;

            string method = tokens[0];
            string url = tokens[1];

            Console.WriteLine($"Request: {method} {url}");

            if (method.ToUpper() != "GET")
            {
                SendError(writer, 405, "Method Not Allowed");
                return;
            }

            string filePath = GetSafeFilePath(url);
            string extension = Path.GetExtension(filePath);

            if (Array.IndexOf(AllowedExtensions, extension) == -1)
            {
                SendError(writer, 403, "Forbidden");
                return;
            }

            if (File.Exists(filePath))
            {
                byte[] content = File.ReadAllBytes(filePath);
                string mime = GetMimeType(extension);
                writer.WriteLine("HTTP/1.1 200 OK");
                writer.WriteLine($"Content-Type: {mime}");
                writer.WriteLine($"Content-Length: {content.Length}");
                writer.WriteLine();
                stream.Write(content, 0, content.Length);
            }
            else
            {
                SendError(writer, 404, "Not Found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private static void SendError(StreamWriter writer, int code, string status)
    {
        string body = $"<html><head><title>{code} {status}</title></head><body><h1>Error {code}: {status}</h1></body></html>";
        writer.WriteLine($"HTTP/1.1 {code} {status}");
        writer.WriteLine("Content-Type: text/html");
        writer.WriteLine($"Content-Length: {Encoding.UTF8.GetByteCount(body)}");
        writer.WriteLine();
        writer.Write(body);
    }

    private static string GetMimeType(string extension) =>
        extension switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            _ => "application/octet-stream",
        };

    private static string GetSafeFilePath(string url)
    {
        url = Uri.UnescapeDataString(url.Split('?')[0]);
        string relativePath = url.TrimStart('/');
        string fullPath = Path.Combine(RootDirectory, relativePath);
        string normalizedPath = Path.GetFullPath(fullPath);

        if (!normalizedPath.StartsWith(Path.GetFullPath(RootDirectory)))
            throw new UnauthorizedAccessException("Directory traversal attempt detected.");

        return normalizedPath;
    }
}
