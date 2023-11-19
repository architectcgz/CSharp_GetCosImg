using HtmlAgilityPack;
using System.Diagnostics;
using System.Threading;


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
            Console.WriteLine("请输入要获取的图片的页数范围下限(1<=下限<=14)");
            int pageNumLow = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("请输入要获取的图片范围上限(上限<=下限<=14)");
            int pageNumHigh = Convert.ToInt32(Console.ReadLine());

            var (titleList, imgURLList) = await getCosImg.getImgURLWithPageNum(pageNumLow,pageNumHigh);
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
        public async Task<(List<string>, List<string>)> getImgURLWithPageNum(int pageNumLow,int pageNumHigh)
        {
            if (pageNumLow <1||pageNumHigh>14)
            {
                Console.WriteLine("页数范围下限或上限输入错误,1<=页数<=14！");
                return (new List<string>(), new List<string>());
            }
            string url;
            var imgURLList = new List<string>();
            var imgTitleList = new List<string>();
            
            for(int i = pageNumLow; i <= pageNumHigh; i++)
            {
                url = $"https://www.ciyuanjie.cn/cosplay/page_{i}.html";
                var result = await getImgURLAndTitleOnePage(url);
                imgURLList.AddRange(result.Item1);
                imgTitleList.AddRange(result.Item2);
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
                var titleNodes = document.DocumentNode.SelectNodes("//div[@class='posr-tit']");
                foreach ( var titleNode in titleNodes )
                {
                    titleList.Add(titleNode.InnerText);
                    //Console.WriteLine(title);
                }
                var nodes = document.DocumentNode.SelectNodes("//div[@class='kzpost-data']/a[1]");
                foreach (var node in nodes)
                {
                    var href = node.GetAttributeValue("href", "");
                    //Console.WriteLine(href);
                    imgUrlList.Add(href.Substring(1, href.Length - 1));
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
            var nodes = document.DocumentNode.SelectNodes("//p//img");
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
        /// <summary>
        /// 使用基于Task的并行处理获取每一个cos系列的所有图片的url以及title(标题)
        /// 保存到字典中 title:url
        /// </summary>
        /// <param name="imgUrlList"></param>
        /// <returns></returns>
        public async Task<Dictionary<string,string>> getEachImgDetail(List<string>imgUrlList)
        {
            string detailedUrl = "https://www.ciyuanjie.cn/";
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
            httpClient.Timeout = TimeSpan.FromMinutes(5);
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
                Console.WriteLine($"{url}的图片下载失败，等待重新下载...");
                return false;
            }
        }

        /// <summary>
        /// 去除图片名中与路径相关的非法字符，如\ :等等
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        static string RemoveInvalidChars(string title)
        {
            
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(title.Where(c => !invalidChars.Contains(c)).ToArray());
        }
        /// <summary>
        /// 基于Task的并行处理下载所有图片
        /// 如果图片数量过大，会占用非常大的内存，所以考虑使用线程池替代Task并行处理
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="titleList"></param>
        /// <returns></returns>
        public async Task DownLoadAll(Dictionary<string, string> dict, List<string> titleList)
        {

            //创建目录
            for (int i = 0; i < titleList.Count; i++)
            {
                string path = $"{titleList[i]}";
                path = RemoveInvalidChars(path);
                Console.WriteLine($"正在创建目录{path}");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            var tasks = new List<Task>();
            foreach (var key in dict.Keys)
            {
                //格式为《宿命回响》DESTINY cosplay欣赏@杜杜Dolly_ cosplay-第一张   -第1张去掉-
                int index = key.IndexOf("cosplay",key.IndexOf("cosplay")+1);
                string path = RemoveInvalidChars(key.Substring(0,index-1)) + "\\" + RemoveInvalidChars(key) + ".jpg";
                var task = Task.Run(async () =>
                {
                    Console.WriteLine($"正在将文件保存到{path}");
                    var result = await DownloadFromURL(dict[key], path);
                    if (!result)
                    {
                        Console.WriteLine($"{key}下载失败，等待重新下载");
                        return;
                    }
                });
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
        }
    }
}







