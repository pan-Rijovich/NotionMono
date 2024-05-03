using Notion.Client;
using NotionMono.Parser;
using NotionMono.Notion;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

class Program()
{
    readonly Dictionary<string, JarData> _jars = [];
    DatabaseController? _dbController;
    readonly JarParser _jarParser = new();
    static readonly StreamWriter _streamWriter = new("log.txt", append: true);

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
                Log("Program load filed");
                _streamWriter.Flush();
                return;
            }
            prog.InitLog();
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("Файл не найден");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Произошла ошибка: " + ex.Message);
            return;
        }

        prog.InitDriver();
    }

    void InitDriver() 
    {
        if (dbName is null || urls is null || period_sec is null) 
        {
            Log("Program: settings parse failed");
            return;
        }
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
            Log("Program: controller is null");
            return;
        }
        if (await _dbController.NotionCheckJar(jarData) > 0)
            _dbController.NotionUpdateJar(jarData);
        else
            await _dbController.AddJar(jarData);
    }

    public static void Log(string logs) 
    {
        Console.WriteLine(DateTime.Now + ": " + logs);
        _streamWriter.WriteLine(DateTime.Now + ": " + logs);
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