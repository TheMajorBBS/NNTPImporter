using NNTPImporter.Helpers;
using System.Net.Sockets;
using System.Text;

class Program
{
	static async Task Main(string[] args)
	{
		if (args.Length < 8)
		{
			Console.WriteLine("Usage: NNTPImporter <MBBS server> <import directory path> [interval in minutes] <NNTP server> <username> <password> <downloadpath> [interval in minutes]");
			Console.WriteLine("\r\n<MBBS server> = IP/hostname of your MBBS Server");
			Console.WriteLine("\r<import directory path> = location of downloaded newsgroup articles");
			Console.WriteLine("\r[interval in minutes] = how often to run the importer task (default=5)");
			Console.WriteLine("\r<NNTP server> = IP/hostname of your NNTP Server");
			Console.WriteLine("\r<username> = Username if required by NNTP server, else type nil");
			Console.WriteLine("\r<password> = Password if required by NNTP server, else type nil");
			Console.WriteLine("\r<downloadpath> = locate to download newsgroup articles");
			Console.WriteLine("\r[interval in minutes] = how often to run the downloader task (default=5)");
			Console.WriteLine("\r\nExample (NNTP server requires authentication):");
			Console.WriteLine("\rNNTPImporter 192.168.1.99 nntp_downloads 15 news.eternal-september.org myusername mypassword nntp_downloads 10");
			Console.WriteLine("\r\nExample (NNTP does not require authentication):");
			Console.WriteLine("\rNNTPImporter 192.168.1.99 nntp_downloads 15 news.another-provider.com nil nil nntp_downloads 10");
			Console.WriteLine("\r(Both examples above will run the importer every 15 minutes and the downloader every 10 minutes)");
			Console.WriteLine("\r\nNote: Paths are relative unless specified");
			return;
		}

		using CancellationTokenSource cts = new();

		string MBBSServer = args[0];
		string importerDirectoryPath = args[1];
		int importerIntervalMinutes = 5;
		int downloaderIntervalMinutes = 5;
		string nntpServer = args[3];
		string userName = args[4];
		string userPassword = args[5];
		string downloadDirectoryPath = args[6];

		if (args.Length >= 3 && int.TryParse(args[2], out int parsedMBBSInterval))
		{
			importerIntervalMinutes = Math.Max(1, parsedMBBSInterval);
		}

		if (args.Length >= 7 && int.TryParse(args[7], out int parsedNNTPInterval))
		{
			downloaderIntervalMinutes = Math.Max(1, parsedNNTPInterval);
		}

		const string importerConfigFile = "NNTPImporter.cfg";

		Task importer = importToMBBSAsync(MBBSServer, importerDirectoryPath, importerIntervalMinutes, cts.Token);
		Task downloader = downloadNNTPAsync(nntpServer, userName, userPassword, downloadDirectoryPath, downloaderIntervalMinutes, importerConfigFile, cts.Token);
		Task monitor = Task.WhenAny(importer, downloader).ContinueWith(t =>
		{
			if (t.Result.IsFaulted)
			{
				cts.Cancel();
			}
		});

		try
		{
			await Task.WhenAll(importer, downloader);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Unrecoverable error occured: {ex.Message}");
		}

	}

	private static async Task importToMBBSAsync(string MBBSServer, string importerDirectoryPath, int importerIntervalMinutes, CancellationToken ct)
	{
		int port = 119;
		
		Console.WriteLine($"{DateTime.Now} - (IMPORTER): Thread started. MBBS Server: {MBBSServer}");

		try
		{
			if (!Directory.Exists(importerDirectoryPath))
			{
				throw new IOException("(IMPORTER): FATAL ERROR: Import directory not found.");
			}

			while (true)
			{
				try
				{
					Console.WriteLine($"{DateTime.Now} - (IMPORTER): Started importing messages into MBBS.");

					string[] messageFiles = Directory.GetFiles(importerDirectoryPath, "*.txt");

					if (messageFiles.Length == 0)
					{
						Console.WriteLine($"{DateTime.Now} - (IMPORTER): No message files found to import.");
					}
					else
					{
						using var client = new TcpClient();
						using var cts = new CancellationTokenSource(10000);

						using (cts.Token.Register(() => client.Close()))
						{
							try
							{
								await client.ConnectAsync(MBBSServer, port);
								if (!client.Connected)
									throw new SocketException();
							}
							catch
							{
								Console.WriteLine($"{DateTime.Now} - (IMPORTER): Unable to connect to MBBS server, retrying in 30 seconds.");
								client.Dispose();
								await Task.Delay(30000, ct);
								continue;
							}
						}

						using var stream = client.GetStream();
						using var reader = new StreamReader(stream, Encoding.ASCII);
						using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

						string? serverResponse = await reader.ReadLineAsync();

						foreach (string filePath in messageFiles)
						{
							string[] lines;
							try
							{
								lines = await File.ReadAllLinesAsync(filePath);
							}
							catch (Exception ex)
							{
								Console.WriteLine($"{DateTime.Now} - (IMPORTER): Error reading file: " + ex.Message);
								continue;
							}

							string? messageId = ExtractMessageId(lines);
							if (string.IsNullOrWhiteSpace(messageId))
							{
								continue;
							}

							await writer.WriteLineAsync($"IHAVE {messageId}");
							string? response = await reader.ReadLineAsync();

							if (response != null && response.StartsWith("335"))
							{
								foreach (string line in lines)
								{
									string outputLine = line.StartsWith(".") ? "." + line : line;
									await writer.WriteLineAsync(outputLine);
								}

								await writer.WriteLineAsync(".");
								response = await reader.ReadLineAsync();

								if (response != null && response.StartsWith("235"))
								{
									File.Delete(filePath);
								}
								else
								{
									Console.WriteLine($"{DateTime.Now} - (IMPORTER): Message not accepted (not 235). File {Path.GetFileName(filePath)} retained.");
								}
							}
							else if (response != null && response.StartsWith("435"))
							{
								File.Delete(filePath);
							}
							else
							{
								Console.WriteLine($"{DateTime.Now} - (IMPORTER): IHAVE rejected by server. File not sent.");
							}
						}

						await writer.WriteLineAsync("QUIT");
						serverResponse = await reader.ReadLineAsync();
						Console.WriteLine($"{DateTime.Now} - (IMPORTER): Finished importing messages into MBBS.");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"{DateTime.Now} - (IMPORTER): Error: {ex.Message}");
				}

				Console.WriteLine($"{DateTime.Now} - (IMPORTER): Sleeping for {importerIntervalMinutes} minute(s)... (Ctrl+C to exit)");
				await Task.Delay(TimeSpan.FromMinutes(importerIntervalMinutes), ct);
			}
		}
		catch (Exception)
		{
			throw;
		}

	}


	static string? ExtractMessageId(string[] lines)
	{
		foreach (string line in lines)
		{
			if (line.StartsWith("Message-ID:", StringComparison.OrdinalIgnoreCase))
			{
				return line.Substring("Message-ID:".Length).Trim();
			}
		}
		return null;
	}

	
	static async Task downloadNNTPAsync(string nntpServer, string username, string password, string downloadPath, int interval, string importerConfigFile, CancellationToken ct)
	{
		Console.WriteLine($"{DateTime.Now} - (DOWNLOADER): Thread started. NNTP Server: {nntpServer}");

		List<NewsgroupStatus>? groups = new List<NewsgroupStatus>();

		try
		{
			groups = await NewgroupStatusHelper.GetNewsgroupStatus(importerConfigFile);

			if (groups == null)
			{
				throw new Exception($"{DateTime.Now} - (DOWNLOADER): FATAL ERROR: No newsgroups configured in NNTPImporter.cfg");
			}

			int port = 119;
							
			Directory.CreateDirectory(downloadPath);

			while (true)
			{
				try
				{
					TcpClient client = new TcpClient();
					using var cts = new CancellationTokenSource(10000);

					using (cts.Token.Register(() => client.Close()))
					{
						try
						{
							await client.ConnectAsync(nntpServer, port);
							if (!client.Connected)
								throw new SocketException();
						}
						catch
						{
							Console.WriteLine($"{DateTime.Now} - (DOWNLOADER): Unable to connect to NNTP server, retrying in 30 seconds..");
							client.Dispose();
							await Task.Delay(30000, ct);
							continue;
						}
					}

					using (NetworkStream stream = client.GetStream())
					using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
					using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true })
					{
						reader.ReadLine();

						if (username != "nil")
						{
							await writer.WriteLineAsync($"AUTHINFO USER {username}");
							reader.ReadLine();
							await writer.WriteLineAsync($"AUTHINFO PASS {password}");
							reader.ReadLine();
						}

						foreach (var group in groups)
						{
							var info = await GroupArticleHelper.GetArticleRangeAsync(writer, reader, group.Name);
							if (info != null)
							{
								await writer.WriteLineAsync($"GROUP {group.Name}");
								reader.ReadLine();

								if (group.LastArticleDownloaded == 0)
								{
									group.LastArticleDownloaded = info.FirstArticle;
								}

								Console.WriteLine($"{DateTime.Now} - (DOWNLOADER): Downloading new articles for {group.Name}");

								int totalArticlesDownloaded = 0;

								for (int articleId = (group.LastArticleDownloaded+1); articleId <= info.LastArticle; articleId++)
								{
									await writer.WriteLineAsync($"ARTICLE {articleId}");
									string? response = await reader.ReadLineAsync();
									if (response != null && !response.StartsWith("220"))
									{
										continue;
									}

									string filePath = Path.Combine(downloadPath, $"{info.Name}_article_{articleId}.txt");
									using (StreamWriter fileWriter = new StreamWriter(filePath, false))
									{
										string? line;
										while ((line = await reader.ReadLineAsync()) != null && line != ".")
										{
											await fileWriter.WriteLineAsync(line);
										}
									}

									group.LastArticleDownloaded++;
									totalArticlesDownloaded++;
								}

								Console.WriteLine($"{DateTime.Now} - (DOWNLOADER): {totalArticlesDownloaded} new articles downloaded for {group.Name}");
							}
						}
						await writer.WriteLineAsync("QUIT");
						reader.ReadLine();
					}
					await NewgroupStatusHelper.WriteNewsgroupStatus(importerConfigFile, groups);
					Console.WriteLine($"{DateTime.Now} - (DOWNLOADER): Sleeping for {interval} minute(s)... (Ctrl+C to exit)");
					await Task.Delay(TimeSpan.FromMinutes(interval), ct);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"{DateTime.Now} - (DOWNLOADER): Unrecoverable error: {ex.Message}");
					throw;
				}
			}
		}
		catch (Exception)
		{
			throw;
		}
	}
}