using System;
using System.Collections.Generic;

namespace HealthChecks.NeteaseIM
{
    public class NeteaseIMOptions
    {
        /// <summary>
        /// 基础地址，默认https://api.netease.im/
        /// </summary>
        public string ApiBaseUri { get; set; } = "https://api.netease.im/";

        /// <summary>
        /// 具体接口地址，默认是文件上传接口nimserver/msg/upload.action
        /// </summary>
        public string RequestUri { get; set; } = "nimserver/msg/upload.action";

        public string AppId { get; set; }

        public string AppKey { get; set; }

        public string AppSecret { get; set; }

        /// <summary>
        /// 接口的参数，默认是content=""，用以返回content参数错误的结果
        /// </summary>
        public Dictionary<string, object> ParamsDict { get; set; } = new Dictionary<string, object>() { { "content", "" } };

        public string Nonce
        {
            get
            {
                if (string.IsNullOrEmpty(_Nonce)) _Nonce = Guid.NewGuid().ToString();
                return _Nonce;
            }
        }
        private string _Nonce;

        /// <summary>
        /// 时间戳精度，默认13位
        /// </summary>
        public int TimePrecision { get; set; } = 13;

        /// <summary>
        /// 返回健康的状态码code值，默认是414；String.Empty可以忽略
        /// </summary>
        public string HealthyCode { get; set; } = "414";

        /// <summary>
        /// 返回健康的状态码desc值，默认是content is empty；String.Empty可以忽略
        /// </summary>
        public string HealthyDesc { get; set; } = "content is empty";
    }
}
