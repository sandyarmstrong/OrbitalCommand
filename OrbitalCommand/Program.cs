using System;
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
				Console.WriteLine("Too few arguments");
				return;
			}
			var until = args[0];

			using (var repo = new Repository(Environment.CurrentDirectory))
			{
				var filter = new CommitFilter
					{
						Until = repo.Tags[until],
						Since = repo.Head,
						SortBy = CommitSortStrategies.Time
					};

				var regex = new Regex (@"Fixes:.+\?(?<bugNum>.....)", RegexOptions.Multiline | RegexOptions.Compiled);
				
				foreach (var commit in repo.Commits.QueryBy(filter))
				{
					var change = new StringBuilder(commit.MessageShort);
					var matches = regex.Matches(commit.Message);
					if (matches.Count > 0)
					{
						change.Append(" (");
						var first = true;
						foreach (Match match in matches)
						{
							if (!first)
								change.Append(", ");
							change.Append(match.Groups["bugNum"].Value);
							first = false;
						}
						change.Append(")");
					}
					Console.WriteLine(change);
				}
			}

			Console.ReadLine();
		}
	}
}
