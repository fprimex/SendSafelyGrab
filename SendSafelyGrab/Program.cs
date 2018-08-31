using System;
using System.IO;
using System.Collections.Generic;

using CommandLine;
using SendSafely;
using SendSafely.Utilities;


namespace SendSafelyGrab
{
    class Options
    {
        [Option('h', "host", Required = false,
                HelpText = "SendSafely hostname to connect to.")]
        public string SendSafelyHost { get; set; }

        [Option('k', "key", Required = false,
                HelpText = "SendSafely API key.")]
        public string SendSafelyKey { get; set; }

        [Option('s', "secret", Required = false,
                HelpText = "SendSafely API secret.")]
        public string SendSafelySecret { get; set; }

        [Option('d', "dir", Required = false,
                HelpText = "Download directory.")]
        public string DownloadDir { get; set; }

        [Option('v', "verbose", Required = false,
                Default = false,
                HelpText = "Show verbose output.")]
        public bool Verbose { get; set; }

        [Value(0, MetaName = "text",
               HelpText = "Text containing SendSafely links.",
               Required = true)]
        public string TextLinks { get; set; }
    }

    class SilentProgressCallback : ISendSafelyProgress
    {
        public void UpdateProgress(string prefix, double progress)
        {
            // Do nothing
        }
    }

    class VerboseProgressCallback : ISendSafelyProgress
    {
        public void UpdateProgress(string prefix, double progress)
        {
            Console.Error.WriteLine(prefix + " " + progress + "%");
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                       .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                       .WithNotParsed<Options>((errs) => HandleParseError(errs));
        }

        static void VerboseWriteLine(bool v, string msg)
        {
            if(v)
            {
                Console.Error.WriteLine(msg);
            }
        }

        static int RunOptionsAndReturnExitCode(Options options)
        {
            ClientAPI ssApi = new ClientAPI();
            string userEmail = "";

            string sendSafelyHost = options.SendSafelyHost ?? Environment.GetEnvironmentVariable("SS_HOST");
            string userApiKey = options.SendSafelyKey ?? Environment.GetEnvironmentVariable("SS_KEY");
            string userApiSecret = options.SendSafelySecret ?? Environment.GetEnvironmentVariable("SS_SECRET");
            string downloadDir = options.DownloadDir ?? Environment.GetEnvironmentVariable("SS_DIR");
            bool verbose = options.Verbose;
            string textLinks = options.TextLinks;
            string destFile;
            System.IO.FileInfo downloadedFile;

            if (sendSafelyHost == null || userApiKey == null || userApiSecret == null)
            {
                // TODO: figure out how to print usage
                System.Environment.Exit(1);
                return 1;
            }

            try
            {
                SendSafely.Utilities.ParseLinksUtility parser = new SendSafely.Utilities.ParseLinksUtility();
                System.Collections.Generic.List<string> links = parser.Parse(textLinks);

                if(links.Count == 0)
                {
                    return 0;
                }
                else
                {
                        // Initialize the API 
                        ssApi.InitialSetup(sendSafelyHost, userApiKey, userApiSecret);

                        // Verify the API key and Secret before continuing.  
                        // Print the authenticated user's email address to the screen if valid. 
                        userEmail = ssApi.VerifyCredentials();
                }

                foreach (string plink in links)
                {
                    PackageInformation pkgToDownload = ssApi.GetPackageInformationFromLink(plink);
                    foreach (SendSafely.File file in pkgToDownload.Files)
                    {
                        if(! System.IO.Directory.Exists(downloadDir))
                        {
                            System.IO.Directory.CreateDirectory(downloadDir);
                        }

                        destFile = Path.GetFullPath(Path.Combine(downloadDir, file.FileName));

                        if(System.IO.File.Exists(destFile))
                        {
                            VerboseWriteLine(verbose, " SendSafely " + file.FileName + " already present");
                        }
                        else
                        {
                            if(verbose)
                            {
                                VerboseWriteLine(verbose, " Downloading SendSafely " + file.FileName);
                                downloadedFile = ssApi.DownloadFile(pkgToDownload.PackageId,
                                    file.FileId, pkgToDownload.KeyCode,
                                    new SilentProgressCallback());
                                    // TODO: make this output pretty and not stupid
                                    //new VerboseProgressCallback());
                            }
                            else
                            {
                                downloadedFile = ssApi.DownloadFile(pkgToDownload.PackageId,
                                    file.FileId, pkgToDownload.KeyCode,
                                    new SilentProgressCallback());
                            }

                            // throws System.IO.IOException on ERROR_ALREADY_EXISTS
                            System.IO.File.Move(downloadedFile.FullName, destFile);
                            Console.WriteLine(file.FileName);
                        }
                    }
                }
            }
            catch (SendSafely.Exceptions.BaseException ex)
            {
                Console.Error.WriteLine(" Error: " + ex.Message);
                System.Environment.Exit(1);
                return 1;
            }
            return 0;
        }

        static int HandleParseError(IEnumerable<CommandLine.Error> errs)
        {
            System.Environment.Exit(1);
            return 1;
        }
    }
}
