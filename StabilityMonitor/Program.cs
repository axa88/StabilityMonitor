using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using static System.Console;
using static System.Environment;


namespace StabilityMonitor
{
	internal static class Program
	{
		private static string _pathFileExt;
		private static readonly Regex ValidIpOptionalPort = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])(:[0-9]+)?$");
		private static readonly Regex ValidHostname = new Regex(@"^(?=.{1,255}$)[0-9A-Za-z](?:(?:[0-9A-Za-z]|-){0,61}[0-9A-Za-z])?(?:\.[0-9A-Za-z](?:(?:[0-9A-Za-z]|-){0,61}[0-9A-Za-z])?)*\.?$");

		private static bool _isFailed;
		private static Ping _ping;
		private static Timer _timer;

		private static void Main()
		{
			var dir = Path.Combine(GetFolderPath(SpecialFolder.DesktopDirectory), "NetworkMonitorLog");
			Directory.CreateDirectory(dir);
			_pathFileExt = Path.Combine(dir, "Log.txt");
			var file = File.Open(_pathFileExt, FileMode.Create);
			file.Close();


			int period = default;
			string host = default;

			while (string.IsNullOrEmpty(host))
			{
				Thread.Sleep(500);

				WriteLine("ping who:");
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

				WriteLine("ping how often (seconds):");
				var per = ReadLine();
				if (per != default && int.TryParse(per, out var p))
					period = p;
			}

			_ping = new Ping();

			_timer = new Timer(TimerCallback, default, 0, Timeout.Infinite);

			using (var sw = new StreamWriter(_pathFileExt, true))
			{
				sw.WriteLine($"{DateTime.Now} Monitoring {host}");
				sw.Close();
			}

			void TimerCallback(object state)
			{
				try { PingHost(host); }
				catch (Exception exception)
				{
					using var sw = new StreamWriter(_pathFileExt, true);
					sw.WriteLine(exception.Message);
					sw.Close();

					if (!(exception is PingException))
						Exit(0);
				}

				_timer.Change(period * 1000, Timeout.Infinite);
			}

			Thread.Sleep(Timeout.Infinite);
		}

		private static void PingHost(string address)
		{
			var reply = _ping.Send(address);
			if (reply == null)
				throw new InvalidOperationException("reply is null");

			if (reply.Status != IPStatus.Success)
			{
				if (!_isFailed)
				{
					_isFailed = true;

					using var sw = new StreamWriter(_pathFileExt, true);
					sw.WriteLine($"{DateTime.Now} {address} {reply.Status}");
					sw.Close();
				}
			}
			else if (_isFailed)
			{
				_isFailed = false;
				using var sw = new StreamWriter(_pathFileExt, true);
				sw.WriteLine($"{DateTime.Now} {address} {reply.Status}");
				sw.Close();
			}
		}

		private static async Task<(bool isResolved, string ipOrException)> ResolveHost(string host)
		{
			try { return(true, (await Dns.GetHostAddressesAsync(host).ConfigureAwait(false)).First().ToString()); }
			catch (Exception e) { return(false, e.Message); }
		}
	}
}