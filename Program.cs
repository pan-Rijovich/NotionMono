// See https://aka.ms/new-console-template for more information
using Notion.Client;
using NotionMono.Parser;
using NotionMono.Notion;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

class Program
{
    Dictionary<string, JarData> _jars = new Dictionary<string, JarData>();
    DatabaseController? _controller;
    JarParser _jarParser = new();
    static StreamWriter _streamWriter = new("log.txt", append: true);

    [JsonProperty("secret")]
    public readonly string? secret;
    [JsonProperty("dbName")]
    public readonly string? dbName;
    [JsonProperty("period_sec")]
    public readonly int? period_sec;
    [JsonProperty("jars")]
    public readonly List<string>? urls;

    public Program() 
    {
    }

    static void Main() 
    {
        string filePath = "settings.json";

        Program? prog = null;

        try
        {
            // Считываем содержимое файла
            string json = File.ReadAllText(filePath);

            // Десериализуем JSON в объект класса Settings
            prog = JsonConvert.DeserializeObject<Program>(json);

            Program.Log("Init parametres:");
            Program.Log($"secret: {prog.secret}");
            Program.Log($"dbName: {prog.dbName}");
            Program.Log($"periodsec: {prog.period_sec}");
            Program.Log("Urls:");
            foreach (var url in prog.urls)
            {
                Program.Log($"{url}");
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("Файл не найден");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Произошла ошибка: " + ex.Message);
        }
        if(prog != null)
            prog.InitDriver();
    }

    void InitDriver() 
    {
        ClientOptions clientOptions = new();
        clientOptions.AuthToken = secret;
        NotionClient client = NotionClientFactory.Create(clientOptions);
        _controller = new DatabaseController(client);
        _controller.InitDB(dbName);

        _jarParser.jarUpdate += UpdateJar;
        _jarParser.ChangeStrings(urls);
        _jarParser.Init(TimeSpan.FromSeconds(double.Parse(period_sec.ToString())));
        
        _streamWriter.AutoFlush = true;
        Console.WriteLine("Нажмите любую клавишу для остановки программы.");
        Console.ReadKey();

        _jarParser.StopTimer();
    }

    void UpdateJar(JarData jarData) 
    {
        if (!_jars.ContainsKey(jarData.Name))
        {
            if (_controller.NotionCheckJar(jarData) > 0)
            {
                _jars.Add(jarData.Name, jarData);
                _controller.NotionUpdateJar(jarData);
            }
            else
            {
                if (_controller.AddJar(jarData))
                    _jars.Add(jarData.Name, jarData);
            }
        }
        else
        {
            _controller.NotionUpdateJar(jarData);
            _jars[jarData.Name] = jarData;
        }
    }

    public static void Log(string logs) 
    {
        Console.WriteLine(DateTime.Now + ": " + logs);
        _streamWriter.WriteLine(DateTime.Now + ": " + logs);
    }
}