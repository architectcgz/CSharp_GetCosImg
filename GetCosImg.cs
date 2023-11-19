using HtmlAgilityPack;
namespace Program
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //string url = "https://www.ciyuanjie.cn/cosplay/";
            var getCosImg = new GetCosImg();
            Console.WriteLine("请输入要获取的图片的页数范围下限(1<=下限<=14)");
            int pageNumLow = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("请输入要获取的图片范围上限(上限<=下限<=14)");
            int pageNumHigh = Convert.ToInt32(Console.ReadLine());
            for(int i = pageNumLow;i<= pageNumHigh; i++)
            {
                Directory.CreateDirectory($"第{i}页cos图");
                var imgURLList = await getCosImg.getImgURLWithPageNum(i);
                Console.WriteLine($"第{i}页有{imgURLList.Count}组图片，开始获取页面中全部图片的url信息");
                var dict = await getCosImg.getEachImgDetail(imgURLList);
                for (int j = 0; j < imgURLList.Count; j++)
                {
                    await getCosImg.DownLoadAll(dict, i);
                }
                Console.WriteLine($"第{i}页cos图下载完成，请查看文件夹");
                Console.WriteLine("防止请求速度过快，休眠1秒");
                await Task.Delay(1000);
            }
           
            Console.WriteLine("已经全部下载完成，请查看文件夹");
            Console.WriteLine("点击任意键退出...");
            Console.ReadKey();
        }
    }
    class GetCosImg
    {
        public async Task<List<string>> getImgURLWithPageNum(int pageNum)
        {
            if (pageNum <1||pageNum>14)
            {
                Console.WriteLine("页数范围输入错误,1<=页数<=14！");
                return new List<string>();
            }
            string url;
            var imgURLList = new List<string>();
            url = $"https://www.ciyuanjie.cn/cosplay/page_{pageNum}.html";
            var result = await getImgURLAndTitleOnePage(url);
            imgURLList.AddRange(result);
            return imgURLList;
        }
        public async Task<List<string>> getImgURLAndTitleOnePage(string url)
        {
            List<string> imgUrlList = new List<string>();
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
                }
            }
            return imgUrlList;
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
                // alt也就是文件名 注意去除title字符串中的空格
                var alt = node.GetAttributeValue("alt", "").ToLower().Replace(" ","");
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
        /// 基于Task的并行处理下载所有图片 下载第pageNum页cos图
        /// 如果图片数量过大，会占用非常大的内存，所以考虑使用线程池替代Task并行处理
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="titleList"></param>
        /// <param name="pageNum">第pageNum页cos图</param>
        /// <returns></returns>
        public async Task DownLoadAll(Dictionary<string, string> dict,int pageNum)
        {
            // 创建一个SemaphoreSlim对象，初始化为24，表示最多允许24个线程同时运行
            //通过在每个任务开始时调用semaphore.Wait() 在每个任务结束时调用semaphore.Release()来实现最多24个线程
            var semaphore = new SemaphoreSlim(24);

            var tasks = new List<Task>();
            foreach (var key in dict.Keys)
            {
                await semaphore.WaitAsync();
                //找到最后一个图片名中的cos
                //【cos正片】好帅的安哥《凹凸世界》安迷修cos欣赏cosplay-第1张.jpg
                //使用【cos正片】好帅的安哥《凹凸世界》安迷修cos欣赏  作为文件夹名
                int folderIndex = key.LastIndexOf("cos");
                string folderPath;
                if(folderIndex > 0)
                {
                    folderPath = RemoveInvalidChars(key.Substring(0, folderIndex));
                }
                else
                {
                    folderPath = RemoveInvalidChars(key);
                }
                
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory($"第{pageNum}页cos图\\{folderPath}");
                }

                string filePath = $"第{pageNum}页cos图\\{folderPath}\\{RemoveInvalidChars(key)}.jpg";
                var task = Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"正在将文件保存到{filePath}");
                        var result = await DownloadFromURL(dict[key], filePath);
                        if (!result)
                        {
                            Console.WriteLine($"{key}下载失败，等待重新下载");
                            return;
                        }
                    }
                    finally
                    {
                        // 完成任务后，释放semaphore  
                        semaphore.Release();
                    }
                    
                });
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
        }
    }
}







