using Notion.Client;
using NotionMono.Parser;
using NotionMono.Notion;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using SyslogNet.Client;
using SyslogNet.Client.Transport;
using SyslogNet.Client.Serialization;
using OpenQA.Selenium;

class Data() 
{
    public ISyslogMessageSender? SyslogSender { get; set; }
    public ISyslogMessageSerializer? syslogMessageSerializer { get; set; }
}

class Program()
{
    readonly Dictionary<string, JarData> _jars = [];
    DatabaseController? _dbController;
    readonly JarParser _jarParser = new();
    static readonly StreamWriter _streamWriter = new("log.txt", append: true);
    static Data data = new();

    [JsonProperty("secret")]
    public readonly string? secret;
    [JsonProperty("dbName")]
    public readonly string? dbName;
    [JsonProperty("period_sec")]
    public readonly int? period_sec;
    [JsonProperty("jars")]
    public readonly List<string>? urls;

    static void Main() 
    {
        string filePath = "settings.json";
        Program? prog;

        try
        {
            // Считываем содержимое файла
            string json = File.ReadAllText(filePath);

            // Десериализуем JSON в объект класса Settings
            prog = JsonConvert.DeserializeObject<Program>(json);
            if (prog == null)
            {
                Log("Program load filed", true);
                _streamWriter.Flush();
                return;
            }
            prog.InitLog();
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("File not found");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Parse Program error: " + ex.Message);
            return;
        }

        prog.InitDriver();
    }

    void InitDriver() 
    {
        if (dbName is null || urls is null || period_sec is null) 
        {
            Log("Program: settings parse failed", true);
            return;
        }

        try 
        {
            data.SyslogSender = new SyslogLocalSender();
            data.syslogMessageSerializer = new SyslogLocalMessageSerializer();
        } 
        catch (Exception ex) 
        {
            Console.WriteLine(ex.Message);
        }

        if (OperatingSystem.IsLinux() && data.syslogMessageSerializer is not null && data.SyslogSender is not null)
            data.SyslogSender.Send(new SyslogMessage(Severity.Debug, "MonoNotion", "Test syslog"), data.syslogMessageSerializer);

        ClientOptions clientOptions = new()
        {
            AuthToken = secret
        };
        NotionClient client = NotionClientFactory.Create(clientOptions);
        _dbController = new DatabaseController(client);
        _dbController.InitDB(dbName);

        _jarParser.jarUpdate += UpdateJar;
        _jarParser.ChangeStrings(urls);
        _jarParser.Init(TimeSpan.FromSeconds(double.Parse(period_sec.Value.ToString())));
        
        _streamWriter.AutoFlush = true;

        while (true)
        {
            Thread.Sleep(Timeout.Infinite);
        }
    }

    static string HideSecret(string? secret) 
    {
        if (secret is null)
            return "";
        return secret[0..7] + new string('*', secret.Length-8);
    }

    async void UpdateJar(JarData jarData) 
    {
        if (_dbController is null)
        {
            Log("Program: controller is null", true);
            return;
        }
        if (await _dbController.NotionCheckJar(jarData) > 0)
            _dbController.NotionUpdateJar(jarData);
        else
            await _dbController.AddJar(jarData);
    }

    public static void Log(string logs, bool isError = false) 
    {
        Console.WriteLine(DateTime.Now + ": " + logs);
        _streamWriter.WriteLine(DateTime.Now + ": " + logs);
         if(isError && OperatingSystem.IsLinux() && data.syslogMessageSerializer is not null && data.SyslogSender is not null)
             data.SyslogSender.Send(new SyslogMessage(Severity.Error, "MonoNotion", DateTime.Now + ": " + logs), data.syslogMessageSerializer);
    }

    void InitLog()
    {
        Log("Init parametres:");
        Log($"secret: {HideSecret(secret)}");
        Log($"dbName: {dbName}");
        Log($"periodsec: {period_sec}");
        Log("Urls:");
        if (urls is null)
        {
            Log("Program failed parse jar`s urls");
            return;
        }
        foreach (var url in urls)
        {
            Log($"{url}");
        }
    }
}