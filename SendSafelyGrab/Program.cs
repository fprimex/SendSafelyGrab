using System;
using System.IO;
using System.Collections.Generic;

using CommandLine;
using SendSafely;


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

        [Option('v', "verbose", Required = false,
                Default = false,
                HelpText = "Show verbose output.")]
        public bool Verbose { get; set; }

        [Value(0, MetaName = "link",
               HelpText = "SendSafely package link.",
               Required = true)]
        public string SendSafelyLink { get; set; }
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
            string sendSafelyHost = options.SendSafelyHost ?? Environment.GetEnvironmentVariable("SS_HOST");
            string userApiKey = options.SendSafelyKey ?? Environment.GetEnvironmentVariable("SS_KEY");
            string userApiSecret = options.SendSafelySecret ?? Environment.GetEnvironmentVariable("SS_SECRET");
            bool verbose = options.Verbose;
            string packageLink = options.SendSafelyLink;
            string packageId = "";

            if (sendSafelyHost == null || userApiKey == null || userApiSecret == null)
            {
                // TODO: figure out how to print usage
                return 1;
            }

            // Initialize the API 
            VerboseWriteLine(verbose, "Initializing API");
            ClientAPI ssApi = new ClientAPI();
            ssApi.InitialSetup(sendSafelyHost, userApiKey, userApiSecret);

            try
            {
                // Verify the API key and Secret before continuing.  
                // Print the authenticated user's email address to the screen if valid. 
                string userEmail = ssApi.VerifyCredentials();
                VerboseWriteLine(verbose, "Connected to SendSafely as user " + userEmail);

                // Download the file again.
                PackageInformation pkgToDownload = ssApi.GetPackageInformationFromLink(packageLink);
                foreach (SendSafely.File file in pkgToDownload.Files)
                {
                    if(! System.IO.File.Exists(file.FileName))
                    {
                        System.IO.FileInfo downloadedFile;
                        if(verbose)
                        {
                            downloadedFile = ssApi.DownloadFile(pkgToDownload.PackageId, file.FileId, pkgToDownload.KeyCode, new VerboseProgressCallback());
                        }
                        else
                        {
                            downloadedFile = ssApi.DownloadFile(pkgToDownload.PackageId, file.FileId, pkgToDownload.KeyCode, new SilentProgressCallback());
                        }

                        VerboseWriteLine(verbose, "Downloaded File to path: " + downloadedFile.FullName);
                        // throws System.IO.IOException on ERROR_ALREADY_EXISTS
                        System.IO.File.Move(downloadedFile.FullName, System.IO.Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + file.FileName);
                        VerboseWriteLine(verbose, "Moved file to: " + System.IO.Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + file.FileName);
                }
            }
            catch (SendSafely.Exceptions.BaseException ex)
            {
                // Catch any custom SendSafelyAPI exceptions and determine how to properly handle them
                if (ex is SendSafely.Exceptions.FileUploadException || ex is SendSafely.Exceptions.InvalidEmailException || ex is SendSafely.Exceptions.InvalidPhonenumberException || ex is SendSafely.Exceptions.InvalidRecipientException || ex is SendSafely.Exceptions.PackageFinalizationException || ex is SendSafely.Exceptions.ApproverRequiredException)
                {
                    // These exceptions indicate a problem that occurred during package preparation.  
                    // If a package was created, delete it so it does not remain in the user's incomplete pacakge list.  
                    VerboseWriteLine(verbose, "Error: " + ex.Message);
                    if (!String.IsNullOrEmpty(packageId))
                    {
                        ssApi.DeleteTempPackage(packageId);
                        VerboseWriteLine(verbose, "Deleted Package - Id#:" + packageId);
                    }
                }
                else
                {
                    // Throw the exception if it was not one of the specific ones we handled by deleting the package. 
                    VerboseWriteLine(verbose, "Error: " + ex.Message);
                }
            }
            return 0;
        }

        static int HandleParseError(IEnumerable<CommandLine.Error> errs)
        {
            return 1;
        }
    }
}
