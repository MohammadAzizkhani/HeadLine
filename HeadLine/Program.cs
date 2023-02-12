using IronOcr;
using System.Drawing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
var token = string.Empty;
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsetting.json")
    .Build();
var authUrl = configuration.GetSection("AuthenticationUrl");
var baseUrl = configuration.GetSection("OnlineOrderingUrl");
var userPass = configuration.GetSection("UserPass").Value.Split(';');
var nationalCode = userPass[0];
var password = userPass[1];
var orderInfo = configuration.GetSection("OrderInfo").Value.Split(';');
var price = int.Parse(orderInfo[0]);
var isin = orderInfo[1];
short volume = short.Parse(orderInfo[2]);
byte side = byte.Parse(orderInfo[3]);
do
{
    using (var client = new HttpClient { BaseAddress = new Uri(authUrl.Value) })
    {
        var captchaResponse = await client.GetAsync("api/Captcha/GetCaptcha");
        var captcha = await JsonSerializer.DeserializeAsync<CaptchaDto>(await captchaResponse.Content.ReadAsStreamAsync());
        var imageData = Convert.FromBase64String(captcha.captchaByteData);
        using (var memoryStream = new MemoryStream(imageData, 0, imageData.Length))
        {
            var ocr = new IronTesseract
            {
                Language = OcrLanguage.EnglishBest,
                Configuration = new TesseractConfiguration
                {
                    ReadBarCodes = false,
                    WhiteListCharacters = "0123456789",
                    BlackListCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
                    PageSegmentationMode = TesseractPageSegmentationMode.SingleLine,
                    RenderSearchablePdfsAndHocr = false,
                }
            };
            using (OcrInput ocrInput = new())
            {
                ocrInput.AddImage(Image.FromStream(memoryStream));
                var captchaValue = ocr.Read(ocrInput);
                if (!Regex.IsMatch(captchaValue.Text, @"\d{5}"))
                    continue;
                LoginRequestModel loginModel = new()
                {
                    loginName = nationalCode,
                    password = password,
                    captcha = new Captcha
                    {
                        hash = captcha.hashedCaptcha,
                        salt = captcha.salt,
                        value = captchaValue.Text
                    }
                };

                var loginResponse = await client.PostAsync("api/v2/accounts/login",
                    new StringContent(JsonSerializer.Serialize(loginModel), Encoding.UTF8, "application/json"));

                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await loginResponse.Content.ReadAsStringAsync());

                token = tokenResponse?.token ?? string.Empty;
            }
        }
    }
} while (string.IsNullOrWhiteSpace(token));
Console.WriteLine("token Get from api Successfully");
Console.WriteLine("-------------------------------------------------------");
using (var client = new HttpClient { BaseAddress = new Uri(baseUrl.Value) })
{
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var data = new Order { validity = 1, validityDate = null, price = price, volume = volume, side = side, isin = isin, accountType = 1 };
    while (DateTime.Now.TimeOfDay < new TimeSpan(9, 3, 0))
    {
        var tasks = new bool[5000].Select(async x => await client.PostAsJsonAsync("api/v2/orders/NewOrder", data));

        await Task.WhenAll(tasks);
    }
}

Console.WriteLine("Done !");
Console.WriteLine("Press any key to continue ... ");
Console.ReadKey();
class Order
{
    public byte validity { get; set; }
    public int price { get; set; }
    public short volume { get; set; }
    public byte side { get; set; }
    public string isin { get; set; }
    public DateTime? validityDate { get; set; }
    public byte accountType { get; set; }
}
class CaptchaDto
{
    public string captchaByteData { get; set; }
    public string salt { get; set; }
    public string hashedCaptcha { get; set; }
}
class LoginRequestModel
{
    public string loginName { get; set; }
    public string password { get; set; }
    public Captcha captcha { get; set; }
}
class Captcha
{
    public string hash { get; set; }
    public string salt { get; set; }
    public string value { get; set; }
}
class TokenResponse
{
    public string token { get; set; }
    public string sessionId { get; set; }
    public string thirdPartyToken { get; set; }
    public int expireIn { get; set; }
    public int step { get; set; }
    public int sejamStatus { get; set; }
    public bool forceToChangePassword { get; set; }
    public bool warningToChangePassword { get; set; }
    public string errorMessage { get; set; }
    public int errorCode { get; set; }
    public bool isSuccess { get; set; }
}