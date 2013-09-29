// Permission is hereby granted, free of charge, to any person obtaining 
// a copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions: 
//  
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software. 
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
// 
// Copyright (c) 2013 Sandy Armstrong <sanfordarmstrong@gmail.com>
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using LibGit2Sharp;

using Mono.Options;

namespace OrbitalCommand
{
	internal class Program
	{
		private static void Main (string[] args)
		{
			var showHelp = false;
			var fogbugzBaseUrl = String.Empty;
			var fogbugzToken = String.Empty;
			var logoffToken = false;
			var options = new OptionSet ()
			{
				{
					"s|server=", "base URL of fogbugz API server",
					v => fogbugzBaseUrl = v
				},
				{
					"k|token=", "fogbugz API token; if this is not provided but server is, you will be prompted for email and password",
					v => fogbugzToken = v
				},
				{
					"l|logoff", "invalidate token when done",
					v => logoffToken = v != null
				},
				{
					"h|help", "show this message and exit",
					v => showHelp = v != null
				}

			};

			List<string> extra;
			try {
				extra = options.Parse (args);
			} catch (OptionException e) {
				Console.WriteLine("Unexpected error: {0}", e.Message);
				Console.WriteLine("Try `OrbitalCommand --help` for more information");
				return;
			}

			if (showHelp || extra.Count != 1
				|| (String.IsNullOrEmpty (fogbugzBaseUrl) && !String.IsNullOrEmpty (fogbugzToken))) {
				ShowHelp (options);
				return;
			}

			var untilTag = extra [0];

			if (!String.IsNullOrEmpty (fogbugzBaseUrl) && String.IsNullOrEmpty (fogbugzToken)) {
				fogbugzToken = GetFogbugzToken (fogbugzBaseUrl);
				if (String.IsNullOrEmpty (fogbugzToken)) {
					Console.Error.WriteLine ("Error logging in to fogbugz; no titles for you!");
				} else {
					Console.WriteLine("Save your new fogbugz token: {0}", fogbugzToken);
				}
				Console.WriteLine ();
			}

			OutputDraftNotes (untilTag, fogbugzToken, fogbugzBaseUrl, logoffToken);
		}

		private static void OutputDraftNotes (string untilTag, string fogbugzToken, string fogbugzBaseUrl, bool logoffToken)
		{
			try {
				using (var repo = new Repository (Environment.CurrentDirectory)) {
					CommitFilter filter;
					try {
						filter = new CommitFilter
						{
							// TODO: Allow any commitish value here
							Until = repo.Tags [untilTag],
							Since = repo.Head,
							SortBy = CommitSortStrategies.Time
						};
					} catch (LibGit2SharpException) {
						Console.Error.WriteLine ("{0} does not appear to be a valid tag ref", untilTag);
						return;
					}

					var regex = new Regex (@"Fixes:\s*(?<bugUrl>http\S+\?(?<bugNum>\d+))",
						RegexOptions.Multiline | RegexOptions.Compiled);

					var idToUrl = new Dictionary<string, string> ();

					foreach (var commit in repo.Commits.QueryBy (filter)) {
						var change = new StringBuilder ("* ");
						change.Append (commit.MessageShort);
						var matches = regex.Matches (commit.Message);
						if (matches.Count > 0) {
							change.Append (" (");
							var first = true;
							foreach (Match match in matches) {
								var bugNum = match.Groups ["bugNum"].Value;
								var bugUrl = match.Groups ["bugUrl"].Value;
								idToUrl [bugNum] = bugUrl;

								if (!first)
									change.Append (", ");
								change.Append (String.Format ("[{0}][{0}]", bugNum));
								first = false;
							}
							change.Append (")");
						}
						Console.WriteLine (change);
					}

					Console.WriteLine ();

					foreach (var pair in idToUrl) {
						var title = String.Empty;
						if (!String.IsNullOrEmpty (fogbugzToken)) {
							var fogbugzClient = new HttpClient ();
							var queryResultXml = fogbugzClient.GetStringAsync (String.Format (
								"{0}/api.asp?cmd=search&q={1}&cols=sTitle&token={2}", fogbugzBaseUrl, pair.Key, fogbugzToken)).Result;
							var xmlDoc = new XmlDocument ();
							xmlDoc.Load (new StringReader (queryResultXml));
							var titleNode = xmlDoc.SelectSingleNode ("//sTitle");
							if (titleNode == null)
								Console.Error.WriteLine ("Error getting title for bug {0}. Invalid token?", pair.Key);
							else
								title = titleNode.InnerText;
						}
						Console.WriteLine ("[{0}]: {1} \"{2}\"", pair.Key, pair.Value, title);
					}
				}
			} catch (RepositoryNotFoundException) {
				Console.Error.WriteLine ("Please run from within a git repository");
				return;
			} finally {
				// Invalidate fogbugz token, if we have one
				if (logoffToken && !String.IsNullOrEmpty (fogbugzToken))
					new HttpClient ().GetAsync (String.Format (
						"{0}/api.asp?cmd=logoff&token={1}", fogbugzBaseUrl, fogbugzToken)).Wait ();
			}
		}

		private static string ReadPassword ()
		{
			var password = new StringBuilder ();
			while (true) {
				var info = Console.ReadKey (true);
				if (info.Key == ConsoleKey.Enter) {
					Console.WriteLine ();
					break;
				} else if (info.Key == ConsoleKey.Backspace && password.Length > 0) {
					password.Remove (password.Length - 1, 1);
				} else {
					password.Append (info.KeyChar);
				}
			}
			return password.ToString ();
		}

		private static void ShowHelp (OptionSet options)
		{
			Console.WriteLine ("Usage: OrbitialCommand [OPTIONS]+ tagname");
			Console.WriteLine ("Generate a draft of markdown release notes based on changes since tagname.");
			Console.WriteLine ();
			Console.WriteLine ("Options:");
			options.WriteOptionDescriptions (Console.Out);
		}

		private static string GetFogbugzToken (string baseUrl)
		{
			Console.Write ("Please type your fogbugz email, then press ENTER: ");
			var email = Console.ReadLine ();
			Console.Write ("Please type your fogbugz password, then press ENTER: ");
			var password = ReadPassword ();

			var fogbugzClient = new HttpClient ();
			var logonXml = fogbugzClient.GetStringAsync (String.Format (
				"{0}/api.asp?cmd=logon&email={1}&password={2}", baseUrl, email, password)).Result;
			var xmlDoc = new XmlDocument ();
			xmlDoc.Load (new StringReader (logonXml));
			var tokenNode = xmlDoc.SelectSingleNode ("//token");
			var token = String.Empty;
			if (tokenNode != null)
				token = tokenNode.InnerText;
			return token;
		}
	}
}
