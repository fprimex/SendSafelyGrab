using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SendSafely;

namespace SendSafelyGrab
{
    class ProgressCallback : ISendSafelyProgress
    {
        public void UpdateProgress(string prefix, double progress)
        {
            Console.WriteLine(prefix + " " + progress + "%");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            /*
             * This example will read in the following command line arguments:
             *
             *        SendSafelyHost: The SendSafely hostname to connect to.  Enterprise users should connect to their designated 
             *                        Enterprise host (company-name.sendsafely.com)
             *
             *            UserApiKey: The API key for the user you want to connect to.  API Keys can be obtained from the Edit 
             *                        Profile screen when logged in to SendSafely
             *
             *         UserApiSecret: The API Secret associated with the API Key used above.  The API Secret is provided to  
             *                        you when you generate a new API Key.  
             *
             *           PackageLink: Link to file to download. 
             *
             */

            if (args == null || args.Length != 4)
            {
                // Invalid number of arguments.  Print the usage syntax to the screen and exit. 
                Console.WriteLine("Usage: " + System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + " SendSafelyHost UserApiKey UserApiSecret PackageLink");
                Console.WriteLine("\n");
                Console.ReadLine();
                return;
            }
            else
            {
                // Valid arguments provided.  Assign each argument to a local variable 
                String sendSafelyHost = args[0];
                String userApiKey = args[1];
                String userApiSecret = args[2];
                String packageLink = args[3];
                String packageId = String.Empty;

                // Initialize the API 
                Console.WriteLine("Initializing API");
                ClientAPI ssApi = new ClientAPI();
                ssApi.InitialSetup(sendSafelyHost, userApiKey, userApiSecret);

                try
                {
                    // Verify the API key and Secret before continuing.  
                    // Print the authenticated user's email address to the screen if valid. 
                    String userEmail = ssApi.VerifyCredentials();
                    Console.WriteLine("Connected to SendSafely as user " + userEmail);

                    // Download the file again.
                    PackageInformation pkgToDownload = ssApi.GetPackageInformationFromLink(packageLink);
                    foreach (File file in pkgToDownload.Files)
                    {
                        System.IO.FileInfo downloadedFile = ssApi.DownloadFile(pkgToDownload.PackageId, file.FileId, pkgToDownload.KeyCode, new ProgressCallback());
                        Console.WriteLine("Downloaded File to path: " + downloadedFile.FullName);
                    }
                }
                catch (SendSafely.Exceptions.BaseException ex)
                {
                    // Catch any custom SendSafelyAPI exceptions and determine how to properly handle them
                    if (ex is SendSafely.Exceptions.FileUploadException || ex is SendSafely.Exceptions.InvalidEmailException || ex is SendSafely.Exceptions.InvalidPhonenumberException || ex is SendSafely.Exceptions.InvalidRecipientException || ex is SendSafely.Exceptions.PackageFinalizationException || ex is SendSafely.Exceptions.ApproverRequiredException)
                    {
                        // These exceptions indicate a problem that occurred during package preparation.  
                        // If a package was created, delete it so it does not remain in the user's incomplete pacakge list.  
                        Console.WriteLine("Error: " + ex.Message);
                        if (!String.IsNullOrEmpty(packageId))
                        {
                            ssApi.DeleteTempPackage(packageId);
                            Console.WriteLine("Deleted Package - Id#:" + packageId);
                        }
                    }
                    else
                    {
                        // Throw the exception if it was not one of the specific ones we handled by deleting the package. 
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }
        }
    }
}
