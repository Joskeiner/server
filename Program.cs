using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Text.Json;
using model;
class SimpleWebServer
{
    private static string rootDirectory = "./Proyecto-final" ; // Carpeta por defecto para archivos.
    private static int port = 8081 ; // Puerto por defecto.
    private static string configFile = "./server.config"; // Archivo de configuración.

    static  void Main(string[] args)
    {

        // Cargar la configuración desde archivo externo
      ServerConfig config = LoadConfiguration();
        
        // Crear el socket TCP para escuchar conexiones
        TcpListener server = new TcpListener(IPAddress.Any, config.Port);
        server.Start();
        Console.WriteLine($"Servidor escuchando en el puerto {config.Port}");

        while (true)
        {
            // Aceptar conexiones de clientes de manera concurrente
            TcpClient client = server.AcceptTcpClient();
            ThreadPool.QueueUserWorkItem(HandleRequest, client);
        }
    }

    private  static ServerConfig LoadConfiguration()
    {
        string jsonContent = File.ReadAllText(configFile);
        ServerConfig config = JsonSerializer.Deserialize<ServerConfig>(jsonContent);
         
          if(config.Port == null ){
            config.Port = port ;
          } 
          if(config.Path == null || config.Path == ""){
            config.Path = rootDirectory;
          } 

        return config;
    }

    private static void HandleRequest(object obj )
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream);
        StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
        var config = LoadConfiguration();
        try
        {
            // Leer la solicitud HTTP
            string requestLine = reader.ReadLine();
            if (string.IsNullOrEmpty(requestLine)) return;

            string[] tokens = requestLine.Split(' ');
            if (tokens.Length < 2) return;
            string method = tokens[0];
            string url = tokens[1];
           Console.WriteLine(" esto tiene url " + url);

            Console.WriteLine($"Solicitud {method} {url}");

            // Manejar solicitudes GET y POST
            if (method == "GET")
            {
                 //Console.WriteLine("entra aqui");
                ServeFile(writer , config , url);
            }
            else if (method == "POST")
            {
                // Loguear datos del POST
                LogRequestData(reader);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            stream.Close();
            client.Close();
        }
    }

    private static void ServeFile(StreamWriter writer , ServerConfig config , string url)
    {
        if (url == "/") url = "/index.html"; // Servir index.html por defecto

        string filePath = Path.Combine(config.Path, url.TrimStart('/'));
        Console.WriteLine(filePath);
        if (File.Exists(filePath))
        {
            string fileExtension = Path.GetExtension(filePath);
            string mimeType = GetMimeType(fileExtension);
            Console.WriteLine("mimeType : " +mimeType);

            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine($"Content-Type: {mimeType}");
            writer.WriteLine("Content-Encoding: gzip"); 
            writer.WriteLine("Connection: close");
            writer.WriteLine(); // Fin de headers

            // Enviar archivo comprimido
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);

                byte[] compressedData = Compress(buffer);
                writer.BaseStream.Write(compressedData, 0, compressedData.Length);
            }
        }
        else
        {
            // Archivo no encontrado, devolver 404
            writer.WriteLine("HTTP/1.1 404 Not Found");
            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.WriteLine("<h1>Error 404 - Archivo no encontrado</h1>");
        }
    }

    private static byte[] Compress(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }

    private static void LogRequestData(StreamReader reader)
    {
        // Leer cuerpo del POST y loguear
        string data = reader.ReadLine();
        Console.WriteLine($"Datos POST recibidos: {data}");
    }

    private static string GetMimeType(string extension)
    {
        return extension switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            _ => "application/octet-stream",
        };
    }
}
