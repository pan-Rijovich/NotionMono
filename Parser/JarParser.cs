using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace NotionMono.Parser
{
    public partial class JarParser
    {
        public Action<JarData>? jarUpdate;
        List<string> jarURLs = [];
        ChromeDriver _driver;
        readonly ChromeOptions options;
        Timer? timer;

        public JarParser() 
        {
            options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--log-level=1");
            _driver = new ChromeDriver(options);
        }

        public void ChangeStrings(List<string> strings) 
        {
            jarURLs.Clear();
            jarURLs = strings;
        }

        public void Init(TimeSpan timerPeriod) 
        {
            timer = new Timer(callback: CheckPage, null, TimeSpan.Zero, timerPeriod);
        }

        void CheckPage(object? state)
        {
            Program.Log("update pages");

            for (int i = 0; i < jarURLs.Count; i++)
            {
                JarData? jarData = ParseJar(jarURLs[i]);
                if (jarData != null)
                {
                    Program.Log("Check page success: " + jarURLs[i] + $"; current: {i+1}/{jarURLs.Count}");
                    jarUpdate?.Invoke((JarData)jarData);
                }
                else
                    Program.Log("Check page failed: " + jarURLs[i] + $"; current: {i + 1}/{jarURLs.Count}");
            }
        }

        private JarData? ParseJar(string url) 
        {
            JarData jarData = new()
            {
                Link = url
            };

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    _driver.Navigate().GoToUrl(url);

                    WebDriverWait wait = new(_driver, TimeSpan.FromSeconds(10));
                    Func<IWebDriver, bool> waitForElement = new((IWebDriver Web) =>
                    {
                        Web.FindElement(By.CssSelector(".description-box"));
                        return true;
                    });
                    wait.Until(waitForElement);

                    IWebElement title = _driver.FindElement(By.CssSelector(".field.name"));

                    jarData.Name = title.Text;
                    IWebElement text = _driver.FindElement(By.CssSelector(".description-box"));

                    jarData.Description = text.Text;
                    IWebElement cost = _driver.FindElement(By.CssSelector(".stats-data-value"));


                    string buf = cost.Text;
                    buf = buf.Replace(" ", "");
                    buf = buf.Replace("₴", "");
                    if (OperatingSystem.IsWindows())
                        buf = buf.Replace(".", ",");
                    if (!double.TryParse(buf, out jarData.Value))
                        Program.Log("Parse failed in parseJar");

                    IWebElement pngPath = _driver.FindElement(By.XPath("//div[@id='jar-state']//div[@class='img']"));
                    Regex regex = ClearLinkRegex();

                    Match match = regex.Match(pngPath.GetAttribute("style"));
                    jarData.ImgPath = match.Groups[1].Value;

                    Thread.Sleep(100);
                    break;
                }
                catch (NoSuchWindowException ex)
                {
                    _driver = new ChromeDriver(options);
                    Program.Log("CheckPage: " + ex);
                }
                catch (Exception ex)
                {
                    Program.Log("Failed to load page: " + ex.Message);
                    return null;
                }
            }

            return jarData;
        }

        public void StopTimer()
        {
            timer?.Dispose();
        }

        [GeneratedRegex(@"url\(""(.+?)""\)")]
        private static partial Regex ClearLinkRegex();
    }
}
