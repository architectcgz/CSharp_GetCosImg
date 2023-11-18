using HtmlAgilityPack;
using System.Diagnostics;


namespace Program
{
    class Program
    {
        static void showList<T>(List<T>list)
        {
            foreach (var item in list)
            {
                Console.WriteLine(item);
            }
        }
        static async Task Main(string[] args)
        {
            //string url = "https://www.ciyuanjie.cn/cosplay/";
            var getCosImg = new GetCosImg();
            Console.WriteLine("请输入要获取的图片的页数pageNum(1<=pageNum<=14)");
            int pageNum = Convert.ToInt32(Console.ReadLine());
            var (titleList, imgURLList) = await getCosImg.getImgURLWithPageNum(pageNum);
            var dict = await getCosImg.getEachImgDetail(imgURLList);
            for(int i =0;i<imgURLList.Count;i++)
            {
                await getCosImg.DownLoadAll(dict,titleList);
            }
            Console.WriteLine("已经全部下载完成，请查看文件夹");
            Console.WriteLine("点击任意键退出...");
            Console.ReadKey();
        }
    }
    class GetCosImg
    {
        public async Task<(List<string>, List<string>)> getImgURLWithPageNum(int pageNum)
        {
            if (pageNum <= 0)
            {
                Console.WriteLine("pageNum输入错误,1<=pageNum<=14！");
                return (new List<string>(), new List<string>());
            }
            string url = "";
            var imgURLList = new List<string>();
            var imgTitleList = new List<string>();
            if (pageNum == 1)
            {
                url = "https://www.ciyuanjie.cn/cosplay";
                var result = await getImgURLAndTitleOnePage(url);
                imgURLList.AddRange(result.Item1);
                imgTitleList.AddRange(result.Item2);
            }
            else
            {
                for(int i = 1; i <= pageNum; i++)
                {
                    url = $"https://www.ciyuanjie.cn/cosplay/page_{i}.html";
                    var result = await getImgURLAndTitleOnePage(url);
                    imgURLList.AddRange(result.Item1);
                    imgTitleList.AddRange(result.Item2);
                }
            }
            return (imgURLList, imgTitleList);
        }
        public async Task<(List<string>,List<string>)> getImgURLAndTitleOnePage(string url)
        {
            List<string> imgUrlList = new List<string>();
            List<string> titleList =  new List<string>();
            using HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/119.0");
            HttpResponseMessage response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var str = await response.Content.ReadAsStringAsync();
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(str);
                var nodes = document.DocumentNode.SelectNodes("//div[@class='kzpost-data']/a[1]");
                foreach (var node in nodes)
                {
                    var href = node.GetAttributeValue("href", "");
                    //Console.WriteLine(href);
                    imgUrlList.Add(href.Substring(1, href.Length - 1));
                    var title = node.GetAttributeValue("title", "");
                    titleList.Add(title);
                    //Console.WriteLine(title);
                }
            }
            return (titleList,imgUrlList);
        }
        private async Task<Dictionary<string,string>> getImgTitleAndSrcDict(string imgUrl)
        {
            using HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/119.0");
            var response = await httpClient.GetAsync(imgUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(imgUrl + "访问失败");
                return null;
            }
            var document = new HtmlDocument();
            var content = await response.Content.ReadAsStringAsync();
            document.LoadHtml(content);
            var nodes = document.DocumentNode.SelectNodes("//div[@class='content_left']/p[5]/span/img");
            nodes ??= document.DocumentNode.SelectNodes("//img[@class='aligncenter']");
            nodes ??= document.DocumentNode.SelectNodes("//div[@class='content_left']/p[4]/img");
            nodes ??= document.DocumentNode.SelectNodes("//div[@class='content_left']/p[5]/img");

            if (nodes == null)
            {
                return null;
            }
            var dict = new Dictionary<string, string>();
            foreach (var node in nodes)
            {
                //src是图片url
                var src = node.GetAttributeValue("src", "");
                Console.WriteLine(src);
                // alt也就是文件名
                var alt = node.GetAttributeValue("alt", "");
                Console.WriteLine(alt);
                if (!dict.ContainsKey(alt))
                    dict.Add(alt, src);
            }
            return dict;
        }
        public async Task<Dictionary<string,string>> getEachImgDetail(List<string>imgUrlList)
        {
            string detailedUrl = "https://www.ciyuanjie.cn/";

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
            Console.WriteLine($"本页有{imgUrlList.Count}组图片");
            var tasks = new List<Task<Dictionary<string, string>>>();
            for (int i = 0; i < imgUrlList.Count; i++)
            {
                int index = i;
                var task = Task.Run(() => getImgTitleAndSrcDict(detailedUrl + imgUrlList[index]));
                tasks.Add(task);
            }
            var results = await Task.WhenAll(tasks);

            var finalDict = new Dictionary<string, string>();
            
            foreach (var dict in results)
            {
                if (dict != null)
                {
                    foreach (var pair in dict)
                    {
                        if (!finalDict.ContainsKey(pair.Key))
                            finalDict.Add(pair.Key, pair.Value);
                    }
                }
            }

            sw.Stop();
            Console.WriteLine($"已获取第一页所有Img数据，用时{sw.Elapsed.ToString()}");
            return finalDict;    
        }
        private async Task<bool> DownloadFromURL(string url, string storagePath)
        {
            // 检查图片是否已经存在
            if (System.IO.File.Exists(storagePath))
            {
                Console.WriteLine($"{storagePath}已经存在，跳过下载");
                return true;
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(4);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/119.0");
            try
            {
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return false;
                var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = System.IO.File.Create(storagePath);
                if (stream == null)
                    return false;
                await stream.CopyToAsync(fileStream);
                Console.WriteLine($"{storagePath}保存成功!");
                return true;
            }
            catch (System.Net.Http.HttpRequestException)
            {
                Console.WriteLine($"{url}的图片下载失败，跳过...");
                return false;
            }
        }

        static string RemoveInvalidChars(string title)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(title.Where(c => !invalidChars.Contains(c)).ToArray());
        }
        public async Task DownLoadAll(Dictionary<string, string> dict, List<string> titleList)
        {

            //创建目录
            for (int i = 0; i < titleList.Count; i++)
            {
                int index = titleList[i].IndexOf("cosplay");
                string path = $"{titleList[i].Substring(0, index+7)}";
                path = RemoveInvalidChars(path);
                Console.WriteLine($"正在创建目录{path}");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            var tasks = new List<Task>();
            foreach (var key in dict.Keys)
            {
                //格式为《宿命回响》DESTINY cosplay欣赏@杜杜Dolly_ cosplay-第一张   -第1张去掉-
                int index = key.IndexOf("cosplay");
                string path = RemoveInvalidChars(key.Substring(0, index + 7)) + "\\" + key + ".jpg";
                var cancellationTokenSource = new CancellationTokenSource();
                var task = Task.Run(async () =>
                {
                    Console.WriteLine($"正在将文件保存到{path}");
                    var result = await DownloadFromURL(dict[key], path);
                    if (!result)
                    {
                        Console.WriteLine($"{key}下载失败，已取消下载任务");
                        cancellationTokenSource.Cancel();
                        return;
                    }
                }, cancellationTokenSource.Token);
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
        }
    }
}







