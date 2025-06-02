namespace NNTPImporter.Helpers
{
	public class NewsgroupInfo
	{
		public required string Name { get; set; }
		public int EstimatedCount { get; set; }
		public int FirstArticle { get; set; }
		public int LastArticle { get; set; }
	}

	public class NewsgroupStatus
	{
		public required string Name { get; set; }
		public int LastArticleDownloaded { get; set; }
	}


	public static class GroupArticleHelper
	{
		public static async Task<NewsgroupInfo?> GetArticleRangeAsync(StreamWriter writer, StreamReader reader, string newsgroup)
		{
			await writer.WriteLineAsync($"GROUP {newsgroup}");
			await writer.FlushAsync();

			string? groupResponse = await reader.ReadLineAsync();

			if (groupResponse != null && groupResponse.StartsWith("211"))
			{
				string[] parts = groupResponse.Split(' ');
				return new NewsgroupInfo
				{
					Name = newsgroup,
					EstimatedCount = int.Parse(parts[1]),
					FirstArticle = int.Parse(parts[2]),
					LastArticle = int.Parse(parts[3])
				};
			}
			else
			{
				Console.WriteLine($"{DateTime.Now} - (DOWNLOADER): Failed to select group, possible server authentication issue.");
				return null;
			}
		}

	}

	public static class NewgroupStatusHelper
	{
		public static async Task<List<NewsgroupStatus>?> GetNewsgroupStatus(string importerConfigFile)
		{
		
			List<NewsgroupStatus> groups = new List<NewsgroupStatus>();
			
			try
			{
				await foreach (string line in File.ReadLinesAsync(importerConfigFile))
				{
					string[] parts = line.Split(' ');
					groups.Add(new NewsgroupStatus { Name = parts[0], LastArticleDownloaded = int.Parse(parts[1]) });
				}
				return groups;
			}
			catch (Exception)
			{
				throw new Exception($"{DateTime.Now} - (DOWNLOADER) FATAL ERROR: An error occurred while reading NNTPImporter.cfg");
			}
		}

		public static async Task<bool> WriteNewsgroupStatus(string importerConfigFile, List<NewsgroupStatus> groups)
		{
			try
			{
				using (StreamWriter writer = new StreamWriter(importerConfigFile, false))
				{
					foreach (var group in groups)
					{
						await writer.WriteLineAsync($"{group.Name} {group.LastArticleDownloaded}");
					}
				}
				return true;
			}
			catch (Exception)
			{
				throw new Exception($"{DateTime.Now} - (DOWNLOADER) FATAL ERROR: An error occurred while writing to NNTPImporter.cfg");
			}
		}
	}
}
