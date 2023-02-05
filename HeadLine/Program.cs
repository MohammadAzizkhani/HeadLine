using IronOcr;
using System.Drawing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

string token = string.Empty;
Console.WriteLine("Please Enter Isin : ");
var isin = Console.ReadLine();
Console.WriteLine("Please Enter Volume : ");
short volume = short.Parse(Console.ReadLine());
Console.WriteLine("Please Enter Price : ");
var price = int.Parse(Console.ReadLine());
do
{
    using (var client = new HttpClient { BaseAddress = new Uri("https://identity.ibtrader.ir") })
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
                    loginName = "",
                    password = "",
                    captcha = new Captcha
                    {
                        hash = captcha.hashedCaptcha,
                        salt = captcha.salt,
                        value = captchaValue.Text
                    }
                };

                var loginResponse = await client.PostAsJsonAsync("api/v2/accounts/login", loginModel);

                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await loginResponse.Content.ReadAsStringAsync());

                token = tokenResponse?.token ?? string.Empty;
            }
        }
    }
} while (string.IsNullOrWhiteSpace(token));
Console.WriteLine("token Get from api Successfully");
Console.WriteLine("-------------------------------------------------------");
using (var client = new HttpClient { BaseAddress = new Uri("https://api.ibtrader.ir/") })
{

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var data = new Order { validity = 1, validityDate = null, price = price, volume = volume, side = 1, isin = isin, accountType = 1 };
    while (DateTime.Now.Hour < 9 && DateTime.Now.Minute < 2)
    {
        var tasks = new int[100].Select(async x => await client.PostAsJsonAsync("api/v2/orders/NewOrder", data));

        await Task.WhenAll(tasks);
    }
}

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