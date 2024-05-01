using Notion.Client;
using NotionMono.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotionMono.Notion
{
    public class DatabaseController
    {
        private NotionClient _client;
        private Database? _db;

        const string BalanceFieldName = "Balance"; 
        const string TitleFieldName = "Name";
        const string DescriptionFieldName = "Description";
        const string ImageFieldName = "Image";
        const string LinkFieldName = "Link";
        public DatabaseController(NotionClient client) 
        {
            _client = client;
        }

        public void InitDB(string dbName)
        {
            SearchParameters searchParameters = new SearchParameters();
            searchParameters.Query = dbName;

            var search = _client.Search.SearchAsync(searchParameters);

            foreach (var result in search.Result.Results) 
            {
                if(result is Database)
                    _db = (Database)result;
            }

            foreach (var param in _db.Properties) 
            {
               Program.Log(param.Key + ": " + param.Value);
            }
        }

        Page CreatePage(PagesCreateParameters param)
        {
            var task = _client?.Pages.CreateAsync(param);
            Task.WaitAll(task);
            return task.Result;
        }

        object GetValue(PropertyValue p)
        {
            switch (p)
            {
                case RichTextPropertyValue richTextPropertyValue:
                    return richTextPropertyValue.RichText.FirstOrDefault()?.PlainText;
                case NumberPropertyValue numberPropertyValue:
                    return numberPropertyValue.Number;
                case FilesPropertyValue filesPropertyValue:
                    ExternalFileWithName file = filesPropertyValue.Files.First() as ExternalFileWithName;
                    return file;
                case TitlePropertyValue titlePropertyValue:
                    return titlePropertyValue.Title.FirstOrDefault()?.PlainText;
                case UrlPropertyValue urlPropertyValue:
                    return urlPropertyValue.Url;
                default:
                    return "";
            }
        }

        public int NotionCheckJar(JarData jarData)
        {
            if (_db is null)
                return -1;

            DatabasesQueryParameters param = new DatabasesQueryParameters();
            param.Filter = new RichTextFilter(TitleFieldName, contains: jarData.Name);
            var task = _client?.Databases.QueryAsync(_db?.Id, param);
            Task.WaitAll(task);

            return task.Result.Results.Count;
        }

        public async void NotionUpdateJar(JarData jarData)
        {
            if (_db is null)
                return;

            DatabasesQueryParameters param = new DatabasesQueryParameters();
            param.Filter = new RichTextFilter(TitleFieldName, contains: jarData.Name);
            Page page = (await _client.Databases.QueryAsync(_db?.Id, param)).Results[0];
            PagesUpdateParameters parameters = new PagesUpdateParameters();
            parameters.Properties = new Dictionary<string, PropertyValue>();
            
            if ((double)GetValue(page.Properties[BalanceFieldName]) != jarData.Value) 
            {
                NumberPropertyValue value = new();
                value.Number = jarData.Value;
                parameters.Properties.Add(BalanceFieldName,value);
                Program.Log($"Changed: {(double)GetValue(page.Properties[BalanceFieldName])} -> {jarData.Value} in {jarData.Name}");
            }
            if ((string)GetValue(page.Properties[DescriptionFieldName]) != jarData.Description) 
            {
                RichTextText description = new();
                description.Text = new();
                description.Text.Content = jarData.Description;
                
                RichTextPropertyValue text = new();
                text.RichText = [description];

                parameters.Properties.Add(DescriptionFieldName, text);
                Program.Log($"Changed: {((string)GetValue(page.Properties[DescriptionFieldName]))[0..20]} -> {jarData.Description[0..20]} in {jarData.Name}");
            }
            if (((ExternalFileWithName)GetValue(page.Properties[ImageFieldName])).External.Url != jarData.ImgPath) 
            {
                ExternalFileWithName file = new();
                file.External = new();
                file.External.Url = jarData.ImgPath;
                file.Name = ImageFieldName;

                FilesPropertyValue files = new();
                files.Files = [file];

                parameters.Properties.Add(ImageFieldName, files);

                Program.Log($"Changed: {((ExternalFileWithName)GetValue(page.Properties[ImageFieldName])).External.Url} -> {jarData.ImgPath} in {jarData.Name}");
            }
            if ((string)GetValue(page.Properties[LinkFieldName]) != jarData.Link) 
            {
                UrlPropertyValue url = new();
                url.Url = jarData.Link;

                parameters.Properties.Add(LinkFieldName, url);

                Program.Log($"Changed: {(string)GetValue(page.Properties[LinkFieldName])} -> {jarData.Link} in {jarData.Name}");
            }

            if (parameters.Properties.Count > 0)
               await _client.Pages.UpdatePropertiesAsync(page.Id, parameters.Properties);
        }

        public bool AddJar(JarData jarData)
        {
            if (_db is null)
            {
                Console.WriteLine("DB is null");
                return false;
            }

            Page page = new();
            page.Properties = new Dictionary<string, PropertyValue>();

            RichTextText text = new();
            text.Text = new();
            text.Text.Content = jarData.Name;
            TitlePropertyValue title = new();
            title.Title = [text];

            RichTextText desc_text = new();
            desc_text.Text = new();
            desc_text.Text.Content = jarData.Description;
            RichTextPropertyValue description = new();
            description.RichText = [desc_text];

            NumberPropertyValue value = new();
            value.Number = jarData.Value;

            ExternalFileWithName file = new();
            file.External = new();
            file.Name = ImageFieldName;
            file.External.Url = jarData.ImgPath;
            FilesPropertyValue files = new();
            files.Files = [file];

            UrlPropertyValue url = new();
            url.Url = jarData.Link; 

            var creat = new PagesCreateParameters();
            creat.Parent = new DatabaseParentInput { DatabaseId = _db.Id };
            creat.Properties = new Dictionary<string, PropertyValue>
                        {
                            { DescriptionFieldName, description },
                            { BalanceFieldName, value },
                            { ImageFieldName, files },
                            { TitleFieldName, title },
                            { LinkFieldName, url}
                        };

            var response = CreatePage(creat);

            if (response.Object is ObjectType.Page)
            {
                Program.Log($"added new jar: {jarData.Name}\ndescription: {jarData.Description}\nvalue: {jarData.Value}\nimgPath: {jarData.ImgPath}");
                return true;
            }
            else
                return false;
        }
    }
}
