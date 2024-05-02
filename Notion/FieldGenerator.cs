using Notion.Client;

namespace NotionMono.Notion
{
    static class FieldGenerator
    {

        public static TitlePropertyValue GetTitle(string title)
        {
            RichTextText text = new()
            {
                Text = new()
            };
            text.Text.Content = title;
            return new TitlePropertyValue{  Title = [text]  };
        }

        public static RichTextPropertyValue GetRichText(string text) 
        {
            RichTextText richTextText = new()
            {
                Text = new()
            };
            richTextText.Text.Content = text;
            return new RichTextPropertyValue(){  RichText = [richTextText]  };
        }

        public static NumberPropertyValue GetNumberProperty(double number) 
        {
            return new NumberPropertyValue()
            {
                Number = number
            };
        }

        public static FilesPropertyValue GetFilesProperty(string url) 
        {
            ExternalFileWithName externalFileWithName = new()
            {
                External = new ExternalFileWithName.Info() 
                {
                    Url = url
                }
            };
            return new FilesPropertyValue()
            {
                Files = [externalFileWithName]
            };
        }

        public static UrlPropertyValue GetUrlProperty(string url)
        {
            return new UrlPropertyValue()
            {
                Url = url
            };
        }
    }
}
