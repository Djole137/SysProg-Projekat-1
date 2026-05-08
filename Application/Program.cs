using System;
using System.Net;
using System.IO;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");

        bool serverRunning = true;

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[SERVER] Gašenje...");
            serverRunning = false;
            listener.Stop();
        };

        try
        {
            listener.Start();
            Console.WriteLine("Server pokrenut na http://localhost:5000/");

            while (serverRunning)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, context);
                }
                catch (HttpListenerException ex) when (!serverRunning)
                {
                    Console.WriteLine("[SERVER] Slušalac zaustavljen.");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Greška: " + ex.Message);
        }
        finally
        {
            listener.Close();
            Console.WriteLine("[SERVER] Server zaustavljen.");
        }
    }

    private static void ProcessRequest(object state)
    {
        HttpListenerContext context = (HttpListenerContext)state;
        var request = context.Request;

        if (request.Url.AbsolutePath.EndsWith("favicon.ico"))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.Close();
            return;
        }

        string category = request.QueryString["category"];
        string difficulty = request.QueryString["difficulty"]?.Trim().ToLower();

        try
        {
            string result = ApiService.GetQuizData(category, difficulty);
            SendResponse(context, result, HttpStatusCode.OK);

        }
        catch (Exception ex)
        {
            SendResponse(context, $"{{\"error\": \"{ex.Message}\"}}", HttpStatusCode.InternalServerError);
        }
    }

    private static void SendResponse(HttpListenerContext context, string content, HttpStatusCode status)
    {
        try
        {
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            using var writer = new StreamWriter(context.Response.OutputStream);
            writer.Write(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Greška pri slanju odgovora: {ex.Message}");
        }
        finally
        {
            context.Response.Close();
        }
    }
}