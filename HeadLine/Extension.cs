namespace HeadLine
{
    public static class Extension
    {
        public static HttpClient GetHttpClient(string baseUrl, HttpClientHandler? handler = null)
        {
            if (handler is null)
            {
                return new HttpClient
                {
                    BaseAddress = new Uri(baseUrl)
                };
            }
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl)
            };
        }
    }
}
