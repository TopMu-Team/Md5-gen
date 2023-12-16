using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace MD5Gen
{
    public class HandleJson
    {
        public string Serialize<T>(T classObj) where T : class, new()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                DataContractJsonSerializer serialiaze = new DataContractJsonSerializer(typeof(T));
                serialiaze.WriteObject(ms, classObj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public T DeSerialize<T>(string classObj) where T : class, new()
        {
            DataContractJsonSerializer deSerialiaze = new DataContractJsonSerializer(typeof(T));
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(classObj)))
            {
                return deSerialiaze.ReadObject(ms) as T;
            }

        }

    }

    public class Config
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string FtpServer { get; set; }
        public string FtpFolder { get; set; }
    }
    internal class Program
    {
        public  static List<string> ExcludeFile =  new List<string>();

        static void Main(string[] args)
        {
            if (Directory.Exists("Patch"))
            {
                Directory.Delete("Patch", true);
            }

            if (File.Exists("Patch.zip"))
            {
                File.Delete("Patch.zip");
            }

            ExcludeFile.Add(Path.Combine(Directory.GetCurrentDirectory(), "FTPConfig.json")); ;
            ExcludeFile.Add(System.Reflection.Assembly.GetEntryAssembly().Location); ;

            var currentDirectory = Directory.GetCurrentDirectory();
            string newTxtFile = "";

            File.Delete(Path.Combine(currentDirectory, "MD5.txt"));
            List<string> allFiles = GetFilesRecursively(currentDirectory);

            foreach (string file in allFiles)
            {
                if(!ExcludeFile.Contains(file))
                {
                    var md5 = CalculateMD5Checksum(file);
                    var size = GetFileSize(file);
                    var path = GetRelativePath(currentDirectory, file);
                    newTxtFile += Path.Combine(path) + ";" + md5 + ";" + size.ToString() + Environment.NewLine ;
                    Console.WriteLine(newTxtFile);
                }
            }
            File.WriteAllText(Path.Combine(currentDirectory, "MD5.txt"), newTxtFile);
            CreateZipFile(allFiles);
            UploadToServer();
        }
        static long GetFileSize(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                return new FileInfo(FilePath).Length;
            }
            return 0;
        }
        static string CalculateMD5Checksum(string filePath)
        {
            using (MD5 md5 = MD5.Create())
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = md5.ComputeHash(stream);

                    // Convert the byte array to a hexadecimal string
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }
            }
        }
        static void CreateFolder()
        {
            string currentDirectory = Directory.GetCurrentDirectory(); // Get the current directory.
            string[] folders = Directory.GetDirectories(currentDirectory);

         
            Directory.CreateDirectory("Patch");
            Directory.CreateDirectory("Patch/Update");
            foreach (string folder in folders)
            {
                var folderName = Path.GetFileName(folder);
                CopyFolder(folder, Path.Combine(currentDirectory, "Patch", "Update", folderName));
            }
            string[] files = Directory.GetFiles(currentDirectory);

            Console.WriteLine("Files in the current directory:");

            foreach (string file in files)
            {
                if(Path.GetFileName(file) == "MD5.txt")
                {
                    continue;
                }
                if (!ExcludeFile.Contains(file))
                {
                    var fileName = Path.GetFileName(file);
                    File.Copy(Path.Combine(Directory.GetCurrentDirectory(), fileName), Path.Combine(currentDirectory, "Patch", "Update", fileName));
                }
              
            }
        }
        static void UploadToServer()
        {
            if(File.Exists("FTPConfig.json"))
            {
                var text = File.ReadAllText("FTPConfig.json");
                Console.WriteLine(text);
                var handleJson = new HandleJson();
                var config = handleJson.DeSerialize<Config>(text);
                Console.WriteLine(config.FtpFolder);

                string ftpServer = config.FtpServer;
                string ftpFolder = config.FtpFolder;
                string username = config.Username;
                string password = config.Password;
                string localFolder = Path.Combine(Directory.GetCurrentDirectory(), "Patch");

                // Create the FTP request URI
                Uri uri = new Uri(ftpServer + ftpFolder);

                // Create a NetworkCredential object for authentication
                NetworkCredential credentials = new NetworkCredential(username, password);

                // Get the list of files in the local folder
                List<string> files = GetFilesRecursively(localFolder);

                foreach (string file in files)
                {
                    var path = file.Replace(Path.Combine(Directory.GetCurrentDirectory(), "Patch"), "");
                    path = path.Replace(Path.GetFileName(path), "");
                    path = path.Replace('\\', '/');
                    
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri + path);
                    request.Method = WebRequestMethods.Ftp.MakeDirectory;
                    request.Credentials = credentials;

                    try
                    {
                        using (var resp = (FtpWebResponse)request.GetResponse())
                        {
                            Console.WriteLine(resp.StatusCode);
                        }
                    }
                    catch (Exception ex) { }
                }

                foreach (string file in files)
                {

                    try
                    {
                        var path = file.Replace(Path.Combine(Directory.GetCurrentDirectory(), "Patch"), "");
                        path = path.Replace(Path.GetFileName(path), "");
                        path = path.Replace('\\', '/');

                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri + path + Path.GetFileName(file));
                        request.Method = WebRequestMethods.Ftp.UploadFile;
                        request.Credentials = credentials;

                        using (FileStream fileStream = File.OpenRead(file))
                        using (Stream ftpStream = request.GetRequestStream())
                        {
                            byte[] buffer = new byte[1024];
                            int bytesRead;

                            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                ftpStream.Write(buffer, 0, bytesRead);
                            }
                        }
                        FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                        Console.WriteLine($"Uploaded: {file} => {response.StatusDescription}");
                        response.Close();
                    } catch (Exception ex) { }

                    
                }

                Console.WriteLine("Upload complete.");
                Console.ReadLine();
            }
        }
        static void CopyFolder(string sourceFolder, string targetFolder)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
            {                
                Directory.CreateDirectory(dirPath.Replace(sourceFolder, targetFolder));
            }
            if(!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            foreach (string filePath in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                File.Copy(filePath, filePath.Replace(sourceFolder, targetFolder), true);
            }

        }
        static void CreateZipFile(List<string> allFiles)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            CreateFolder();
            File.Move(Path.Combine(Directory.GetCurrentDirectory(), "MD5.txt"), Path.Combine(Directory.GetCurrentDirectory(), "Patch", "MD5.txt"));

            
            
            string sourceFolder = Path.Combine(currentDirectory, "Patch");
            string zipFilePath = Path.Combine(currentDirectory, "Patch.zip");
            try
            {
               ZipFile.CreateFromDirectory(sourceFolder, zipFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }


        }
        static List<string> GetFilesRecursively(string directory)
        {
            List<string> allFiles = new List<string>();

            // Get all files in the current directory.
            string[] files = Directory.GetFiles(directory);
            foreach (string file in files) 
            {
                Console.WriteLine(file);
            }
            allFiles.AddRange(files);

            // Recursively get files in subdirectories.
            string[] subdirectories = Directory.GetDirectories(directory);
            foreach (string subdirectory in subdirectories)
            {
                allFiles.AddRange(GetFilesRecursively(subdirectory));
            }

            
            return allFiles;
        }
        static string GetRelativePath(string baseDirectory, string fullPath)
        {
            Uri baseUri = new Uri(baseDirectory + Path.DirectorySeparatorChar);
            Uri fullUri = new Uri(fullPath);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

            return Uri.UnescapeDataString(relativeUri.ToString());
        }
    }
}
