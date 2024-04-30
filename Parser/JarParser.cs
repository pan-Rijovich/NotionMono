using Notion.Client;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NotionMono.Parser
{
    public class JarParser
    {
        List<string> jarURLs = new();
        public Action<JarData>? jarUpdate;
        IWebDriver? _driver;
        Timer? timer;

        public void ChangeStrings(List<string> strings) 
        {
            jarURLs.Clear();
            jarURLs = strings;
        }

        public void Init(TimeSpan timerPeriod) 
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-in-process-stack-traces");
            options.AddArgument("--disable-logging");
            options.AddArgument("--log-level=3");
            options.AddArgument("--output=/dev/null");
            _driver = new ChromeDriver(options);

            timer = new Timer(callback: CheckPage, null, TimeSpan.Zero, timerPeriod);
        }

        void CheckPage(object? state)
        {
            // URL страницы, которую вы хотите парсить
            try
            {
                foreach (string url in jarURLs)
                    jarUpdate.Invoke(parseJar(url));

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private JarData parseJar(string url) 
        {
            JarData jarData = new();

            _driver.Navigate().GoToUrl(url);

            // Используем явное ожидание для ожидания загрузки элемента на странице
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            Func<IWebDriver, bool> waitForElement = new Func<IWebDriver, bool>((IWebDriver Web) =>
            {
                Web.FindElement(By.CssSelector(".description-box"));
                return true;
            });
            wait.Until(waitForElement);

            IWebElement title = _driver.FindElement(By.CssSelector(".field.name"));
            //Console.WriteLine("Name: " + title.Text);
            jarData.Name = title.Text;
            IWebElement text = _driver.FindElement(By.CssSelector(".description-box"));
            //Console.WriteLine("Text: " + text.Text);
            jarData.Description = text.Text;
            IWebElement cost = _driver.FindElement(By.CssSelector(".stats-data-value"));
            //Console.WriteLine("Cost: " + cost.Text);
            string buf = cost.Text;
            buf = buf.Replace(" ", "");
            buf = buf.Replace("₴", "");
            buf = buf.Replace(".", ",");
            if (!double.TryParse(buf, out jarData.Value))
                Program.Log("Parse failed in parseJar");
            IWebElement pngPath = _driver.FindElement(By.XPath("//div[@id='jar-state']//div[@class='img']"));
            Regex regex = new Regex(@"url\(""(.+?)""\)");
            // Поиск URL в строке CSS-кода
            Match match = regex.Match(pngPath.GetAttribute("style"));
            jarData.ImgPath = match.Groups[1].Value;
            //Console.WriteLine("Image: " + match.Groups[1].Value);

            return jarData;
        }

        public void StopTimer()
        {
            timer?.Dispose();
        }
    }
}
