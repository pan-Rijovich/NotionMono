using Notion.Client;
using NotionMono.Parser;

namespace NotionMono.Notion
{
    public class DatabaseController(NotionClient client)
    {
        private readonly NotionClient _client = client;
        private Database? _db;

        const string BalanceFieldName = "Balance"; 
        const string TitleFieldName = "Name";
        const string DescriptionFieldName = "Description";
        const string ImageFieldName = "Image";
        const string LinkFieldName = "Link";

        public void InitDB(string dbName)
        {
            SearchFilter filter = new()
            {
                Value = SearchObjectType.Database
            };
            SearchParameters searchParameters = new()
            {
                Query = dbName,
                Filter = filter
            };

            TryInitDB(searchParameters, dbName);

            if (_db is null) 
            {
                Program.Log("DatabaseController: db not found; name: " + dbName);
                return;
            }

            foreach (var param in _db.Properties) 
            {
               Program.Log(param.Key + ": " + param.Value);
            }
        }

        void TryInitDB(SearchParameters searchParameters, string dbName) 
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var search = _client.Search.SearchAsync(searchParameters);

                    Program.Log("Found jars:");
                    foreach (var result in search.Result.Results)
                    {
                        Program.Log("Title: " + ((Database)result).Title.FirstOrDefault()?.PlainText);
                        if (result is Database resultDB && resultDB.Title.FirstOrDefault()?.PlainText == dbName)
                            _db = resultDB;
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Program.Log("Search DB: " + ex.Message);
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
            }
        }

        Page? CreatePage(PagesCreateParameters param)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var task = _client?.Pages.CreateAsync(param);
                    if (task is null)
                    {
                        Program.Log("DBController: page create failed");
                        return new Page();
                    }
                    task.Wait();
                    return task.Result;
                }
                catch (Exception ex)
                {
                    Program.Log("Create page filed: " + ex.Message);
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
            }
            return null;
        }

        static object? GetValue(PropertyValue p)
        {
            switch (p)
            {
                case RichTextPropertyValue richTextPropertyValue:
                    return richTextPropertyValue.RichText.FirstOrDefault()?.PlainText;
                case NumberPropertyValue numberPropertyValue:
                    if (numberPropertyValue.Number is null)
                        return null;
                    return numberPropertyValue.Number.Value;
                case FilesPropertyValue filesPropertyValue:
                    ExternalFileWithName? file = filesPropertyValue.Files.First() as ExternalFileWithName;
                    return file;
                case TitlePropertyValue titlePropertyValue:
                    return titlePropertyValue.Title.FirstOrDefault()?.PlainText;
                case UrlPropertyValue urlPropertyValue:
                    return urlPropertyValue.Url;
                default:
                    return "";
            }
        }

        public async Task<int> NotionCheckJar(JarData jarData)
        {
            if (_db is null)
                return -1;

            DatabasesQueryParameters param = new()
            {
                Filter = new RichTextFilter(TitleFieldName, contains: jarData.Name)
            };
            var task = await _client.Databases.QueryAsync(_db?.Id, param);
            if (task is null) 
            {
                Program.Log("DBController: failed check jar");
                return -1;
            }

            return task.Results.Count;
        }

        public async void NotionUpdateJar(JarData jarData)
        {
            if (_db is null)
                return;

            DatabasesQueryParameters param = new()
            {
                Filter = new RichTextFilter(TitleFieldName, contains: jarData.Name)
            };

            try
            {
                Page page = (await _client.Databases.QueryAsync(_db?.Id, param)).Results[0];
                PagesUpdateParameters parameters = new()
                {
                    Properties = new Dictionary<string, PropertyValue>()
                };

                if (GetValue(page.Properties[BalanceFieldName]) is double balance && balance != jarData.Value)
                {
                    parameters.Properties.Add(BalanceFieldName, FieldGenerator.GetNumberProperty(jarData.Value));
                    Program.Log($"Changed: {balance} -> {jarData.Value} in {jarData.Name}");
                }
                if (GetValue(page.Properties[DescriptionFieldName]) is string description && description != jarData.Description)
                {
                    parameters.Properties.Add(DescriptionFieldName, FieldGenerator.GetRichText(jarData.Description));
                    Program.Log($"Changed: {description[0..20]} -> {jarData.Description[0..20]} in {jarData.Name}");
                }
                if (GetValue(page.Properties[ImageFieldName]) is ExternalFileWithName file && file.External.Url != jarData.ImgPath)
                {
                    parameters.Properties.Add(ImageFieldName, FieldGenerator.GetFilesProperty(jarData.ImgPath, "Image"));
                    Program.Log($"Changed: {file.External.Url} -> {jarData.ImgPath} in {jarData.Name}");
                }
                if (GetValue(page.Properties[LinkFieldName]) is string link && link != jarData.Link)
                {
                    parameters.Properties.Add(LinkFieldName, FieldGenerator.GetUrlProperty(jarData.Link));
                    Program.Log($"Changed: {link} -> {jarData.Link} in {jarData.Name}");
                }

                if (parameters.Properties.Count > 0)
                    await _client.Pages.UpdatePropertiesAsync(page.Id, parameters.Properties);
            }
            catch (Exception ex) 
            {
                Program.Log("Exception in NotionUpdateJar: " + ex.Message);
            }
        }

        public Task AddJar(JarData jarData)
        {
            if (_db is null)
            {
                Console.WriteLine("DB is null");
                return Task.CompletedTask;
            }

            var creat = new PagesCreateParameters
            {
                Parent = new DatabaseParentInput { DatabaseId = _db.Id },
                Properties = new Dictionary<string, PropertyValue>
                {
                    { DescriptionFieldName, FieldGenerator.GetRichText(jarData.Description) },
                    { BalanceFieldName, FieldGenerator.GetNumberProperty(jarData.Value) },
                    { ImageFieldName, FieldGenerator.GetFilesProperty(jarData.ImgPath, "Image") },
                    { TitleFieldName, FieldGenerator.GetTitle(jarData.Name) },
                    { LinkFieldName, FieldGenerator.GetUrlProperty(jarData.Link)}
                }
            };

            var response = CreatePage(creat);

            if (response is not null && response.Object is ObjectType.Page)
            {
                Program.Log($"added new jar: {jarData.Name}\ndescription: {jarData.Description}" +
                    $"\nvalue: {jarData.Value}\nimgPath: {jarData.ImgPath}");
            }

            return Task.CompletedTask;
        }
    }
}
