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
using System.Text;
using System.Text.RegularExpressions;

using LibGit2Sharp;

namespace OrbitalCommand
{
	class Program
	{
		static void Main (string [] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("Wrong number of arguments");
				return;
			}
			var until = args[0];

			try
			{
				using (var repo = new Repository(Environment.CurrentDirectory))
				{
					CommitFilter filter;
					try
					{
						filter = new CommitFilter
							{
								// TODO: Allow any commitish value here
								Until = repo.Tags[until],
								Since = repo.Head,
								SortBy = CommitSortStrategies.Time
							};
					}
					catch (LibGit2SharpException)
					{
						Console.WriteLine("{0} does not appear to be a valid tag ref", until);
						return;
					}

					var regex = new Regex(@"Fixes:\s*(?<bugUrl>http\S+\?(?<bugNum>\d+))",
					                      RegexOptions.Multiline | RegexOptions.Compiled);

					var idToUrl = new Dictionary<string, string>();

					foreach (var commit in repo.Commits.QueryBy(filter))
					{
						var change = new StringBuilder("* ");
						change.Append(commit.MessageShort);
						var matches = regex.Matches(commit.Message);
						if (matches.Count > 0)
						{
							change.Append(" (");
							var first = true;
							foreach (Match match in matches)
							{
								var bugNum = match.Groups["bugNum"].Value;
								var bugUrl = match.Groups["bugUrl"].Value;
								idToUrl[bugNum] = bugUrl;

								if (!first)
									change.Append(", ");
								change.Append(String.Format("[{0}][{0}]", bugNum));
								first = false;
							}
							change.Append(")");
						}
						Console.WriteLine(change);
					}

					Console.WriteLine();

					foreach (var pair in idToUrl)
					{
						Console.WriteLine("[{0}]: {1}", pair.Key, pair.Value);
					}
				}
			}
			catch (RepositoryNotFoundException)
			{
				Console.WriteLine("Please run from within a git repository");
				return;
			}
		}
	}
}
