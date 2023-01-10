using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace GroupMeAPI
{
	internal static class WebRequester
	{
		/// <summary> A lock to prevent simultaneous hits on GroupMe </summary>
		private static readonly object _webLock = new();
		private static DateTime _lastQueryTime = DateTime.MinValue;
		const int MinMSBtwQueries = 250;

		internal static (HttpStatusCode StatusCode, string? Response) Get(string url, Dictionary<string, object>? arguments = null)
			=> PerformWebRequest(url, arguments, (client, url) => client.GetAsync(url));

		internal static (HttpStatusCode StatusCode, string? Response) Post<T>(string url, T value, Dictionary<string, object>? arguments = null)
			=> PerformWebRequest(url, arguments, (client, url) => client.PostAsJsonAsync(url, value));

		private static (HttpStatusCode StatusCode, string? Response) PerformWebRequest(string url, Dictionary<string, object>? arguments, Func<HttpClient, string, Task<HttpResponseMessage>> action)
		{
			int errors = 0;
			beginning:

			lock (_webLock)
			{
				using var client = new HttpClient();
				UriBuilder builder = new(url);
				if (arguments is not null)
				{
					var nonBlankArgs = arguments.Where(kv => kv.Value is not null && !string.IsNullOrWhiteSpace(kv.Value.ToString()));
					builder.Query = string.Join("&", nonBlankArgs.Select(kv => $"{kv.Key}={kv.Value}"));
				}
				var resultUrl = builder.ToString();
				var result = action(client, resultUrl).Result;

				_lastQueryTime = DateTime.Now;

				switch (result.StatusCode)
				{
					case HttpStatusCode.OK:
					case HttpStatusCode.NotModified:
					case HttpStatusCode.Accepted:
					case (HttpStatusCode)418:
						return (result.StatusCode, result.Content.ReadAsStringAsync().Result);
					case HttpStatusCode.GatewayTimeout:
						if (errors < 3)
						{
							errors++;
							goto beginning;
						}
						else
						{
							return (result.StatusCode, null);
						}
					default:
						_ = 0;
						break;
				}
				throw new NotImplementedException(result.StatusCode.ToString());
			}
		}
	}
}
