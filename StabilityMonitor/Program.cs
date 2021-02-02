using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static System.Console;


namespace StabilityMonitor
{
	class Program
	{
		private static string _pathFileExt;
		private static readonly Regex ValidIpOptionalPort = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])(:[0-9]+)?$");
		private static readonly Regex ValidHostname = new Regex(@"^(?=.{1,255}$)[0-9A-Za-z](?:(?:[0-9A-Za-z]|-){0,61}[0-9A-Za-z])?(?:\.[0-9A-Za-z](?:(?:[0-9A-Za-z]|-){0,61}[0-9A-Za-z])?)*\.?$");

		private static bool _isFailed;
		private static Ping _ping;
		private static Timer _timer;

		static void Main(string[] args)
		{
			var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NetworkMonitorLog");
			Directory.CreateDirectory(dir);
			_pathFileExt = Path.Combine(dir, "Log.txt");
			var file = File.Open(_pathFileExt, FileMode.Create);
			file.Close();


			int period = default;
			string host = default;

			while (string.IsNullOrEmpty(host))
			{
				Thread.Sleep(500);

				WriteLine("host:");
				var h = ReadLine();
				if (!string.IsNullOrWhiteSpace(h) && (ValidIpOptionalPort.Match(h).Success || ValidHostname.Match(h).Success))
				{
					var (isResolved, ipOrException) = ResolveHost(h).Result;
					if (isResolved)
						host = ipOrException;
					else
						WriteLine(ipOrException);
				}
			}

			while (period == 0)
			{
				Thread.Sleep(500);

				WriteLine("ping frequency in seconds:");
				var per = ReadLine();
				if (per != default && int.TryParse(per, out var p))
					period = p;
			}

			_ping = new Ping();

			_timer = new Timer(TimerCallback, default, 0, Timeout.Infinite);

			void TimerCallback(object state)
			{
				PingHost(host);
				_timer.Change(period * 1000, Timeout.Infinite);
			}

			Thread.Sleep(Timeout.Infinite);
		}

		private static void PingHost(string nameOrAddress)
		{
			try
			{
				var reply = _ping.Send(nameOrAddress);
				if (reply == null)
					throw new InvalidOperationException("reply is null");

				if (reply.Status != IPStatus.Success)
				{
					if (!_isFailed)
					{
						_isFailed = true;

						using var sw = new StreamWriter(_pathFileExt, true);
						sw.WriteLine($"{DateTime.Now} {reply.Address} {reply.Status}");
						sw.Close();
					}
				}
				else if (_isFailed)
				{
					_isFailed = false;
					using var sw = new StreamWriter(_pathFileExt, true);
					sw.WriteLine($"{DateTime.Now} {reply.Address} {reply.Status}");
					sw.Close();
				}

			}
			catch (PingException pingException)
			{
				using var sw = new StreamWriter(_pathFileExt, true);
				sw.WriteLine(pingException.Message);
			}
		}

		private static async Task<(bool isResolved, string ipOrException)> ResolveHost(string host)
		{
			try { return(true, (await Dns.GetHostAddressesAsync(host).ConfigureAwait(false)).First().ToString()); }
			catch (Exception e) { return(false, e.Message); }
		}
	}
}