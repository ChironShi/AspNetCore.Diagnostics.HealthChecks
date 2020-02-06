using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HealthChecks.NeteaseIM
{
    public class NeteaseIMHealthCheck
        : IHealthCheck
    {
        public NeteaseIMOptions Options { get; protected set; }

        private readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly object SyncRoot = new object();

        public HttpClient ApiClient
        {
            get
            {
                if (_ApiClient == null)
                {
                    lock (SyncRoot)
                    {
                        if (_ApiClient == null)
                        {
                            _ApiClient = new HttpClient
                            {
                                BaseAddress = new Uri(Options.ApiBaseUri)
                            };
                            _ApiClient.Timeout = TimeSpan.FromSeconds(10);
                            _ApiClient.DefaultRequestHeaders.Add("AppKey", Options.AppKey);
                            _ApiClient.DefaultRequestHeaders.Add("Nonce", Options.Nonce);
                        }
                    }
                }
                return _ApiClient;
            }
        }
        private static HttpClient _ApiClient;

        public NeteaseIMHealthCheck(NeteaseIMOptions opts)
        {
            if (opts == null)
            {
                throw new ArgumentNullException(nameof(NeteaseIMOptions));
            }
            Options = opts;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var currTime = GetCurrUnixTicks();
                if (Options.TimePrecision != 13) currTime /= 1000;
                var curTimeStr = currTime.ToString();

                var msg = new HttpRequestMessage(HttpMethod.Post, Options.RequestUri);
                msg.Headers.Add("CurTime", curTimeStr);
                msg.Headers.Add("CheckSum", GenCheckSum(curTimeStr));
                msg.Content = new StringContent(Options.ParamsDict?.Any() != true ? string.Empty : string.Join("&", Options.ParamsDict.Where(d => d.Value != null && d.Value as string != "").ToDictionary(d => d.Key, d => d.Value is bool ? d.Value.ToString().ToLower() : d.Value.ToString()).Select(d => $"{d.Key}={WebUtility.UrlEncode(d.Value)}")), Encoding.UTF8);
                msg.Content.Headers.Remove("Content-Type");
                msg.Content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                var rStr = await Task.Run(async () =>
                {
                    var r = await TimeoutAfter(ApiClient.SendAsync(msg), ApiClient.Timeout);
                    return await TimeoutAfter(r.Content.ReadAsStringAsync(), ApiClient.Timeout);
                });

                var rsp = JsonConvert.DeserializeObject<NeteaseIMHealthCheckApiResponse>(rStr);
                if ((string.IsNullOrEmpty(Options.HealthyCode) || rsp.Code == Options.HealthyCode)
                    && (string.IsNullOrEmpty(Options.HealthyDesc) || rsp.Desc == Options.HealthyDesc))
                {
                    return HealthCheckResult.Healthy();
                }

                return HealthCheckResult.Unhealthy(rStr);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
            }
        }

        private string GenCheckSum(string currTime, string nonce = null)
        {
            var argsStr = string.Join("", new[] { Options.AppSecret, nonce ?? Options.Nonce, currTime });
            var sha1 = new SHA1CryptoServiceProvider();
            var signature = BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(argsStr))).Replace("-", "").ToLower();
            return signature;
        }

        private long GetCurrUnixTicks()
        {
            var time = DateTime.UtcNow;
            TimeSpan timeSpan = new TimeSpan(time.Ticks - Epoch.Ticks);
            return (long)Math.Round(timeSpan.TotalMilliseconds, 0);
        }

        private async Task<TResult> TimeoutAfter<TResult>(Task<TResult> task, TimeSpan timeout, string timeoutMsg = null)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException(string.IsNullOrEmpty(timeoutMsg) ? "The operation has timed out." : timeoutMsg);
                }
            }
        }

        private class NeteaseIMHealthCheckApiResponse
        {
            public string Code { get; set; }
            public string Desc { get; set; }
        }
    }
}
